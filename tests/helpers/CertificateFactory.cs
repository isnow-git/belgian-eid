using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using BcX509 = Org.BouncyCastle.X509.X509Certificate;

namespace BelgianEid.Tests.Helpers;

/// <summary>
/// Groups an X.509 certificate in the three forms needed by tests:
/// the .NET format, the BouncyCastle format, and the asymmetric key pair.
/// </summary>
internal sealed record CertificatePair(
    X509Certificate2 DotNet,
    BcX509 BouncyCastle,
    AsymmetricCipherKeyPair KeyPair);

/// <summary>
/// Factory for self-signed or CA-signed X.509 certificates.
/// Uses 1024-bit RSA keys for fast execution (tests only).
/// </summary>
internal static class CertificateFactory
{
    private static readonly SecureRandom Rng = new();

    // ── Public factory ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a self-signed Certificate Authority (CA).
    /// </summary>
    public static CertificatePair CreateCa()
    {
        AsymmetricCipherKeyPair kp = GenerateRsaKeyPair();

        X509V3CertificateGenerator gen = new();
        gen.SetSerialNumber(BigInteger.Two);
        gen.SetIssuerDN(new X509Name("CN=Test Belgian CA, O=Test, C=BE"));
        gen.SetSubjectDN(new X509Name("CN=Test Belgian CA, O=Test, C=BE"));
        gen.SetNotBefore(DateTime.UtcNow.AddDays(-1));
        gen.SetNotAfter(DateTime.UtcNow.AddYears(5));
        gen.SetPublicKey(kp.Public);
        gen.AddExtension(X509Extensions.BasicConstraints, critical: true,
            new BasicConstraints(cA: true));

        BcX509 bc = gen.Generate(new Asn1SignatureFactory("SHA256WITHRSA", kp.Private, Rng));
        return new CertificatePair(ToDotNet(bc), bc, kp);
    }

    /// <summary>
    /// Creates a leaf certificate signed by <paramref name="ca"/>.
    /// </summary>
    /// <param name="ca">Issuing CA.</param>
    /// <param name="ocspUrl">
    /// URL inserted in the AIA extension (id-ad-ocsp).
    /// If <c>null</c>, the AIA extension is not added to the certificate.
    /// </param>
    public static CertificatePair CreateLeaf(CertificatePair ca, string? ocspUrl = null)
    {
        AsymmetricCipherKeyPair kp = GenerateRsaKeyPair();

        X509V3CertificateGenerator gen = new();
        gen.SetSerialNumber(BigInteger.ValueOf(42L));
        gen.SetIssuerDN(ca.BouncyCastle.SubjectDN);
        gen.SetSubjectDN(new X509Name("CN=Test Citizen, O=Test, C=BE"));
        gen.SetNotBefore(DateTime.UtcNow.AddDays(-1));
        gen.SetNotAfter(DateTime.UtcNow.AddYears(1));
        gen.SetPublicKey(kp.Public);
        gen.AddExtension(X509Extensions.BasicConstraints, critical: false,
            new BasicConstraints(cA: false));

        if (ocspUrl is not null)
        {
            // AIA extension: points the OCSP responder to ocspUrl.
            // .NET 8 reads it via X509AuthorityInformationAccessExtension.EnumerateOcspUris().
            gen.AddExtension(
                X509Extensions.AuthorityInfoAccess,
                critical: false,
                new AuthorityInformationAccess(new AccessDescription(
                    AccessDescription.IdADOcsp,
                    new GeneralName(GeneralName.UniformResourceIdentifier, ocspUrl))));
        }

        BcX509 bc = gen.Generate(new Asn1SignatureFactory("SHA256WITHRSA", ca.KeyPair.Private, Rng));
        return new CertificatePair(ToDotNet(bc), bc, kp);
    }

    // ── Private ────────────────────────────────────────────────────────────────

    /// <summary>Generates a 1024-bit RSA key pair.</summary>
    private static AsymmetricCipherKeyPair GenerateRsaKeyPair()
    {
        var gen = new RsaKeyPairGenerator();
        gen.Init(new RsaKeyGenerationParameters(
            BigInteger.ValueOf(65537L),
            Rng,
            strength: 1024,
            certainty: 80));
        return gen.GenerateKeyPair();
    }

    /// <summary>Converts a BouncyCastle certificate to a .NET <see cref="X509Certificate2"/>.</summary>
    private static X509Certificate2 ToDotNet(BcX509 cert)
        => new(cert.GetEncoded());
}
