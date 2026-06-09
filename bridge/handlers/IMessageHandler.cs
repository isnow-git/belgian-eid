using System.Text.Json;

namespace BelgianEid.Bridge.Handlers;

/// <summary>
/// Handles one specific type of incoming message from the Chrome extension.
/// Each implementation is responsible for a single command (Single Responsibility Principle).
/// New commands are added by implementing this interface without modifying the router
/// or any existing handler (Open/Closed Principle).
/// </summary>
public interface IMessageHandler
{
    /// <summary>
    /// The <c>type</c> field value that this handler responds to (e.g. <c>"ping"</c>).
    /// </summary>
    string MessageType { get; }

    /// <summary>
    /// Processes the incoming message and returns a JSON-serialisable response object.
    /// Synchronous handlers return a completed <see cref="ValueTask{T}"/>;
    /// asynchronous handlers (e.g. signing) can truly await.
    /// </summary>
    /// <param name="message">The full JSON message received from the extension.</param>
    /// <param name="requestId">The correlation ID extracted from the message, or <c>null</c>.</param>
    ValueTask<object> HandleAsync(JsonElement message, string? requestId);
}
