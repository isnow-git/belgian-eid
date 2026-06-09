using BelgianEid.Abstractions;
using BelgianEid.Exceptions;
using BelgianEid.Models;

namespace BelgianEid.Bridge.Services;

/// <summary>
/// Implements <see cref="IEidService"/> by delegating to the BelgianEid library services.
/// Each public method opens its own PC/SC session and disposes it after the operation,
/// preventing long-held card handles from blocking other applications.
/// </summary>
public sealed class EidService : IEidService
{
    private readonly IEidClient _client;
    private readonly IEidIdentityService _identityService;
    private readonly IEidCertificateService _certService;
    private readonly IEidPinService _pinService;
    private readonly IEidSignatureService _sigService;
    private readonly IEidAuthenticationService _authService;

    public EidService(
        IEidClient client,
        IEidIdentityService identityService,
        IEidCertificateService certService,
        IEidPinService pinService,
        IEidSignatureService sigService,
        IEidAuthenticationService authService)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        _certService = certService ?? throw new ArgumentNullException(nameof(certService));
        _pinService = pinService ?? throw new ArgumentNullException(nameof(pinService));
        _sigService = sigService ?? throw new ArgumentNullException(nameof(sigService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    // ── Reader detection ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public IReadOnlyList<EidReader> GetReaders() =>
        _client.GetReaders();

    /// <inheritdoc/>
    public (bool ReaderPresent, bool CardPresent) GetStatus()
    {
        var readers = _client.GetReaders();
        bool present = readers.Count > 0;
        bool hasCard = present && readers.Any(r => r.HasCardInserted);
        return (present, hasCard);
    }

    // ── Identity ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public EidIdentity ReadIdentity()
    {
        using var session = _client.OpenSession();
        return _identityService.ReadIdentity(session);
    }

    /// <inheritdoc/>
    public EidAddress ReadAddress()
    {
        using var session = _client.OpenSession();
        return _identityService.ReadAddress(session);
    }

    /// <inheritdoc/>
    public byte[]? ReadPhoto()
    {
        using var session = _client.OpenSession();
        return _identityService.ReadPhoto(session)?.Data;
    }

    /// <inheritdoc/>
    public EidCardData ReadCard(bool includePhoto = false)
    {
        using var session = _client.OpenSession();
        return _client.ReadCardData(session, includePhoto);
    }

    // ── Certificates ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public EidCertificateSet ReadCertificates()
    {
        using var session = _client.OpenSession();
        return _certService.ReadCertificates(session);
    }

    /// <inheritdoc/>
    public byte[] ReadCertificate(EidCertificateKind kind)
    {
        using var session = _client.OpenSession();
        return _certService.ReadCertificate(session, kind).RawData;
    }

    // ── PIN ───────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public EidPinStatus GetPinStatus()
    {
        using var session = _client.OpenSession();
        return _pinService.GetPinStatus(session);
    }

    /// <inheritdoc/>
    public void VerifyPin(string pin)
    {
        using var session = _client.OpenSession();
        _pinService.VerifyPin(session, pin);
    }

    // ── Signing ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<AuthenticationResult> SignChallengeAsync(
        byte[] challenge, string pin,
        EidHashAlgorithm algorithm = EidHashAlgorithm.Sha256,
        CancellationToken cancellationToken = default)
    {
        using var session = _client.OpenSession();
        return await _authService.SignChallengeAsync(
            session, challenge, pin, algorithm, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SignatureResult> SignDataAsync(
        byte[] data, string pin,
        EidHashAlgorithm algorithm = EidHashAlgorithm.Sha256,
        CancellationToken cancellationToken = default)
    {
        using var session = _client.OpenSession();
        return await _sigService.SignDataAsync(
            session, data, pin, algorithm, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SignatureResult> SignHashAsync(
        byte[] hash, string pin,
        EidHashAlgorithm algorithm = EidHashAlgorithm.Sha256,
        CancellationToken cancellationToken = default)
    {
        using var session = _client.OpenSession();
        return await _sigService.SignHashAsync(
            session, hash, pin, algorithm, cancellationToken);
    }

    // ── Certificate validation (OCSP) ─────────────────────────────────────────

    /// <inheritdoc/>
    public Task<OcspResult> ValidateCertificateAsync(
        EidCertificateKind kind,
        CancellationToken cancellationToken = default)
    {
        // IEidClient.ValidateCertificateAsync handles the cert + issuer lookup internally.
        using var session = _client.OpenSession();
        return _client.ValidateCertificateAsync(session, kind, cancellationToken);
    }
}
