using System.Security.Cryptography.X509Certificates;

namespace BelgianEid.Models;

/// <summary>
/// Groups the four X.509 certificates present on the eID card.
/// </summary>
public sealed class EidCertificateSet
{
    /// <summary>Citizen authentication certificate.</summary>
    public required X509Certificate2 Authentication { get; init; }

    /// <summary>Citizen signature certificate (qualified signature).</summary>
    public required X509Certificate2 Signature { get; init; }

    /// <summary>Intermediate authority certificate (Citizen CA).</summary>
    public required X509Certificate2 IntermediateCa { get; init; }

    /// <summary>Root certificate (Belgium Root CA).</summary>
    public required X509Certificate2 Root { get; init; }

    /// <summary>
    /// Returns the certificate corresponding to the requested type.
    /// </summary>
    public X509Certificate2 Get(EidCertificateKind kind) => kind switch
    {
        EidCertificateKind.Authentication => Authentication,
        EidCertificateKind.Signature => Signature,
        EidCertificateKind.IntermediateCa => IntermediateCa,
        EidCertificateKind.Root => Root,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// Returns the issuer certificate of the specified citizen certificate.
    /// Both authentication and signature are issued by the Citizen CA.
    /// </summary>
    public X509Certificate2 GetIssuerOf(EidCertificateKind kind) => kind switch
    {
        EidCertificateKind.Authentication => IntermediateCa,
        EidCertificateKind.Signature => IntermediateCa,
        EidCertificateKind.IntermediateCa => Root,
        _ => throw new ArgumentOutOfRangeException(nameof(kind),
            "The root certificate has no issuer in the card's certificate chain."),
    };
}
