using System.Text.Json;
using System.Text.Json.Serialization;

namespace BelgianEid.Bridge.NativeMessaging;

/// <summary>
/// Writes messages to stdout using the Chrome Native Messaging framing protocol.
/// Each message is prefixed with a 4-byte little-endian unsigned integer that
/// specifies the byte length of the UTF-8 JSON payload.
/// </summary>
/// <remarks>
/// Reference: https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging#native-messaging-host-protocol
/// </remarks>
public sealed class NativeMessageWriter : INativeMessageWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Stream _output;
    private readonly object _writeLock = new();

    public NativeMessageWriter(Stream output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    /// <inheritdoc/>
    public void Write(object message)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        var header = BitConverter.GetBytes(payload.Length);

        lock (_writeLock)
        {
            _output.Write(header);
            _output.Write(payload);
            _output.Flush();
        }
    }

    /// <inheritdoc/>
    public void WriteError(string? requestId, string errorMessage) =>
        Write(new { id = requestId, error = errorMessage });
}
