using System.Text.Json;

namespace BelgianEid.Bridge.NativeMessaging;

/// <summary>
/// Reads framed messages from a Chrome Native Messaging host connection.
/// </summary>
public interface INativeMessageReader
{
    /// <summary>
    /// Reads the next message from the input stream.
    /// </summary>
    /// <returns>
    /// The parsed <see cref="JsonElement"/>, or <c>null</c> on EOF
    /// (i.e. Chrome has closed the native port).
    /// </returns>
    /// <exception cref="System.IO.IOException">
    /// Thrown when the stream contains a malformed or oversized frame.
    /// </exception>
    JsonElement? Read();
}
