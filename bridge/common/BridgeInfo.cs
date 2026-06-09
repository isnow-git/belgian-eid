namespace BelgianEid.Bridge.Common;

/// <summary>
/// Immutable metadata describing this bridge application instance.
/// Injected into <see cref="Handlers.PingHandler"/> at startup.
/// </summary>
public sealed record BridgeInfo(string Version, string ServiceName);
