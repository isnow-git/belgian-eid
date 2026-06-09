using System.Reflection;
using BelgianEid.Abstractions;
using BelgianEid.Bridge.Common;
using BelgianEid.Bridge.Handlers;
using BelgianEid.Bridge.Hosting;
using BelgianEid.Bridge.NativeMessaging;
using BelgianEid.Bridge.Routing;
using BelgianEid.Bridge.Services;
using BelgianEid.DependencyInjection;
using BelgianEid.Models;
using Microsoft.Extensions.DependencyInjection;

// ============================================================================
// BelgianEidBridge — Chrome Native Messaging Host for Belgian eID cards
// ============================================================================
// Protocol : stdin/stdout with a 4-byte little-endian length prefix per frame.
// Lifecycle: Chrome launches this process on connectNative() and kills it when
//            the extension disconnects or the browser closes.
// Reference: https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging
// ============================================================================

// Redirect Console.Out → stderr so that accidental writes never corrupt the
// binary Native Messaging protocol on stdout.
Console.SetOut(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });

var stdin = Console.OpenStandardInput();
var stdout = Console.OpenStandardOutput();

// ── BelgianEid library — DI container ────────────────────────────────────────
await using var provider = new ServiceCollection()
    .AddBelgianEid()
    .BuildServiceProvider();

// ── Bridge metadata ──────────────────────────────────────────────────────────
var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
var info = new BridgeInfo(Version: version, ServiceName: "belgian-eid-bridge");

// ── eID service (Dependency Inversion: all handlers depend on IEidService) ───
IEidService eidService = new EidService(
    provider.GetRequiredService<IEidClient>(),
    provider.GetRequiredService<IEidIdentityService>(),
    provider.GetRequiredService<IEidCertificateService>(),
    provider.GetRequiredService<IEidPinService>(),
    provider.GetRequiredService<IEidSignatureService>(),
    provider.GetRequiredService<IEidAuthenticationService>());

// ── I/O ──────────────────────────────────────────────────────────────────────
INativeMessageReader reader = new NativeMessageReader(stdin);
INativeMessageWriter writer = new NativeMessageWriter(stdout);

// ── Reader hot-plug monitoring ────────────────────────────────────────────────
// The monitor runs on a background thread; events are pushed to Chrome as they occur.
// NativeMessageWriter.Write() is thread-safe (internal lock).
var readerMonitor = provider.GetRequiredService<IEidReaderMonitor>();
readerMonitor.ReaderChanged += (_, e) =>
{
    writer.Write(new
    {
        type = "reader_state_changed",
        eventKind = e.Kind switch
        {
            EidReaderEventKind.ReaderConnected    => "readerConnected",
            EidReaderEventKind.ReaderDisconnected => "readerDisconnected",
            EidReaderEventKind.CardInserted       => "cardInserted",
            EidReaderEventKind.CardRemoved        => "cardRemoved",
            _                                    => e.Kind.ToString(),
        },
        reader = new
        {
            name = e.Reader.Name,
            slotId = e.Reader.SlotId,
            hasCardInserted = e.Reader.HasCardInserted,
        },
    });
};
readerMonitor.Start();

// ── Handlers (Open/Closed: add a command by adding a handler + one line here)
IMessageHandler[] handlers =
[
    // Utility
    new PingHandler(info),

    // Reader detection
    new GetReadersHandler(eidService),
    new StatusHandler(eidService),

    // Identity
    new ReadIdentityHandler(eidService),
    new ReadAddressHandler(eidService),
    new ReadPhotoHandler(eidService),
    new ReadCardHandler(eidService),

    // Certificates
    new ReadCertificatesHandler(eidService),
    new ReadCertificateHandler(eidService),

    // PIN
    new GetPinStatusHandler(eidService),
    new VerifyPinHandler(eidService),

    // Signing
    new SignChallengeHandler(eidService),
    new SignDataHandler(eidService),
    new SignHashHandler(eidService),

    // Certificate validation (OCSP)
    new ValidateCertificateHandler(eidService),
];

// ── Router ───────────────────────────────────────────────────────────────────
IMessageRouter router = new MessageRouter(handlers);

// ── Host ─────────────────────────────────────────────────────────────────────
var host = new NativeMessagingHost(reader, writer, router);
await host.RunAsync();
readerMonitor.Stop();
