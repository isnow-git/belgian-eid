using System.Text.Json;

namespace BelgianEid.Bridge.Routing;

/// <summary>
/// Dispatches an incoming JSON message to the appropriate <see cref="Handlers.IMessageHandler"/>
/// based on the message's <c>type</c> field.
/// </summary>
public interface IMessageRouter
{
    /// <summary>
    /// Routes the message to a registered handler and returns a JSON-serialisable response.
    /// Returns a structured error response when no handler matches the message type.
    /// Supports asynchronous handlers (e.g. signing operations).
    /// </summary>
    ValueTask<object> RouteAsync(JsonElement message);
}
