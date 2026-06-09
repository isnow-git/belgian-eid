using System.Security.Cryptography.X509Certificates;
using BelgianEid.Models;

namespace BelgianEid.Abstractions;

/// <summary>
/// Service for reading personal data from the eID card.
/// </summary>
public interface IEidIdentityService
{
    /// <summary>Reads and decodes the identity data.</summary>
    EidIdentity ReadIdentity(IEidSession session);

    /// <summary>Reads and decodes the residence address.</summary>
    EidAddress ReadAddress(IEidSession session);

    /// <summary>Reads the identity photo (may return <c>null</c> if absent).</summary>
    EidPhoto? ReadPhoto(IEidSession session);
}

/// <summary>
/// Service for reading X.509 certificates from the eID card.
/// </summary>
public interface IEidCertificateService
{
    /// <summary>Reads the complete set of four certificates from the card.</summary>
    EidCertificateSet ReadCertificates(IEidSession session);

    /// <summary>Reads a specific certificate from the card.</summary>
    X509Certificate2 ReadCertificate(IEidSession session, EidCertificateKind kind);
}

/// <summary>
/// Service for PIN code verification.
/// </summary>
public interface IEidPinService
{
    /// <summary>
    /// Verifies the cardholder's PIN code (maximum 3 attempts before the card is blocked).
    /// </summary>
    /// <exception cref="Exceptions.EidPinIncorrectException">Incorrect PIN.</exception>
    /// <exception cref="Exceptions.EidPinBlockedException">PIN blocked.</exception>
    void VerifyPin(IEidSession session, string pin);

    /// <summary>Returns the current PIN code status without attempting verification.</summary>
    EidPinStatus GetPinStatus(IEidSession session);
}

/// <summary>
/// Service for electronic signing with the card's signature key.
/// </summary>
public interface IEidSignatureService
{
    /// <summary>
    /// Signs arbitrary data: the library computes the hash then has the card sign it.
    /// </summary>
    Task<SignatureResult> SignDataAsync(
        IEidSession session, byte[] data, string pin,
        EidHashAlgorithm hashAlgorithm = EidHashAlgorithm.Sha256,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs a hash already computed by the caller.
    /// </summary>
    Task<SignatureResult> SignHashAsync(
        IEidSession session, byte[] hash, string pin, EidHashAlgorithm hashAlgorithm,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for challenge-based authentication using the card's authentication key.
/// </summary>
public interface IEidAuthenticationService
{
    /// <summary>
    /// Signs a challenge provided by a server with the private authentication key.
    /// </summary>
    Task<AuthenticationResult> SignChallengeAsync(
        IEidSession session, byte[] challenge, string pin,
        EidHashAlgorithm hashAlgorithm = EidHashAlgorithm.Sha256,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for online (OCSP) validation of certificate revocation.
/// </summary>
public interface IOcspValidationService
{
    /// <summary>
    /// Queries the official Belgian OCSP responder to determine the status of a certificate.
    /// </summary>
    /// <exception cref="Exceptions.EidCommunicationException">Communication with the responder failed.</exception>
    Task<OcspResult> CheckAsync(
        X509Certificate2 certificate, X509Certificate2 issuerCertificate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Like <see cref="CheckAsync"/>, but throws an exception if the certificate is revoked.
    /// </summary>
    /// <exception cref="Exceptions.EidCertificateRevokedException">The certificate is revoked.</exception>
    Task EnsureNotRevokedAsync(
        X509Certificate2 certificate, X509Certificate2 issuerCertificate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for checking certificate revocation against a CRL
/// (Certificate Revocation List). Used as a fallback when OCSP is unavailable.
/// </summary>
public interface ICrlValidationService
{
    /// <summary>
    /// Returns <c>true</c> if the certificate appears in the configured CRL.
    /// </summary>
    Task<bool> IsRevokedAsync(X509Certificate2 certificate, CancellationToken cancellationToken = default);
}

/// <summary>
/// High-level facade grouping all common operations on the eID card.
/// Recommended entry point for an external developer.
/// </summary>
public interface IEidClient
{
    /// <summary>Enumerates the available readers.</summary>
    IReadOnlyList<EidReader> GetReaders();

    /// <summary>Opens a session to an eID card.</summary>
    IEidSession OpenSession(EidReader? reader = null);

    /// <summary>
    /// Reads all readable data from a card (identity, address, photo, certificates).
    /// </summary>
    EidCardData ReadCardData(IEidSession session, bool includePhoto = true);

    /// <summary>Signs arbitrary data with the signature key.</summary>
    Task<SignatureResult> SignAsync(
        IEidSession session, byte[] data, string pin,
        CancellationToken cancellationToken = default);

    /// <summary>Signs a challenge with the authentication key.</summary>
    Task<AuthenticationResult> AuthenticateAsync(
        IEidSession session, byte[] challenge, string pin,
        CancellationToken cancellationToken = default);

    /// <summary>Verifies the revocation of a card certificate via OCSP.</summary>
    Task<OcspResult> ValidateCertificateAsync(
        IEidSession session, EidCertificateKind kind,
        CancellationToken cancellationToken = default);
}
