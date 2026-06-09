using BelgianEid.Abstractions;
using BelgianEid.Exceptions;
using BelgianEid.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BelgianEid.Implementations;

/// <summary>
/// Monitors smart card readers by periodically polling <see cref="IEidReaderService"/>.
/// Raises <see cref="ReaderChanged"/> for each reader connect/disconnect
/// and each card insertion/removal.
/// </summary>
public sealed class EidReaderMonitor : IEidReaderMonitor
{
    private readonly IEidReaderService _readerService;
    private readonly ILogger<EidReaderMonitor> _logger;
    private readonly TimeSpan _pollInterval;

    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private IReadOnlyList<EidReader> _previousReaders = [];

    public event EventHandler<EidReaderChangedEventArgs>? ReaderChanged;

    public EidReaderMonitor(
        IEidReaderService readerService,
        ILogger<EidReaderMonitor>? logger = null,
        TimeSpan? pollInterval = null)
    {
        _readerService = readerService ?? throw new ArgumentNullException(nameof(readerService));
        _logger = logger ?? NullLogger<EidReaderMonitor>.Instance;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(500);
    }

    /// <inheritdoc/>
    public void Start(CancellationToken cancellationToken = default)
    {
        if (_monitorTask is { IsCompleted: false })
            return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _monitorTask = Task.Run(() => MonitorLoopAsync(_cts.Token), _cts.Token);
    }

    /// <inheritdoc/>
    public void Stop()
    {
        _cts?.Cancel();
        try { _monitorTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts?.Dispose();
        _cts = null;
        _monitorTask = null;
    }

    /// <inheritdoc/>
    public void Dispose() => Stop();

    // ── Private ───────────────────────────────────────────────────────────────

    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        _previousReaders = SafeGetReaders();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_pollInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var current = SafeGetReaders();
            DetectChanges(_previousReaders, current);
            _previousReaders = current;
        }
    }

    private IReadOnlyList<EidReader> SafeGetReaders()
    {
        try
        {
            return _readerService.GetAvailableReaders();
        }
        catch (EidException ex)
        {
            _logger.LogDebug("Reader enumeration failed: {Message}", ex.Message);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Unexpected error during reader enumeration: {Message}", ex.Message);
            return [];
        }
    }

    private void DetectChanges(IReadOnlyList<EidReader> previous, IReadOnlyList<EidReader> current)
    {
        var previousById = previous.ToDictionary(r => r.SlotId);
        var currentById = current.ToDictionary(r => r.SlotId);

        foreach (var reader in current.Where(r => !previousById.ContainsKey(r.SlotId)))
            Raise(EidReaderEventKind.ReaderConnected, reader);

        foreach (var reader in previous.Where(r => !currentById.ContainsKey(r.SlotId)))
            Raise(EidReaderEventKind.ReaderDisconnected, reader);

        foreach (var cur in current)
        {
            if (!previousById.TryGetValue(cur.SlotId, out var prev)) continue;
            if (cur.HasCardInserted && !prev.HasCardInserted)
                Raise(EidReaderEventKind.CardInserted, cur);
            else if (!cur.HasCardInserted && prev.HasCardInserted)
                Raise(EidReaderEventKind.CardRemoved, cur);
        }
    }

    private void Raise(EidReaderEventKind kind, EidReader reader)
    {
        _logger.LogInformation("Reader event: {Kind} — {Reader}", kind, reader);
        ReaderChanged?.Invoke(this, new EidReaderChangedEventArgs { Kind = kind, Reader = reader });
    }
}
