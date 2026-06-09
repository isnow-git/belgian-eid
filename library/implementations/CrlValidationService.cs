using System.Security.Cryptography.X509Certificates;
using BelgianEid.Abstractions;
using BelgianEid.Configuration;
using BelgianEid.Exceptions;
using BelgianEid.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Security;
using BcX509 = Org.BouncyCastle.X509.X509Certificate;

namespace BelgianEid.Implementations;

/// <summary>
/// Verifies certificate revocation via CRL (Certificate Revocation Lists).
/// Official URL: http://crl.eid.belgium.be/eidc200508.crl (page 25 of the PDF)
/// </summary>
public sealed class CrlValidationService : ICrlValidationService
{
    private readonly HttpClient _httpClient;
    private readonly BelgianEidOptions _options;
    private readonly ILogger<CrlValidationService> _logger;

    public CrlValidationService(
        HttpClient httpClient,
        IOptions<BelgianEidOptions> options,
        ILogger<CrlValidationService>? logger = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger ?? NullLogger<CrlValidationService>.Instance;
    }

    public async Task<bool> IsRevokedAsync(
        X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        string crlUrl = string.IsNullOrEmpty(_options.CrlUrl)
            ? "http://crl.eid.belgium.be/eidc200508.crl"
            : _options.CrlUrl;

        try
        {
            _logger.LogDebug("Downloading CRL from {Url}", crlUrl);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.OcspTimeout);

            byte[] crlData = await _httpClient.GetByteArrayAsync(crlUrl, cts.Token).ConfigureAwait(false);

            var crl = new X509Crl(crlData);

            // Convert the .NET certificate to a BouncyCastle certificate
            BcX509 bcCert = DotNetUtilities.FromX509Certificate(certificate);

            // Check whether the certificate is revoked (this overload expects an X509Certificate)
            bool isRevoked = crl.IsRevoked(bcCert);

            if (isRevoked)
            {
                _logger.LogWarning("Certificate revoked (CRL)");
            }
            else
            {
                _logger.LogDebug("Certificate not found in CRL — valid");
            }

            return isRevoked;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CRL verification failed");
            throw new EidCommunicationException($"CRL verification failed: {ex.Message}", ex);
        }
    }
}
