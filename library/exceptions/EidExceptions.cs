namespace BelgianEid.Exceptions;

/// <summary>
/// Base exception for the Belgian eID library.
/// All specific exceptions inherit from this one.
/// </summary>
public class EidException : Exception
{
    public EidException(string message) : base(message) { }
    public EidException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a configuration parameter is invalid or when the native
/// PKCS#11 library cannot be found or loaded.
/// </summary>
public sealed class EidConfigurationException : EidException
{
    public EidConfigurationException(string message) : base(message) { }
    public EidConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when no smart card reader is available,
/// or the requested reader does not exist.
/// </summary>
public sealed class EidReaderNotFoundException : EidException
{
    public EidReaderNotFoundException()
        : base("No smart card reader was detected.") { }

    public EidReaderNotFoundException(string message) : base(message) { }
}

/// <summary>
/// Thrown when no eID card is inserted in the target reader.
/// </summary>
public sealed class EidCardNotPresentException : EidException
{
    public EidCardNotPresentException()
        : base("No eID card is present in the reader.") { }

    public EidCardNotPresentException(string message) : base(message) { }
}

/// <summary>
/// Thrown when communication with the card or the OCSP responder fails.
/// </summary>
public sealed class EidCommunicationException : EidException
{
    public EidCommunicationException(string message) : base(message) { }
    public EidCommunicationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Base exception related to PIN code verification.
/// </summary>
public abstract class EidPinException : EidException
{
    protected EidPinException(string message) : base(message) { }
}

/// <summary>
/// Thrown when the entered PIN code is incorrect.
/// </summary>
public sealed class EidPinIncorrectException : EidPinException
{
    /// <summary>Number of remaining attempts before the card is blocked.</summary>
    public int RemainingAttempts { get; }

    public EidPinIncorrectException(int remainingAttempts)
        : base($"Incorrect PIN code. Remaining attempts: {remainingAttempts}.")
    {
        RemainingAttempts = remainingAttempts;
    }
}

/// <summary>
/// Thrown when the PIN code is blocked (3 attempts exhausted).
/// </summary>
public sealed class EidPinBlockedException : EidPinException
{
    public EidPinBlockedException()
        : base("The PIN code is blocked. The card must be unblocked at the municipal administration.") { }
}

/// <summary>
/// Base exception related to certificates.
/// </summary>
public class EidCertificateException : EidException
{
    public EidCertificateException(string message) : base(message) { }
    public EidCertificateException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a certificate has been revoked (OCSP finding).
/// </summary>
public sealed class EidCertificateRevokedException : EidCertificateException
{
    /// <summary>Date of certificate revocation, if known.</summary>
    public DateTimeOffset? RevocationTime { get; }

    public EidCertificateRevokedException(DateTimeOffset? revocationTime)
        : base(revocationTime is { } t
            ? $"The certificate was revoked on {t:u}."
            : "The certificate has been revoked.")
    {
        RevocationTime = revocationTime;
    }
}

/// <summary>
/// Thrown when an expected piece of data is absent from the card
/// (data object or certificate not found).
/// </summary>
public sealed class EidDataNotFoundException : EidException
{
    public EidDataNotFoundException(string message) : base(message) { }
}
