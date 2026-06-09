using System.Security.Cryptography.X509Certificates;

namespace BelgianEid.Models;

/// <summary>
/// Result of an electronic signature operation.
/// </summary>
public sealed class SignatureResult
{
    /// <summary>Data (or hash) that was signed.</summary>
    public required byte[] SignedData { get; init; }

    /// <summary>Signature produced by the card.</summary>
    public required byte[] Signature { get; init; }

    /// <summary>Hash algorithm used.</summary>
    public required EidHashAlgorithm HashAlgorithm { get; init; }

    /// <summary>Citizen signature certificate (holder of the public key).</summary>
    public required X509Certificate2 SignerCertificate { get; init; }

    /// <summary>UTC timestamp of the signature (client-side).</summary>
    public DateTimeOffset SignedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Result of a challenge-based authentication.
/// </summary>
public sealed class AuthenticationResult
{
    /// <summary>Challenge provided by the server.</summary>
    public required byte[] Challenge { get; init; }

    /// <summary>Signature of the challenge produced with the authentication key.</summary>
    public required byte[] Signature { get; init; }

    /// <summary>Hash algorithm used.</summary>
    public required EidHashAlgorithm HashAlgorithm { get; init; }

    /// <summary>Citizen authentication certificate (to be sent to the server).</summary>
    public required X509Certificate2 AuthenticationCertificate { get; init; }
}

/// <summary>
/// Result of an OCSP certificate revocation check.
/// </summary>
public sealed class OcspResult
{
    /// <summary>Revocation status reported by the responder.</summary>
    public required OcspCertificateStatus Status { get; init; }

    /// <summary>Date the OCSP response was produced.</summary>
    public DateTimeOffset? ProducedAt { get; init; }

    /// <summary>Revocation date (if <see cref="Status"/> equals <see cref="OcspCertificateStatus.Revoked"/>).</summary>
    public DateTimeOffset? RevocationTime { get; init; }

    /// <summary>Revocation reason code (if applicable).</summary>
    public int? RevocationReason { get; init; }

    /// <summary>Shortcut: whether the certificate is considered valid.</summary>
    public bool IsValid => Status == OcspCertificateStatus.Good;
}
