namespace BelgianEid.Models;

/// <summary>Gender as shown on the eID card.</summary>
public enum EidGender
{
    /// <summary>Unknown or not specified.</summary>
    Unknown = 0,
    /// <summary>Male.</summary>
    Male = 1,
    /// <summary>Female.</summary>
    Female = 2,
}

/// <summary>
/// Identifies one of the four X.509 certificates present on the eID card.
/// </summary>
public enum EidCertificateKind
{
    /// <summary>Citizen authentication certificate.</summary>
    Authentication = 0,
    /// <summary>Citizen signature certificate (qualified signature).</summary>
    Signature = 1,
    /// <summary>Intermediate authority certificate (Citizen CA).</summary>
    IntermediateCa = 2,
    /// <summary>Root certificate (Belgium Root CA).</summary>
    Root = 3,
}

/// <summary>
/// Identifies one of the two private keys usable on the eID card.
/// </summary>
public enum EidPrivateKeyKind
{
    /// <summary>Authentication private key (challenge / login).</summary>
    Authentication = 0,
    /// <summary>Signature private key (document signing).</summary>
    Signature = 1,
}

/// <summary>
/// Signature mechanism applied by the card.
/// </summary>
public enum EidSignatureMechanism
{
    Auto = 0,
    // ECDSA mechanisms
    EcdsaSha256,
    EcdsaSha384,
    EcdsaSha512,
    Ecdsa,
    // RSA mechanisms (for compatibility with older cards)
    RsaPkcs1,
    Sha256RsaPkcs1,
    Sha1RsaPkcs1
}

/// <summary>
/// Hash algorithm used during a signature operation.
/// </summary>
public enum EidHashAlgorithm
{
    /// <summary>SHA-256 (recommended).</summary>
    Sha256 = 0,
    /// <summary>SHA-384.</summary>
    Sha384 = 1,
    /// <summary>SHA-512.</summary>
    Sha512 = 2,
    /// <summary>SHA-1 (obsolete, provided for compatibility).</summary>
    Sha1 = 3,
}

/// <summary>
/// Type of event emitted by the reader monitor.
/// </summary>
public enum EidReaderEventKind
{
    /// <summary>A new reader was connected.</summary>
    ReaderConnected = 0,
    /// <summary>A reader was disconnected.</summary>
    ReaderDisconnected = 1,
    /// <summary>An eID card was inserted in a reader.</summary>
    CardInserted = 2,
    /// <summary>An eID card was removed from a reader.</summary>
    CardRemoved = 3,
}

/// <summary>
/// Revocation status of a certificate as returned by an OCSP responder.
/// </summary>
public enum OcspCertificateStatus
{
    /// <summary>The certificate is valid (not revoked).</summary>
    Good = 0,
    /// <summary>The certificate has been revoked.</summary>
    Revoked = 1,
    /// <summary>The responder does not know the status of the certificate.</summary>
    Unknown = 2,
}
