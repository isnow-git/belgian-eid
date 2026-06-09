using BelgianEid.Abstractions;
using BelgianEid.Models;
using BelgianEid.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BelgianEid.Implementations;

/// <summary>
/// Signs data or hashes with the card's signature private key.
/// </summary>
public sealed class EidSignatureService(
    IEidCertificateService certificateService,
    ILogger<EidSignatureService>? logger = null) : IEidSignatureService
{
    private readonly IEidCertificateService _certificateService = certificateService;
    private readonly ILogger<EidSignatureService> _logger = logger ?? NullLogger<EidSignatureService>.Instance;


    /// <inheritdoc/>
    public Task<SignatureResult> SignDataAsync(
        IEidSession session, byte[] data, string pin,
        EidHashAlgorithm hashAlgorithm = EidHashAlgorithm.Sha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(data);

        byte[] hash = SigningUtils.ComputeHash(data, hashAlgorithm);
        return SignCoreAsync(session, data, hash, hashAlgorithm, pin, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SignatureResult> SignHashAsync(
        IEidSession session, byte[] hash, string pin, EidHashAlgorithm hashAlgorithm,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(hash);
        SigningUtils.EnsureHashLength(hash, hashAlgorithm);

        return SignCoreAsync(session, hash, hash, hashAlgorithm, pin, cancellationToken);
    }

    private async Task<SignatureResult> SignCoreAsync(
        IEidSession session, byte[] signedData, byte[] hash,
        EidHashAlgorithm hashAlgorithm, string pin, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!session.IsAuthenticated)
                session.Login(pin);

            byte[] digestInfo = SigningUtils.BuildDigestInfo(hash, hashAlgorithm);
            byte[] signature  = session.Sign(
                EidPrivateKeyKind.Signature, digestInfo, EidSignatureMechanism.RsaPkcs1);

            _logger.LogInformation(
                "Signature ({Algo}) produced: {Bytes} bytes.", hashAlgorithm, signature.Length);

            return new SignatureResult
            {
                SignedData        = signedData,
                Signature         = signature,
                HashAlgorithm     = hashAlgorithm,
                SignerCertificate = _certificateService.ReadCertificate(session, EidCertificateKind.Signature),
            };
        }, cancellationToken).ConfigureAwait(false);
    }
}
