using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Ocsp;
using Org.BouncyCastle.Security;
using BcX509 = Org.BouncyCastle.X509.X509Certificate;

namespace BelgianEid.Tests.Helpers;

/// <summary>
/// Factory for DER-encoded OCSP responses, to be injected into <see cref="FakeHttpMessageHandler"/>
/// to simulate different OCSP responder behaviours without any network call.
/// </summary>
internal static class OcspResponseFactory
{
    private static readonly SecureRandom Rng = new();

    // ── Responses by certificate status ───────────────────────────────────────

    /// <summary>OCSP response indicating the certificate is <b>valid</b> (Good).</summary>
    public static byte[] Good(CertificatePair ca, CertificatePair leaf)
        => BuildSuccessful(ca, leaf, certStatus: null);

    /// <summary>
    /// OCSP response indicating the certificate has been <b>revoked</b>.
    /// </summary>
    /// <param name="revokedAt">
    /// Revocation date inserted in the response (precision to the second, UTC).
    /// </param>
    public static byte[] Revoked(CertificatePair ca, CertificatePair leaf, DateTimeOffset revokedAt)
        => BuildSuccessful(ca, leaf,
            certStatus: new RevokedStatus(revokedAt.UtcDateTime, reason: 0));

    /// <summary>
    /// OCSP response indicating the responder <b>does not know</b> the certificate.
    /// </summary>
    public static byte[] Unknown(CertificatePair ca, CertificatePair leaf)
        => BuildSuccessful(ca, leaf, certStatus: new UnknownStatus());

    /// <summary>
    /// OCSP response containing only an error code (non-Successful status),
    /// without a response block — simulates a responder returning e.g. MalformedRequest.
    /// </summary>
    /// <param name="ocspRespStatus">
    /// Numeric status code (e.g. <c>1</c> = MalformedRequest).
    /// Use the <c>OcspRespStatus.*</c> constants from BouncyCastle.
    /// </param>
    public static byte[] Error(int ocspRespStatus)
    {
        // Minimal DER encoding of an OcspResponse without ResponseBytes:
        //   OcspResponse ::= SEQUENCE { responseStatus ENUMERATED { value } }
        // Bytes: 30 03 0A 01 <status>
        return new byte[] { 0x30, 0x03, 0x0A, 0x01, (byte)ocspRespStatus };
    }

    // ── Private ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <c>BasicOcspResp</c> signed by the CA, then wraps it
    /// in an <c>OcspResp</c> with Successful status.
    /// </summary>
    /// <param name="certStatus">
    /// Certificate status:
    /// <c>null</c> = Good, <see cref="RevokedStatus"/> = Revoked, <see cref="UnknownStatus"/> = Unknown.
    /// </param>
    private static byte[] BuildSuccessful(CertificatePair ca, CertificatePair leaf, CertificateStatus? certStatus)
    {
        // The CertID must use SHA-256, the same algorithm as OcspValidationService.BuildRequest().
        var certId = new CertificateID(
            NistObjectIdentifiers.IdSha256.Id,
            ca.BouncyCastle,
            leaf.BouncyCastle.SerialNumber);

        // Build the BasicOcspResp.
        var basicGen = new BasicOcspRespGenerator(new RespID(ca.BouncyCastle.SubjectDN));
        basicGen.AddResponse(certId, certStatus);

        var sigFactory = new Asn1SignatureFactory("SHA256WITHRSA", ca.KeyPair.Private, Rng);
        BasicOcspResp basicResp = basicGen.Generate(
            sigFactory,
            chain: new BcX509[] { ca.BouncyCastle },
            producedAt: DateTime.UtcNow);

        // Wrap in OcspResp with Successful status.
        // Note: the class is named OCSPRespGenerator (OCSP in uppercase) in BouncyCastle 2.x.
        return new OCSPRespGenerator()
            .Generate(OCSPRespGenerator.Successful, basicResp)
            .GetEncoded();
    }
}
