using System.Text.Json;
using BelgianEid.Bridge.Common;

namespace BelgianEid.Bridge.Handlers;

/// <summary>
/// Handles <c>{ "type": "ping" }</c> messages.
/// Returns the bridge's service name and current version, allowing the Chrome
/// extension to verify that the host is alive and check for version compatibility.
/// </summary>
public sealed class PingHandler : IMessageHandler
{
    private readonly BridgeInfo _info;

    public PingHandler(BridgeInfo info)
    {
        _info = info ?? throw new ArgumentNullException(nameof(info));
    }

    public string MessageType => "ping";

    public ValueTask<object> HandleAsync(JsonElement message, string? requestId) =>
        ValueTask.FromResult<object>(new
        {
            id = requestId,
            status = "ok",
            service = _info.ServiceName,
            version = _info.Version,
        });
}
