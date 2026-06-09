namespace BelgianEid.Bridge.Common;

/// <summary>
/// Thrown when a request from the Chrome extension is malformed or is missing a required field.
/// Results in a structured error response being sent back to the extension.
/// </summary>
public sealed class BridgeRequestException : Exception
{
    public BridgeRequestException(string message) : base(message) { }
}
