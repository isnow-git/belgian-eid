using System.Text.Json;
using BelgianEid.Bridge.Common;
using BelgianEid.Bridge.Services;

namespace BelgianEid.Bridge.Handlers;

/// <summary>
/// Handles <c>{ "type": "sign_hash", "pin": "...", "hash": "...", "algorithm": "sha256" }</c>.
/// Signs a pre-computed hash using the card's <b>non-repudiation</b> (signature) key.
/// Produces a <b>qualified electronic signature</b> legally binding under Belgian/eIDAS law.
/// </summary>
/// <remarks>
/// The <c>hash</c> field must be the <b>pre-computed digest</b> of the document, Base64-encoded.
/// The <c>algorithm</c> field specifies which algorithm produced the hash (default: <c>sha256</c>).
/// The hash length must match the declared algorithm (32 bytes for SHA-256, etc.).
/// </remarks>
public sealed class SignHashHandler : IMessageHandler
{
    private readonly IEidService _eidService;

    public SignHashHandler(IEidService eidService)
    {
        _eidService = eidService ?? throw new ArgumentNullException(nameof(eidService));
    }

    public string MessageType => "sign_hash";

    public async ValueTask<object> HandleAsync(JsonElement message, string? requestId)
    {
        var pin = message.RequireString("pin");
        var hash = Convert.FromBase64String(message.RequireString("hash"));
        var algorithm = message.GetHashAlgorithmOrDefault("algorithm");

        var result = await _eidService.SignHashAsync(hash, pin, algorithm);

        return new
        {
            id = requestId,
            signature = Convert.ToBase64String(result.Signature),
            certificate = Convert.ToBase64String(result.SignerCertificate.RawData),
            algorithm = result.HashAlgorithm.ToString().ToLowerInvariant(),
            signedAtUtc = result.SignedAtUtc.ToString("o"),
        };
    }
}
