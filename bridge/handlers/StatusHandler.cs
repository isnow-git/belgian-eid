using System.Text.Json;
using BelgianEid.Bridge.Services;

namespace BelgianEid.Bridge.Handlers;

/// <summary>
/// Handles <c>{ "type": "status" }</c> messages.
/// Reports whether a PC/SC card reader is connected and whether an eID card is inserted.
/// Useful for polling the hardware state before issuing read or sign operations.
/// </summary>
public sealed class StatusHandler : IMessageHandler
{
    private readonly IEidService _eidService;

    public StatusHandler(IEidService eidService)
    {
        _eidService = eidService ?? throw new ArgumentNullException(nameof(eidService));
    }

    public string MessageType => "get_status";

    public ValueTask<object> HandleAsync(JsonElement message, string? requestId)
    {
        var (readerPresent, cardPresent) = _eidService.GetStatus();

        return ValueTask.FromResult<object>(new { id = requestId, readerPresent, cardPresent });
    }
}
