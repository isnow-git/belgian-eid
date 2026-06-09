using System.Net;
using System.Net.Http.Headers;
using BelgianEid.Configuration;
using BelgianEid.Exceptions;
using BelgianEid.Implementations;
using BelgianEid.Models;
using BelgianEid.Tests.Helpers;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Ocsp;
using Xunit;

namespace BelgianEid.Tests;

/// <summary>
/// Unit tests for <see cref="OcspValidationService"/>.
/// No real network calls are made: the <see cref="HttpClient"/> is replaced
/// by a <see cref="FakeHttpMessageHandler"/> that returns OCSP responses built
/// in memory using <see cref="OcspResponseFactory"/>.
/// </summary>
public sealed class OcspValidationServiceTests
{
    // ── Shared data ────────────────────────────────────────────────────────────
    // Certificates are generated once for the entire test class
    // (RSA 1024-bit generation ~80 ms per pair).

    /// <summary>Fictional URL present in the AIA extension of the leaf certificate.</summary>
    private const string AiaOcspUrl = "http://ocsp.aia.example.com/";

    /// <summary>Fallback URL configured in options (used when no AIA is present).</summary>
    private const string FallbackOcspUrl = "http://ocsp.fallback.example.com/";

    // Test CA + two leaf certificates
    private static readonly CertificatePair CaCert         = CertificateFactory.CreateCa();
    private static readonly CertificatePair LeafWithAia    = CertificateFactory.CreateLeaf(CaCert, AiaOcspUrl);
    private static readonly CertificatePair LeafWithoutAia = CertificateFactory.CreateLeaf(CaCert, ocspUrl: null);

    // ── Helper: service construction ───────────────────────────────────────────

    /// <summary>
    /// Instantiates an <see cref="OcspValidationService"/> with a stubbed <see cref="HttpClient"/>.
    /// All HTTP requests sent are captured in the returned list.
    /// </summary>
    /// <param name="respond">Delegate that produces the HTTP response from the received request.</param>
    private static (OcspValidationService Service, List<HttpRequestMessage> Requests)
        BuildService(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var requests = new List<HttpRequestMessage>();

        var handler = new FakeHttpMessageHandler(req =>
        {
            requests.Add(req);
            return respond(req);
        });

        var options = Options.Create(new BelgianEidOptions
        {
            OcspResponderUrl = new Uri(FallbackOcspUrl),
        });

        return (new OcspValidationService(new HttpClient(handler), options), requests);
    }

    /// <summary>Creates an HTTP 200 OK response wrapping OCSP bytes.</summary>
    private static HttpResponseMessage Http200(byte[] ocspBytes)
    {
        var content = new ByteArrayContent(ocspBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/ocsp-response");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 1. OCSP statuses
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CheckAsync_RetourneGood_QuandLeCertificatEstValide()
    {
        byte[] ocspResp = OcspResponseFactory.Good(CaCert, LeafWithAia);
        var (svc, _) = BuildService(_ => Http200(ocspResp));

        OcspResult result = await svc.CheckAsync(LeafWithAia.DotNet, CaCert.DotNet);

        Assert.Equal(OcspCertificateStatus.Good, result.Status);
        Assert.True(result.IsValid);
        Assert.Null(result.RevocationTime);
        Assert.NotNull(result.ProducedAt);   // the responder correctly timestamped the response
    }

    [Fact]
    public async Task CheckAsync_RetourneRevoked_QuandLeCertificatEstRevoque()
    {
        DateTimeOffset revokedAt = DateTimeOffset.UtcNow.AddDays(-7);
        byte[] ocspResp = OcspResponseFactory.Revoked(CaCert, LeafWithAia, revokedAt);
        var (svc, _) = BuildService(_ => Http200(ocspResp));

        OcspResult result = await svc.CheckAsync(LeafWithAia.DotNet, CaCert.DotNet);

        Assert.Equal(OcspCertificateStatus.Revoked, result.Status);
        Assert.False(result.IsValid);
        Assert.NotNull(result.RevocationTime);
        // The returned revocation date must match the one inserted (precision to the second).
        double deltaSeconds = Math.Abs((result.RevocationTime!.Value - revokedAt).TotalSeconds);
        Assert.True(deltaSeconds < 2,
            $"Revocation time delta too large: {deltaSeconds:F1} s (expected < 2 s).");
    }

    [Fact]
    public async Task CheckAsync_RetourneUnknown_QuandLeRepondeurNeConnaitPasLeCertificat()
    {
        byte[] ocspResp = OcspResponseFactory.Unknown(CaCert, LeafWithAia);
        var (svc, _) = BuildService(_ => Http200(ocspResp));

        OcspResult result = await svc.CheckAsync(LeafWithAia.DotNet, CaCert.DotNet);

        Assert.Equal(OcspCertificateStatus.Unknown, result.Status);
        Assert.False(result.IsValid);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2. EnsureNotRevokedAsync
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EnsureNotRevokedAsync_NeLevePasException_QuandLeCertificatEstValide()
    {
        byte[] ocspResp = OcspResponseFactory.Good(CaCert, LeafWithAia);
        var (svc, _) = BuildService(_ => Http200(ocspResp));

        // Must complete without exception.
        await svc.EnsureNotRevokedAsync(LeafWithAia.DotNet, CaCert.DotNet);
    }

    [Fact]
    public async Task EnsureNotRevokedAsync_LeveRevokedException_QuandLeCertificatEstRevoque()
    {
        DateTimeOffset revokedAt = DateTimeOffset.UtcNow.AddDays(-3);
        byte[] ocspResp = OcspResponseFactory.Revoked(CaCert, LeafWithAia, revokedAt);
        var (svc, _) = BuildService(_ => Http200(ocspResp));

        EidCertificateRevokedException ex =
            await Assert.ThrowsAsync<EidCertificateRevokedException>(
                () => svc.EnsureNotRevokedAsync(LeafWithAia.DotNet, CaCert.DotNet));

        Assert.NotNull(ex.RevocationTime);
        Assert.Contains("revoked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 3. Network and protocol error handling
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CheckAsync_LeveCommunicationException_QuandLeServeurRepond500()
    {
        var (svc, _) = BuildService(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<EidCommunicationException>(
            () => svc.CheckAsync(LeafWithAia.DotNet, CaCert.DotNet));
    }

    [Fact]
    public async Task CheckAsync_LeveCommunicationException_QuandLeStatutOcspEstUneErreur()
    {
        // The responder signals that the request is malformed (MalformedRequest = 1).
        // MalformedRequest = 1 (constant defined in OCSPRespGenerator)
        byte[] errorResp = OcspResponseFactory.Error(OCSPRespGenerator.MalformedRequest);
        var (svc, _) = BuildService(_ => Http200(errorResp));

        await Assert.ThrowsAsync<EidCommunicationException>(
            () => svc.CheckAsync(LeafWithAia.DotNet, CaCert.DotNet));
    }

    [Fact]
    public async Task CheckAsync_LeveCommunicationException_QuandLaReponseEstDesOctetsAleatoires()
    {
        // Random bytes: do not constitute a valid OCSP response.
        byte[] garbage = { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0xFF };
        var (svc, _) = BuildService(_ => Http200(garbage));

        await Assert.ThrowsAsync<EidCommunicationException>(
            () => svc.CheckAsync(LeafWithAia.DotNet, CaCert.DotNet));
    }

    [Fact]
    public async Task CheckAsync_LeveCommunicationException_EnCasDerreurReseau()
    {
        // Simulates a network failure.
        var (svc, _) = BuildService(_ => throw new HttpRequestException("Connection refused."));

        await Assert.ThrowsAsync<EidCommunicationException>(
            () => svc.CheckAsync(LeafWithAia.DotNet, CaCert.DotNet));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 4. OCSP URL selection (AIA vs fallback)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CheckAsync_UtiliseLUrlDeLAia_QuandLeCertificatAUneExtensionAia()
    {
        byte[] ocspResp = OcspResponseFactory.Good(CaCert, LeafWithAia);
        var (svc, requests) = BuildService(_ => Http200(ocspResp));

        await svc.CheckAsync(LeafWithAia.DotNet, CaCert.DotNet);

        Assert.Single(requests);
        Assert.Equal(AiaOcspUrl, requests[0].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task CheckAsync_UtiliseLUrlFallback_QuandLeCertificatNaPasDAia()
    {
        byte[] ocspResp = OcspResponseFactory.Good(CaCert, LeafWithoutAia);
        var (svc, requests) = BuildService(_ => Http200(ocspResp));

        await svc.CheckAsync(LeafWithoutAia.DotNet, CaCert.DotNet);

        Assert.Single(requests);
        Assert.Equal(FallbackOcspUrl, requests[0].RequestUri!.AbsoluteUri);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 5. HTTP / RFC 6960 compliance
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CheckAsync_EnvoieUneRequetePOST()
    {
        byte[] ocspResp = OcspResponseFactory.Good(CaCert, LeafWithAia);
        var (svc, requests) = BuildService(_ => Http200(ocspResp));

        await svc.CheckAsync(LeafWithAia.DotNet, CaCert.DotNet);

        Assert.Equal(HttpMethod.Post, requests[0].Method);
    }

    [Fact]
    public async Task CheckAsync_EnvoieLeContentType_ApplicationOcspRequest()
    {
        byte[] ocspResp = OcspResponseFactory.Good(CaCert, LeafWithAia);
        var (svc, requests) = BuildService(_ => Http200(ocspResp));

        await svc.CheckAsync(LeafWithAia.DotNet, CaCert.DotNet);

        Assert.Equal(
            "application/ocsp-request",
            requests[0].Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task CheckAsync_EnvoieLeHeaderAccept_ApplicationOcspResponse()
    {
        // RFC 6960 §4.1.2: the HTTP POST request must include Accept: application/ocsp-response.
        byte[] ocspResp = OcspResponseFactory.Good(CaCert, LeafWithAia);
        var (svc, requests) = BuildService(_ => Http200(ocspResp));

        await svc.CheckAsync(LeafWithAia.DotNet, CaCert.DotNet);

        Assert.Contains(
            requests[0].Headers.Accept,
            h => h.MediaType == "application/ocsp-response");
    }

    [Fact]
    public async Task CheckAsync_EnvoieUnCorpsNonVide()
    {
        byte[] ocspResp = OcspResponseFactory.Good(CaCert, LeafWithAia);
        byte[]? capturedBody = null;

        // The HttpRequestMessage is disposed after SendAsync: read the body
        // inside the handler, while the request is still alive.
        var (svc, _) = BuildService(req =>
        {
            capturedBody = req.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            return Http200(ocspResp);
        });

        await svc.CheckAsync(LeafWithAia.DotNet, CaCert.DotNet);

        Assert.NotNull(capturedBody);
        Assert.NotEmpty(capturedBody);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 6. Argument validation
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CheckAsync_LeveArgumentNullException_QuandCertificateEstNull()
    {
        var (svc, _) = BuildService(_ => Http200(Array.Empty<byte>()));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => svc.CheckAsync(null!, CaCert.DotNet));
    }

    [Fact]
    public async Task CheckAsync_LeveArgumentNullException_QuandIssuerEstNull()
    {
        var (svc, _) = BuildService(_ => Http200(Array.Empty<byte>()));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => svc.CheckAsync(LeafWithAia.DotNet, null!));
    }
}
