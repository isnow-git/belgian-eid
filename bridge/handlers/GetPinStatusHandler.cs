using System.Text.Json;
using BelgianEid.Bridge.Services;

namespace BelgianEid.Bridge.Handlers;

/// <summary>
/// Handles <c>{ "type": "get_pin_status" }</c> messages.
/// Returns the current PIN retry counter and blocked state without attempting a verification.
/// Use this before <c>verify_pin</c> to warn the user when only one attempt remains.
/// </summary>
public sealed class GetPinStatusHandler : IMessageHandler
{
    private readonly IEidService _eidService;

    public GetPinStatusHandler(IEidService eidService)
    {
        _eidService = eidService ?? throw new ArgumentNullException(nameof(eidService));
    }

    public string MessageType => "get_pin_status";

    public ValueTask<object> HandleAsync(JsonElement message, string? requestId)
    {
        var status = _eidService.GetPinStatus();

        return ValueTask.FromResult<object>(new
        {
            id = requestId,
            remainingAttempts = status.RemainingAttempts,
            isBlocked = status.IsBlocked,
        });
    }
}
