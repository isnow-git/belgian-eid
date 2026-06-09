using System.Text.Json;
using BelgianEid.Bridge.Services;

namespace BelgianEid.Bridge.Handlers;

/// <summary>
/// Handles <c>{ "type": "read_certificates" }</c> messages.
/// Reads all four X.509 certificates from the card and returns each as a Base64-encoded DER blob.
/// </summary>
public sealed class ReadCertificatesHandler : IMessageHandler
{
    private readonly IEidService _eidService;

    public ReadCertificatesHandler(IEidService eidService)
    {
        _eidService = eidService ?? throw new ArgumentNullException(nameof(eidService));
    }

    public string MessageType => "read_certificates";

    public ValueTask<object> HandleAsync(JsonElement message, string? requestId)
    {
        var certs = _eidService.ReadCertificates();

        return ValueTask.FromResult<object>(new
        {
            id = requestId,
            authentication = Convert.ToBase64String(certs.Authentication.RawData),
            signature = Convert.ToBase64String(certs.Signature.RawData),
            intermediateCA = Convert.ToBase64String(certs.IntermediateCa.RawData),
            root = Convert.ToBase64String(certs.Root.RawData),
        });
    }
}
