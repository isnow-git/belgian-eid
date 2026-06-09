using BelgianEid.Abstractions;
using BelgianEid.Models;
using BelgianEid.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;

namespace BelgianEid.Implementations;

/// <summary>
/// Performs challenge-based authentication with the card's authentication private key.
/// </summary>
public sealed class EidAuthenticationService : IEidAuthenticationService
{
    private readonly IEidCertificateService _certificateService;
    private readonly ILogger<EidAuthenticationService> _logger;

    // EC OID (1.2.840.10045.2.1) — identifies EC public keys in X.509 certificates.
    private const string EcPublicKeyOid = "1.2.840.10045.2.1";

    public EidAuthenticationService(
        IEidCertificateService certificateService,
        ILogger<EidAuthenticationService>? logger = null)
    {
        _certificateService = certificateService;
        _logger = logger ?? NullLogger<EidAuthenticationService>.Instance;
    }

    /// <inheritdoc/>
    public async Task<AuthenticationResult> SignChallengeAsync(
        IEidSession session, byte[] challenge, string pin,
        EidHashAlgorithm hashAlgorithm = EidHashAlgorithm.Sha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(challenge);
        if (challenge.Length == 0)
            throw new ArgumentException("The challenge cannot be empty.", nameof(challenge));

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!session.IsAuthenticated)
                session.Login(pin);

            // Read the authentication certificate to determine the key type (EC vs RSA).
            var authCert = _certificateService.ReadCertificate(session, EidCertificateKind.Authentication);
            bool isEc = string.Equals(
                authCert.PublicKey.Oid.Value, EcPublicKeyOid, StringComparison.Ordinal);

            byte[] signature;
            EidHashAlgorithm actualAlgorithm;

            if (isEc)
            {
                // Modern Belgian eID cards (CA3+, CA6): EC P-384.
                // Use SHA-384 + raw ECDSA (CKM_ECDSA) — the card signs the pre-computed hash.
                // Server-side verification: SHA384withECDSA.
                actualAlgorithm = EidHashAlgorithm.Sha384;
                byte[] hash = SHA384.HashData(challenge);
                signature = session.Sign(EidPrivateKeyKind.Authentication, hash, EidSignatureMechanism.Ecdsa);
                _logger.LogInformation("Challenge signed (EC P-384, SHA-384 + CKM_ECDSA).");
            }
            else
            {
                // Older Belgian eID cards: RSA 2048.
                // Pass pre-computed SHA-256 hash; CKM_SHA256_RSA_PKCS wraps in DigestInfo internally.
                // Server-side verification: SHA256withRSA.
                actualAlgorithm = EidHashAlgorithm.Sha256;
                byte[] hash = SHA256.HashData(challenge);
                signature = session.Sign(EidPrivateKeyKind.Authentication, hash, EidSignatureMechanism.Sha256RsaPkcs1);
                _logger.LogInformation("Challenge signed (RSA, SHA-256 + CKM_SHA256_RSA_PKCS).");
            }

            return new AuthenticationResult
            {
                Challenge                 = challenge,
                Signature                 = signature,
                HashAlgorithm             = actualAlgorithm,
                AuthenticationCertificate = authCert,
            };
        }, cancellationToken).ConfigureAwait(false);
    }
}
