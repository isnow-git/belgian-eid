using System.Text.Json;
using BelgianEid.Bridge.Common;
using BelgianEid.Bridge.Services;

namespace BelgianEid.Bridge.Handlers;

/// <summary>
/// Handles <c>{ "type": "verify_pin", "pin": "..." }</c> messages.
/// Verifies the supplied PIN against the card's PIN counter.
/// </summary>
/// <remarks>
/// On failure the eID card decrements its retry counter.
/// When it reaches zero the card is blocked; unblocking requires a visit
/// to the municipal administration (handled via <c>EidPinBlockedException</c>
/// at the hosting layer).
/// </remarks>
public sealed class VerifyPinHandler : IMessageHandler
{
    private readonly IEidService _eidService;

    public VerifyPinHandler(IEidService eidService)
    {
        _eidService = eidService ?? throw new ArgumentNullException(nameof(eidService));
    }

    public string MessageType => "verify_pin";

    public ValueTask<object> HandleAsync(JsonElement message, string? requestId)
    {
        var pin = message.RequireString("pin");
        _eidService.VerifyPin(pin);

        return ValueTask.FromResult<object>(new { id = requestId, verified = true });
    }
}
