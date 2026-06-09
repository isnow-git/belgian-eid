using System.Text.Json;
using BelgianEid.Bridge.NativeMessaging;
using BelgianEid.Bridge.Routing;
using BelgianEid.Exceptions;

namespace BelgianEid.Bridge.Hosting;

/// <summary>
/// Orchestrates the Native Messaging message loop.
/// Reads JSON messages from <see cref="INativeMessageReader"/>, routes each one
/// through <see cref="IMessageRouter"/>, and writes the response back via
/// <see cref="INativeMessageWriter"/>.
/// </summary>
/// <remarks>
/// The host terminates cleanly on EOF (Chrome closed the native port) or on a
/// fatal read error. Per-message exceptions are caught and converted to structured
/// error responses so that the extension always receives a valid JSON reply.
/// </remarks>
public sealed class NativeMessagingHost
{
    private readonly INativeMessageReader _reader;
    private readonly INativeMessageWriter _writer;
    private readonly IMessageRouter _router;

    public NativeMessagingHost(
        INativeMessageReader reader,
        INativeMessageWriter writer,
        IMessageRouter router)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _router = router ?? throw new ArgumentNullException(nameof(router));
    }

    /// <summary>
    /// Runs the message loop until Chrome closes the connection (EOF)
    /// or an unrecoverable read error occurs.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            JsonElement? message;

            try
            {
                message = _reader.Read();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[BelgianEidBridge] Fatal read error — shutting down: {ex.Message}");
                return;
            }

            if (message is null)
                return; // EOF — Chrome has closed the native port

            await ProcessMessageAsync(message.Value);
        }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private async Task ProcessMessageAsync(JsonElement message)
    {
        // Extract the correlation ID upfront so exception handlers can echo it back.
        string? requestId = message.TryGetProperty("id", out var idProp)
            ? idProp.GetString()
            : null;

        try
        {
            var response = await _router.RouteAsync(message);
            _writer.Write(response);
        }
        catch (EidPinIncorrectException ex)
        {
            // Propagate remaining-attempts so the extension can warn the user.
            _writer.Write(new
            {
                id = requestId,
                error = ex.Message,
                triesRemaining = ex.RemainingAttempts,
                blocked = false,
            });
        }
        catch (EidPinBlockedException ex)
        {
            _writer.Write(new
            {
                id = requestId,
                error = ex.Message,
                triesRemaining = 0,
                blocked = true,
            });
        }
        catch (EidReaderNotFoundException ex)
        {
            Console.Error.WriteLine($"[BelgianEidBridge] No reader found: {ex.Message}");
            _writer.WriteError(requestId, ex.Message);
        }
        catch (EidCardNotPresentException ex)
        {
            Console.Error.WriteLine($"[BelgianEidBridge] No card present: {ex.Message}");
            _writer.WriteError(requestId, ex.Message);
        }
        catch (EidException ex)
        {
            Console.Error.WriteLine($"[BelgianEidBridge] EidException: {ex.Message}");
            _writer.WriteError(requestId, ex.Message);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[BelgianEidBridge] Unexpected exception: {ex}");
            // DEBUG — expose real exception so portal can display it
            _writer.WriteError(requestId, $"[{ex.GetType().Name}] {ex.Message}");
        }
    }
}
