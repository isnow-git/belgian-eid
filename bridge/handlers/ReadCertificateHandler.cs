using System.Text.Json;
using BelgianEid.Bridge.Common;
using BelgianEid.Bridge.Services;

namespace BelgianEid.Bridge.Handlers;

/// <summary>
/// Handles <c>{ "type": "read_certificate", "kind": "..." }</c> messages.
/// Reads a single X.509 certificate from the card and returns it as a Base64-encoded DER blob.
/// </summary>
/// <remarks>
/// The <c>kind</c> field accepts (case-insensitive):
/// <c>authentication</c>, <c>signature</c>, <c>intermediateCA</c> / <c>citizenCA</c>, <c>root</c>.
/// </remarks>
public sealed class ReadCertificateHandler : IMessageHandler
{
    private readonly IEidService _eidService;

    public ReadCertificateHandler(IEidService eidService)
    {
        _eidService = eidService ?? throw new ArgumentNullException(nameof(eidService));
    }

    public string MessageType => "read_certificate";

    public ValueTask<object> HandleAsync(JsonElement message, string? requestId)
    {
        var kind = message.RequireCertificateKind("kind");
        var raw = _eidService.ReadCertificate(kind);

        return ValueTask.FromResult<object>(new
        {
            id = requestId,
            kind = kind.ToString().ToLowerInvariant(),
            certificate = Convert.ToBase64String(raw),
        });
    }
}
