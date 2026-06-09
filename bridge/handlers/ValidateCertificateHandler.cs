using System.Text.Json;
using BelgianEid.Bridge.Common;
using BelgianEid.Bridge.Services;

namespace BelgianEid.Bridge.Handlers;

/// <summary>
/// Handles <c>{ "type": "validate_certificate", "kind": "..." }</c> messages.
/// Queries the official Belgian OCSP responder to check whether the certificate
/// of the given <c>kind</c> has been revoked.
/// </summary>
/// <remarks>
/// The <c>kind</c> field accepts: <c>authentication</c>, <c>signature</c>,
/// <c>intermediateCA</c>, <c>root</c>.
/// This operation requires an active internet connection to reach the OCSP server.
/// </remarks>
public sealed class ValidateCertificateHandler : IMessageHandler
{
    private readonly IEidService _eidService;

    public ValidateCertificateHandler(IEidService eidService)
    {
        _eidService = eidService ?? throw new ArgumentNullException(nameof(eidService));
    }

    public string MessageType => "validate_certificate";

    public async ValueTask<object> HandleAsync(JsonElement message, string? requestId)
    {
        var kind = message.RequireCertificateKind("kind");
        var result = await _eidService.ValidateCertificateAsync(kind);

        return new
        {
            id = requestId,
            kind = kind.ToString().ToLowerInvariant(),
            status = result.Status.ToString(),
            isValid = result.IsValid,
            producedAt = result.ProducedAt?.ToString("o"),
            revocationTime = result.RevocationTime?.ToString("o"),
            revocationReason = result.RevocationReason,
        };
    }
}
