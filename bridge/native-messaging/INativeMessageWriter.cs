namespace BelgianEid.Bridge.NativeMessaging;

/// <summary>
/// Writes framed messages to a Chrome Native Messaging host connection.
/// </summary>
public interface INativeMessageWriter
{
    /// <summary>Serialises <paramref name="message"/> and writes it to the output stream.</summary>
    void Write(object message);

    /// <summary>
    /// Writes a structured <c>{ id, error }</c> error response to the output stream.
    /// </summary>
    void WriteError(string? requestId, string errorMessage);
}
