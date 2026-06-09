using System.Text.Json;
using BelgianEid.Bridge.Common;
using BelgianEid.Bridge.Services;

namespace BelgianEid.Bridge.Handlers;

/// <summary>
/// Handles <c>{ "type": "sign_challenge", "pin": "...", "challenge": "...", "algorithm": "sha256" }</c>.
/// Signs a server-issued challenge using the card's <b>authentication</b> key.
///
/// <para>
/// The <c>challenge</c> field may be Base64 standard (<c>+/=</c>) or Base64Url (<c>-_</c> without padding).
/// Both encodings are accepted and normalized internally.
/// </para>
/// </summary>
public sealed class SignChallengeHandler : IMessageHandler
{
    private readonly IEidService _eidService;

    public SignChallengeHandler(IEidService eidService)
    {
        _eidService = eidService ?? throw new ArgumentNullException(nameof(eidService));
    }

    public string MessageType => "sign_challenge";

    public async ValueTask<object> HandleAsync(JsonElement message, string? requestId)
    {
        var pin = message.RequireString("pin");

        // Accept both standard Base64 and Base64Url (no padding, - and _ chars).
        var challengeEncoded = message.RequireString("challenge");
        var challengeBytes   = FromBase64OrBase64Url(challengeEncoded);

        var algorithm = message.GetHashAlgorithmOrDefault("algorithm");

        var result = await _eidService.SignChallengeAsync(challengeBytes, pin, algorithm);

        // Read the intermediate CA certificate (Citizen CA) stored on the card.
        // Required by the server for PKIX chain validation up to the Belgium Root CA.
        var certs          = _eidService.ReadCertificates();
        var intermediateCa = Convert.ToBase64String(certs.IntermediateCa.RawData);

        return new
        {
            id              = requestId,
            signature       = Convert.ToBase64String(result.Signature),
            authCertificate = Convert.ToBase64String(result.AuthenticationCertificate.RawData),
            caCertificates  = new[] { intermediateCa },
            algorithm       = result.HashAlgorithm.ToString().ToLowerInvariant(),
        };
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static byte[] FromBase64OrBase64Url(string encoded)
    {
        // Normalise Base64Url → standard Base64
        var standard = encoded.Replace('-', '+').Replace('_', '/');
        switch (standard.Length % 4)
        {
            case 2: standard += "=="; break;
            case 3: standard += "=";  break;
        }
        return Convert.FromBase64String(standard);
    }
}
