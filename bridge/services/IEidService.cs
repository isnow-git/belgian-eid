using BelgianEid.Models;

namespace BelgianEid.Bridge.Services;

/// <summary>
/// Exposes all operations provided by the BelgianEid library as a single, bridge-friendly contract.
/// Each method manages its own PC/SC session internally (open → operate → dispose).
/// Handlers depend on this interface, never on concrete library types (Dependency Inversion Principle).
/// </summary>
public interface IEidService
{
    // ── Reader detection ──────────────────────────────────────────────────────

    /// <summary>Returns the list of all PC/SC card readers detected on the machine.</summary>
    IReadOnlyList<EidReader> GetReaders();

    /// <summary>Reports whether at least one reader is connected and whether a card is inserted.</summary>
    (bool ReaderPresent, bool CardPresent) GetStatus();

    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Reads identity fields from the chip (name, NRN, birth date, gender, …).</summary>
    EidIdentity ReadIdentity();

    /// <summary>Reads the cardholder's address (street, zip code, municipality).</summary>
    EidAddress ReadAddress();

    /// <summary>
    /// Reads the cardholder's photo. Returns <c>null</c> if no photo is present on the card.
    /// </summary>
    byte[]? ReadPhoto();

    /// <summary>
    /// Reads all card data at once: identity, address, and optionally the photo.
    /// </summary>
    /// <param name="includePhoto">When <c>true</c>, also reads the JPEG photo (slower).</param>
    EidCardData ReadCard(bool includePhoto = false);

    // ── Certificates ──────────────────────────────────────────────────────────

    /// <summary>Reads all four X.509 certificates present on the card.</summary>
    EidCertificateSet ReadCertificates();

    /// <summary>Reads a single X.509 certificate by kind, returned as raw DER bytes.</summary>
    byte[] ReadCertificate(EidCertificateKind kind);

    // ── PIN ───────────────────────────────────────────────────────────────────

    /// <summary>Returns the current PIN status (remaining attempts, blocked flag).</summary>
    EidPinStatus GetPinStatus();

    /// <summary>
    /// Verifies the PIN against the card.
    /// Throws <see cref="BelgianEid.Exceptions.EidPinIncorrectException"/> on failure and
    /// <see cref="BelgianEid.Exceptions.EidPinBlockedException"/> when the card is blocked.
    /// </summary>
    void VerifyPin(string pin);

    // ── Signing ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Signs a server-issued challenge with the card's <b>authentication</b> key.
    /// The library hashes <paramref name="challenge"/> internally before signing.
    /// </summary>
    Task<AuthenticationResult> SignChallengeAsync(
        byte[] challenge, string pin,
        EidHashAlgorithm algorithm = EidHashAlgorithm.Sha256,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs raw data with the card's <b>non-repudiation</b> (signature) key.
    /// The library computes the hash of <paramref name="data"/> internally.
    /// Suitable only for small payloads — send a pre-computed hash for large documents.
    /// </summary>
    Task<SignatureResult> SignDataAsync(
        byte[] data, string pin,
        EidHashAlgorithm algorithm = EidHashAlgorithm.Sha256,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs a pre-computed hash with the card's <b>non-repudiation</b> key.
    /// Use this for large documents: compute the SHA-256 hash client-side and pass it here.
    /// </summary>
    Task<SignatureResult> SignHashAsync(
        byte[] hash, string pin,
        EidHashAlgorithm algorithm = EidHashAlgorithm.Sha256,
        CancellationToken cancellationToken = default);

    // ── Certificate validation (OCSP) ─────────────────────────────────────────

    /// <summary>
    /// Queries the Belgian OCSP responder to check whether the given certificate
    /// has been revoked.
    /// </summary>
    Task<OcspResult> ValidateCertificateAsync(
        EidCertificateKind kind,
        CancellationToken cancellationToken = default);
}
