using System.Text.Json;
using BelgianEid.Bridge.Common;
using BelgianEid.Bridge.Services;

namespace BelgianEid.Bridge.Handlers;

/// <summary>
/// Handles <c>{ "type": "sign_data", "pin": "...", "data": "...", "algorithm": "sha256" }</c>.
/// Signs raw data using the card's <b>non-repudiation</b> (signature) key.
/// The BelgianEid library computes the digest of <c>data</c> internally before signing.
/// </summary>
/// <remarks>
/// The <c>data</c> field must be the <b>raw bytes to sign</b>, Base64-encoded.
/// Only suitable for small payloads. For large documents (PDFs, etc.) compute the hash
/// client-side and use <c>sign_hash</c> instead.
/// The <c>algorithm</c> field is optional and defaults to <c>sha256</c>.
/// </remarks>
public sealed class SignDataHandler : IMessageHandler
{
    private readonly IEidService _eidService;

    public SignDataHandler(IEidService eidService)
    {
        _eidService = eidService ?? throw new ArgumentNullException(nameof(eidService));
    }

    public string MessageType => "sign_data";

    public async ValueTask<object> HandleAsync(JsonElement message, string? requestId)
    {
        var pin = message.RequireString("pin");
        var data = Convert.FromBase64String(message.RequireString("data"));
        var algorithm = message.GetHashAlgorithmOrDefault("algorithm");

        var result = await _eidService.SignDataAsync(data, pin, algorithm);

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
