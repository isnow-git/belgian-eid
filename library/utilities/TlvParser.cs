namespace BelgianEid.Utilities;

/// <summary>
/// Parser for TLV (Tag-Length-Value) structures used by the identity and address
/// files on the Belgian eID card.
/// </summary>
/// <remarks>
/// eID format: each record is encoded as 1 tag byte, 1 length byte,
/// then the value. Fields never exceed 255 bytes.
/// </remarks>
internal static class TlvParser
{
    /// <summary>
    /// Parses a TLV block and returns a table mapping each tag to its raw value.
    /// When tags are repeated, the last occurrence wins.
    /// </summary>
    public static IReadOnlyDictionary<int, byte[]> Parse(byte[]? data)
    {
        var result = new Dictionary<int, byte[]>();
        if (data is null || data.Length == 0)
            return result;

        int index = 0;
        while (index + 1 < data.Length)
        {
            int tag = data[index++];

            // Padding byte: logical end of the file.
            if (tag == 0x00)
                break;

            int length = data[index++];

            // Malformed structure: stop cleanly.
            if (index + length > data.Length)
                break;

            var value = new byte[length];
            Array.Copy(data, index, value, 0, length);
            index += length;

            result[tag] = value;
        }

        return result;
    }
}
