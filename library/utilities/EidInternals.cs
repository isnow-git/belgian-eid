using BelgianEid.Models;

namespace BelgianEid.Utilities;

/// <summary>
/// Labels (CKA_LABEL) exposed by the PKCS#11 module of the Belgian eID middleware
/// for certificates and private keys.
/// </summary>
internal static class EidObjectLabels
{
    public const string AuthenticationCertificate = "Authentication";
    public const string SignatureCertificate = "Signature";
    public const string IntermediateCaCertificate = "CA";
    public const string RootCertificate = "Root";

    public const string AuthenticationPrivateKey = "Authentication";
    public const string SignaturePrivateKey = "Signature";

    /// <summary>Returns the label of the certificate corresponding to the requested type.</summary>
    public static string ForCertificate(EidCertificateKind kind) => kind switch
    {
        EidCertificateKind.Authentication => AuthenticationCertificate,
        EidCertificateKind.Signature => SignatureCertificate,
        EidCertificateKind.IntermediateCa => IntermediateCaCertificate,
        EidCertificateKind.Root => RootCertificate,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Returns the label of the private key corresponding to the requested type.</summary>
    public static string ForPrivateKey(EidPrivateKeyKind kind) => kind switch
    {
        EidPrivateKeyKind.Authentication => AuthenticationPrivateKey,
        EidPrivateKeyKind.Signature => SignaturePrivateKey,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

/// <summary>
/// TLV tags for the identity file (EF 4031) on the eID card.
/// </summary>
internal static class IdentityTags
{
    public const int CardNumber = 1;
    public const int ChipNumber = 2;
    public const int ValidityBegin = 3;
    public const int ValidityEnd = 4;
    public const int IssuingMunicipality = 5;
    public const int NationalNumber = 6;
    public const int LastName = 7;
    public const int FirstNames = 8;
    public const int ThirdNameInitial = 9;
    public const int Nationality = 10;
    public const int BirthLocation = 11;
    public const int BirthDate = 12;
    public const int Gender = 13;
    public const int PhotoHash = 17;
}

/// <summary>
/// TLV tags for the address file (EF 4033) on the eID card.
/// </summary>
internal static class AddressTags
{
    public const int Street = 1;
    public const int ZipCode = 2;
    public const int Municipality = 3;
}

/// <summary>
/// ASN.1 "DigestInfo" prefixes to prepend to a hash for an RSA PKCS#1 v1.5
/// signature performed via the raw CKM_RSA_PKCS mechanism.
/// </summary>
internal static class DigestInfoPrefixes
{
    public static readonly byte[] Sha1 =
    [
        0x30, 0x21, 0x30, 0x09, 0x06, 0x05, 0x2b, 0x0e, 0x03, 0x02,
        0x1a, 0x05, 0x00, 0x04, 0x14,
    ];

    public static readonly byte[] Sha256 =
    [
        0x30, 0x31, 0x30, 0x0d, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01,
        0x65, 0x03, 0x04, 0x02, 0x01, 0x05, 0x00, 0x04, 0x20,
    ];

    public static readonly byte[] Sha384 =
    [
        0x30, 0x41, 0x30, 0x0d, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01,
        0x65, 0x03, 0x04, 0x02, 0x02, 0x05, 0x00, 0x04, 0x30,
    ];

    public static readonly byte[] Sha512 =
    [
        0x30, 0x51, 0x30, 0x0d, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01,
        0x65, 0x03, 0x04, 0x02, 0x03, 0x05, 0x00, 0x04, 0x40,
    ];

    /// <summary>Returns the DigestInfo prefix associated with the given hash algorithm.</summary>
    public static byte[] For(EidHashAlgorithm algorithm) => algorithm switch
    {
        EidHashAlgorithm.Sha1 => Sha1,
        EidHashAlgorithm.Sha256 => Sha256,
        EidHashAlgorithm.Sha384 => Sha384,
        EidHashAlgorithm.Sha512 => Sha512,
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
    };
}
