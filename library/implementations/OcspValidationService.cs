using System.Security.Cryptography.X509Certificates;
using BelgianEid.Abstractions;
using BelgianEid.Configuration;
using BelgianEid.Exceptions;
using BelgianEid.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Ocsp;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using BcX509 = Org.BouncyCastle.X509.X509Certificate;

namespace BelgianEid.Implementations;

public sealed class OcspValidationService : IOcspValidationService
{
    private const string OcspRequestContentType = "application/ocsp-request";

    private readonly HttpClient _httpClient;
    private readonly BelgianEidOptions _options;
    private readonly ILogger<OcspValidationService> _logger;

    public OcspValidationService(
        HttpClient httpClient,
        IOptions<BelgianEidOptions> options,
        ILogger<OcspValidationService>? logger = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger ?? NullLogger<OcspValidationService>.Instance;
    }

    /// <inheritdoc/>
    public async Task<OcspResult> CheckAsync(
        X509Certificate2 certificate,
        X509Certificate2 issuerCertificate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentNullException.ThrowIfNull(issuerCertificate);

        BcX509 subject = DotNetUtilities.FromX509Certificate(certificate);
        BcX509 issuer = DotNetUtilities.FromX509Certificate(issuerCertificate);

        byte[] requestBytes = BuildRequest(subject, issuer);
        byte[] responseBytes = await PostAsync(requestBytes, certificate, cancellationToken).ConfigureAwait(false);

        return ParseResponse(responseBytes);
    }

    /// <inheritdoc/>
    public async Task EnsureNotRevokedAsync(
        X509Certificate2 certificate,
        X509Certificate2 issuerCertificate,
        CancellationToken cancellationToken = default)
    {
        OcspResult result = await CheckAsync(certificate, issuerCertificate, cancellationToken)
            .ConfigureAwait(false);

        if (result.Status == OcspCertificateStatus.Revoked)
            throw new EidCertificateRevokedException(result.RevocationTime);
    }

    private static byte[] BuildRequest(BcX509 subject, BcX509 issuer)
    {
        // SHA-256: the Belgian /2 responder no longer accepts SHA-1 in the CertID.
        var certificateId = new CertificateID(
            NistObjectIdentifiers.IdSha256.Id,
            issuer,
            subject.SerialNumber);

        var generator = new OcspReqGenerator();
        generator.AddRequest(certificateId);

        // Without nonce: simpler and avoids replay-check issues on the server side.
        return generator.Generate().GetEncoded();
    }

    private async Task<byte[]> PostAsync(
        byte[] request,
        X509Certificate2 certificate,
        CancellationToken cancellationToken)
    {
        // Prefer the URL contained in the certificate's AIA extension (compatible with all
        // generations of Belgian cards). Fall back to the URL configured in options.
        Uri ocspUrl = GetOcspUrlFromCertificate(certificate) ?? _options.OcspResponderUrl;

        _logger.LogInformation("OCSP URL: {}", ocspUrl.AbsoluteUri);

        using var content = new ByteArrayContent(request);
        content.Headers.ContentType = new MediaTypeHeaderValue(OcspRequestContentType);

        // HttpRequestMessage required to add the Accept header (request header,
        // not available on HttpContentHeaders).
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ocspUrl);
        httpRequest.Content = content;
        httpRequest.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/ocsp-response"));

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.OcspTimeout);

            _logger.LogDebug("Sending OCSP request to {Url} (size: {Size} bytes)",
                ocspUrl, request.Length);

            using HttpResponseMessage response = await _httpClient
                .SendAsync(httpRequest, cts.Token)
                .ConfigureAwait(false);

            _logger.LogDebug("OCSP response received: {StatusCode}", response.StatusCode);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            throw new EidCommunicationException(
                $"Communication with the OCSP responder failed ({ocspUrl}).", ex);
        }
    }

    /// <summary>
    /// Extracts the OCSP responder URL from the AIA (Authority Information Access) extension
    /// of the certificate. Returns <c>null</c> if the extension is absent or contains no OCSP URI.
    /// </summary>
    private static Uri? GetOcspUrlFromCertificate(X509Certificate2 certificate)
    {
        // X509AuthorityInformationAccessExtension is natively available in .NET 8.
        var aia = certificate.Extensions
            .OfType<X509AuthorityInformationAccessExtension>()
            .FirstOrDefault();

        string? ocspUri = aia?.EnumerateOcspUris().FirstOrDefault();
        return ocspUri is not null ? new Uri(ocspUri) : null;
    }

    private OcspResult ParseResponse(byte[] responseBytes)
    {
        OcspResp response;
        try
        {
            response = new OcspResp(responseBytes);
        }
        catch (Exception ex)
        {
            throw new EidCommunicationException("OCSP response is unreadable or malformed.", ex);
        }

        if (response.Status != OcspRespStatus.Successful)
            throw new EidCommunicationException(
                $"The OCSP responder returned an error status ({response.Status}).");

        if (response.GetResponseObject() is not BasicOcspResp basic || basic.Responses.Length == 0)
            throw new EidCommunicationException("Empty OCSP response.");

        SingleResp single = basic.Responses[0];
        object? certStatus = single.GetCertStatus();

        if (certStatus is null)
        {
            _logger.LogInformation("OCSP: certificate is valid (not revoked).");
            return new OcspResult
            {
                Status = OcspCertificateStatus.Good,
                ProducedAt = basic.ProducedAt,
            };
        }

        if (certStatus is RevokedStatus revoked)
        {
            _logger.LogWarning("OCSP: certificate REVOKED on {Date}.", revoked.RevocationTime);
            return new OcspResult
            {
                Status = OcspCertificateStatus.Revoked,
                ProducedAt = basic.ProducedAt,
                RevocationTime = revoked.RevocationTime,
                RevocationReason = revoked.HasRevocationReason ? revoked.RevocationReason : null,
            };
        }

        _logger.LogWarning("OCSP: certificate status unknown to the responder.");
        return new OcspResult
        {
            Status = OcspCertificateStatus.Unknown,
            ProducedAt = basic.ProducedAt,
        };
    }
}
