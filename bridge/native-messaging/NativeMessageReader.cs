using System.Text.Json;

namespace BelgianEid.Bridge.NativeMessaging;

/// <summary>
/// Reads messages from stdin using the Chrome Native Messaging framing protocol.
/// Each message is preceded by a 4-byte little-endian unsigned integer that
/// specifies the byte length of the UTF-8 JSON payload that follows.
/// </summary>
/// <remarks>
/// Reference: https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging#native-messaging-host-protocol
/// </remarks>
public sealed class NativeMessageReader : INativeMessageReader
{
    private const int MaxMessageBytes = 1_048_576; // 1 MiB — Chrome's own limit

    private readonly Stream _input;

    public NativeMessageReader(Stream input)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
    }

    /// <inheritdoc/>
    public JsonElement? Read()
    {
        var lengthBuffer = new byte[4];
        int bytesRead = _input.ReadAtLeast(lengthBuffer, 4, throwOnEndOfStream: false);

        if (bytesRead == 0)
            return null; // EOF — Chrome has closed the native port

        if (bytesRead < 4)
            throw new IOException($"Incomplete length header: read {bytesRead}/4 bytes.");

        int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

        if (messageLength <= 0 || messageLength > MaxMessageBytes)
            throw new IOException($"Invalid message length in frame header: {messageLength} bytes.");

        var payload = new byte[messageLength];
        _input.ReadExactly(payload);

        return JsonSerializer.Deserialize<JsonElement>(payload);
    }
}
