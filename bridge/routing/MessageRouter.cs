using System.Text.Json;
using BelgianEid.Bridge.Handlers;

namespace BelgianEid.Bridge.Routing;

/// <summary>
/// Dictionary-based router that dispatches incoming messages to the registered
/// <see cref="IMessageHandler"/> whose <see cref="IMessageHandler.MessageType"/> matches
/// the message's <c>type</c> field (case-insensitive).
/// </summary>
/// <remarks>
/// <b>Open/Closed Principle:</b> adding a new command requires only implementing
/// <see cref="IMessageHandler"/> and registering it in <c>Program.cs</c>.
/// No modification to this class is ever needed.
/// </remarks>
public sealed class MessageRouter : IMessageRouter
{
    private readonly IReadOnlyDictionary<string, IMessageHandler> _handlers;

    /// <param name="handlers">
    /// The set of handlers to register. Each handler's
    /// <see cref="IMessageHandler.MessageType"/> must be unique.
    /// </param>
    public MessageRouter(IEnumerable<IMessageHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        _handlers = handlers.ToDictionary(
            keySelector: h => h.MessageType,
            comparer: StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public ValueTask<object> RouteAsync(JsonElement message)
    {
        string? requestId = message.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        string? type = message.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

        if (type is null || !_handlers.TryGetValue(type, out var handler))
            return ValueTask.FromResult<object>(
                new { id = requestId, error = $"Unknown message type: '{type}'" });

        return handler.HandleAsync(message, requestId);
    }
}
