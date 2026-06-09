using BelgianEid.Abstractions;
using BelgianEid.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BelgianEid.Implementations;

/// <summary>
/// High-level facade grouping all library services.
/// This is the recommended entry point for an external developer: they do not
/// need to know the underlying specialised services.
/// </summary>
public sealed class EidClient : IEidClient
{
    private readonly IEidReaderService _readerService;
    private readonly IEidIdentityService _identityService;
    private readonly IEidCertificateService _certificateService;
    private readonly IEidSignatureService _signatureService;
    private readonly IEidAuthenticationService _authenticationService;
    private readonly IOcspValidationService _ocspService;
    private readonly ILogger<EidClient> _logger;

    public EidClient(
        IEidReaderService readerService,
        IEidIdentityService identityService,
        IEidCertificateService certificateService,
        IEidSignatureService signatureService,
        IEidAuthenticationService authenticationService,
        IOcspValidationService ocspService,
        ILogger<EidClient>? logger = null)
    {
        _readerService = readerService;
        _identityService = identityService;
        _certificateService = certificateService;
        _signatureService = signatureService;
        _authenticationService = authenticationService;
        _ocspService = ocspService;
        _logger = logger ?? NullLogger<EidClient>.Instance;
    }

    /// <inheritdoc/>
    public IReadOnlyList<EidReader> GetReaders()
        => _readerService.GetAvailableReaders();

    /// <inheritdoc/>
    public IEidSession OpenSession(EidReader? reader = null)
        => _readerService.OpenSession(reader);

    /// <inheritdoc/>
    public EidCardData ReadCardData(IEidSession session, bool includePhoto = true)
    {
        ArgumentNullException.ThrowIfNull(session);
        _logger.LogInformation("Reading all data from the eID card.");

        return new EidCardData
        {
            Identity = _identityService.ReadIdentity(session),
            Address = _identityService.ReadAddress(session),
            Photo = includePhoto ? _identityService.ReadPhoto(session) : null,
            Certificates = _certificateService.ReadCertificates(session),
        };
    }

    /// <inheritdoc/>
    public Task<SignatureResult> SignAsync(
        IEidSession session, byte[] data, string pin,
        CancellationToken cancellationToken = default)
        => _signatureService.SignDataAsync(
            session, data, pin, EidHashAlgorithm.Sha256, cancellationToken);

    /// <inheritdoc/>
    public Task<AuthenticationResult> AuthenticateAsync(
        IEidSession session, byte[] challenge, string pin,
        CancellationToken cancellationToken = default)
        => _authenticationService.SignChallengeAsync(
            session, challenge, pin, EidHashAlgorithm.Sha256, cancellationToken);

    /// <inheritdoc/>
    public Task<OcspResult> ValidateCertificateAsync(
        IEidSession session, EidCertificateKind kind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        EidCertificateSet set = _certificateService.ReadCertificates(session);
        return _ocspService.CheckAsync(set.Get(kind), set.Get(EidCertificateKind.IntermediateCa), cancellationToken);
    }
}
