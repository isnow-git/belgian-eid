using BelgianEid.Abstractions;
using BelgianEid.Exceptions;
using BelgianEid.Models;
using Microsoft.Extensions.Logging;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;

namespace BelgianEid.Implementations;

/// <summary>
/// Detects smart card readers and opens sessions to the eID card.
/// </summary>
public sealed class EidReaderService : IEidReaderService
{
    private readonly IPkcs11LibraryProvider _libraryProvider;
    private readonly ILogger<EidReaderService> _logger;
    private readonly Pkcs11InteropFactories _factories = new();

    public EidReaderService(
        IPkcs11LibraryProvider libraryProvider,
        ILogger<EidReaderService>? logger = null)
    {
        _libraryProvider = libraryProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EidReaderService>.Instance;
    }

    /// <inheritdoc/>
    public IReadOnlyList<EidReader> GetAvailableReaders()
    {
        IPkcs11Library library = _libraryProvider.GetLibrary();
        var readers = new List<EidReader>();

        try
        {
            foreach (ISlot slot in library.GetSlotList(SlotsType.WithOrWithoutTokenPresent))
            {
                ISlotInfo info = slot.GetSlotInfo();
                readers.Add(new EidReader
                {
                    SlotId = slot.SlotId,
                    Name = info.SlotDescription?.Trim() ?? $"Slot {slot.SlotId}",
                    HasCardInserted = info.SlotFlags.TokenPresent,
                });
            }
        }
        catch (Pkcs11Exception ex)
        {
            throw new EidCommunicationException("Failed to enumerate card readers.", ex);
        }

        _logger.LogInformation("{Count} reader(s) detected.", readers.Count);
        return readers;
    }

    /// <inheritdoc/>
    public IEidSession OpenSession(EidReader? reader = null)
    {
        IReadOnlyList<EidReader> readers = GetAvailableReaders();
        if (readers.Count == 0)
            throw new EidReaderNotFoundException();

        EidReader target = reader is null
            ? readers.FirstOrDefault(r => r.HasCardInserted)
              ?? throw new EidCardNotPresentException()
            : readers.FirstOrDefault(r => r.SlotId == reader.SlotId)
              ?? throw new EidReaderNotFoundException(
                  $"The reader '{reader.Name}' (slot {reader.SlotId}) is no longer available.");

        if (!target.HasCardInserted)
            throw new EidCardNotPresentException(
                $"No eID card in reader '{target.Name}'.");

        ISlot slot = ResolveSlot(target.SlotId);
        _logger.LogInformation("Opening a session on '{Reader}'.", target.Name);
        return new EidSession(_factories, slot, target, _logger);
    }

    private ISlot ResolveSlot(ulong slotId)
    {
        IPkcs11Library library = _libraryProvider.GetLibrary();
        return library.GetSlotList(SlotsType.WithTokenPresent)
                   .FirstOrDefault(s => s.SlotId == slotId)
               ?? throw new EidCardNotPresentException();
    }
}
