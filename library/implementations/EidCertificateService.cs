using System.Security.Cryptography.X509Certificates;
using BelgianEid.Abstractions;
using BelgianEid.Exceptions;
using BelgianEid.Models;

namespace BelgianEid.Implementations;

/// <summary>
/// Reads X.509 certificates from the eID card.
/// </summary>
public sealed class EidCertificateService : IEidCertificateService
{
    /// <inheritdoc/>
    public EidCertificateSet ReadCertificates(IEidSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new EidCertificateSet
        {
            Authentication = ReadCertificate(session, EidCertificateKind.Authentication),
            Signature = ReadCertificate(session, EidCertificateKind.Signature),
            IntermediateCa = ReadCertificate(session, EidCertificateKind.IntermediateCa),
            Root = ReadCertificate(session, EidCertificateKind.Root),
        };
    }

    /// <inheritdoc/>
    public X509Certificate2 ReadCertificate(IEidSession session, EidCertificateKind kind)
    {
        ArgumentNullException.ThrowIfNull(session);

        byte[] raw = session.GetCertificateRaw(kind);
        try
        {
            // .NET 8: the X509Certificate2(byte[]) constructor decodes DER.
            // (X509CertificateLoader only exists from .NET 9 onwards.)
            return new X509Certificate2(raw);
        }
        catch (Exception ex)
        {
            throw new EidCertificateException(
                $"The certificate '{kind}' could not be decoded (invalid X.509 data).", ex);
        }
    }
}
