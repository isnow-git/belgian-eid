# BelgianEid — .NET Smart-Card Library

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com)
[![NuGet](https://img.shields.io/badge/NuGet-BelgianEid-004880?style=flat-square&logo=nuget)](https://www.nuget.org/packages/BelgianEid)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](../LICENSE)
[![Tests](https://img.shields.io/badge/tests-17%20passing-brightgreen?style=flat-square)](../tests)

**.NET 8 class library** for reading and cryptographically operating on the Belgian electronic identity card (eID) via PKCS#11. Designed for dependency injection, fully mockable for unit testing, and independently usable outside of the Chrome bridge.

---

## Table of Contents

- [Features](#features)
- [Quick Start](#quick-start)
- [Dependency Injection](#dependency-injection)
- [Reader Hot-Plug Monitoring](#reader-hot-plug-monitoring)
- [Configuration](#configuration)
- [Error Handling](#error-handling)
- [Architecture](#architecture)
- [Testing](#testing)
- [License](#license)

---

## Features

| Domain | Operations |
|---|---|
| **Readers** | Enumerate connected PC/SC readers · hot-plug monitoring (500 ms polling) |
| **Identity** | Name · birth date and place · nationality · national register number (NRN) |
| **Address** | Street · postal code · municipality |
| **Photo** | JPEG image embedded on the card |
| **Certificates** | All four X.509 certificates: Authentication · Signature · Citizen CA · Root CA |
| **PIN** | Verify PIN code · query remaining attempts |
| **Authentication** | Sign a server-issued challenge with the card's authentication key (RSA / ECDSA) |
| **Signature** | Sign arbitrary data or a pre-computed hash with the non-repudiation key |
| **OCSP** | Online certificate revocation check via the official Belgian OCSP responder (RFC 6960, SHA-256) |

---

## Install

```powershell
dotnet add package BelgianEid
```

> **Native middleware.** The NuGet package does **not** bundle the LGPLv3
> `beidpkcs11` native library. Install the
> [Belgian eID middleware](https://eid.belgium.be) system-wide (most cardholders
> already have it), or drop the native library under `native/<rid>/` next to your
> app — see [Configuration](#configuration). When you build this repository from
> source, the Windows binary is bundled for you under
> `library/native/win-x64/`.

---

## Quick Start

```csharp
using BelgianEid.Abstractions;
using BelgianEid.DependencyInjection;
using BelgianEid.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;

await using var provider = new ServiceCollection()
    .AddLogging()
    .AddBelgianEid()
    .BuildServiceProvider();

var client = provider.GetRequiredService<IEidClient>();

// Open a session on the first available card
using IEidSession session = client.OpenSession();

// Read all card data in one call
EidCardData card = client.ReadCardData(session);
Console.WriteLine($"{card.Identity.FirstNames} {card.Identity.LastName}");
Console.WriteLine($"{card.Address.Street}, {card.Address.ZipCode} {card.Address.Municipality}");

// Authenticate against a server challenge
byte[] challenge = RandomNumberGenerator.GetBytes(32);
AuthenticationResult auth = await client.AuthenticateAsync(session, challenge, pin: "1234");
// auth.Signature                 → byte[]           (RSA-PKCS1v15/SHA-256 or ECDSA)
// auth.AuthenticationCertificate → X509Certificate2 (send this to the server)

// Validate the authentication certificate via OCSP
OcspResult ocsp = await client.ValidateCertificateAsync(session, EidCertificateKind.Authentication);
Console.WriteLine(ocsp.IsValid ? "Certificate is valid." : $"Revoked on {ocsp.RevocationTime:d}");
```

---

## Dependency Injection

Register all services with a single call on any `IServiceCollection`:

```csharp
// Default configuration
services.AddBelgianEid();

// Custom configuration via delegate
services.AddBelgianEid(options =>
{
    options.OcspTimeout         = TimeSpan.FromSeconds(10);
    options.Pkcs11LibraryPath   = @"C:\Windows\System32\beidpkcs11.dll"; // explicit path
});

// Configuration from appsettings.json  (section "BelgianEid")
services.AddBelgianEid();
// appsettings.json:  { "BelgianEid": { "OcspTimeout": "00:00:10" } }
```

**Services registered by `AddBelgianEid()`**

| Interface | Implementation | Notes |
|---|---|---|
| `IEidClient` | `EidClient` | High-level facade — start here |
| `IEidReaderService` | `EidReaderService` | Reader enumeration + session management |
| `IEidReaderMonitor` | `EidReaderMonitor` | Hot-plug background watcher |
| `IEidIdentityService` | `EidIdentityService` | TLV identity / address / photo parsing |
| `IEidCertificateService` | `EidCertificateService` | X.509 certificate retrieval |
| `IEidPinService` | `EidPinService` | PIN verification and status |
| `IEidSignatureService` | `EidSignatureService` | Non-repudiation key signing |
| `IEidAuthenticationService` | `EidAuthenticationService` | Challenge signing (authentication key) |
| `IOcspValidationService` | `OcspValidationService` | Online revocation check |
| `IPkcs11LibraryProvider` | `Pkcs11LibraryProvider` | Thread-safe PKCS#11 library loader |

### Usage without DI

The library is fully usable without a DI container — useful in tests or small utilities:

```csharp
using BelgianEid.Configuration;
using BelgianEid.Implementations;
using Microsoft.Extensions.Options;

var options       = Options.Create(new BelgianEidOptions());
var libraryLoader = new Pkcs11LibraryProvider(options);
var readerService = new EidReaderService(libraryLoader);
var pinService    = new EidPinService();

using IEidSession session = readerService.OpenSession();
pinService.VerifyPin(session, "1234");
```

---

## Reader Hot-Plug Monitoring

`IEidReaderMonitor` detects reader and card state changes in real time by polling PKCS#11 every 500 ms. Subscribe to `ReaderChanged` before calling `Start()`:

```csharp
var monitor = provider.GetRequiredService<IEidReaderMonitor>();

monitor.ReaderChanged += (_, e) =>
{
    Console.WriteLine($"[{e.Kind}] {e.Reader.Name}  card={e.Reader.HasCardInserted}");
    // e.Kind: ReaderConnected | ReaderDisconnected | CardInserted | CardRemoved
};

monitor.Start();

// ...

monitor.Stop();
```

> The event fires from a background thread. Marshal to the UI thread with a `Dispatcher` or `SynchronizationContext` if updating UI controls.

---

## Configuration

All options are exposed through `BelgianEidOptions` using the standard `IOptions<T>` pattern.

| Option | Type | Default | Description |
|---|---|---|---|
| `Pkcs11LibraryPath` | `string?` | `null` | Explicit path to `beidpkcs11`. Auto-detected via PATH and bundled natives if null. |
| `BundledNativeDirectory` | `string` | `"native"` | Subdirectory containing per-RID native binaries (relative to the assembly). |
| `OcspResponderUrl` | `Uri` | `http://ocsp.eidpki.belgium.be/eid/0` | Official Belgian OCSP responder. Used as fallback when the certificate has no AIA extension. |
| `OcspTimeout` | `TimeSpan` | `00:00:15` | Maximum wait for an OCSP response before throwing `EidCommunicationException`. |
| `DataObjectLabels.Identity` | `string` | `"identity"` | PKCS#11 CKO_DATA label for the identity file. |
| `DataObjectLabels.Address` | `string` | `"address"` | PKCS#11 CKO_DATA label for the address file. |
| `DataObjectLabels.Photo` | `string` | `"photo_file"` | PKCS#11 CKO_DATA label for the photo file. |

**Native library resolution order**

1. `BelgianEidOptions.Pkcs11LibraryPath` if set
2. Bundled file under `native/<runtime-identifier>/` (copied to output by the `.csproj` glob)
3. System PATH — Belgian eID middleware installation (`beidpkcs11.dll` / `libbeidpkcs11.so` / `libbeidpkcs11.dylib`)

**Bundling a native library for a non-Windows platform**

Drop the file in the matching `native/<rid>/` folder and rebuild. The `.csproj` glob `native/**/*` copies it automatically.

| Platform | RID | Expected filename |
|---|---|---|
| Windows 64-bit | `win-x64` | `beidpkcs11.dll` |
| Windows 32-bit | `win-x86` | `beidpkcs11.dll` |
| Linux x64 | `linux-x64` | `libbeidpkcs11.so` |
| Linux ARM64 | `linux-arm64` | `libbeidpkcs11.so` |
| macOS Intel | `osx-x64` | `libbeidpkcs11.dylib` |
| macOS Apple Silicon | `osx-arm64` | `libbeidpkcs11.dylib` |

Download the native libraries from [eid.belgium.be](https://eid.belgium.be) or [github.com/Fedict/eid-mw](https://github.com/Fedict/eid-mw).

---

## Error Handling

All library exceptions derive from `EidException` (namespace `BelgianEid.Exceptions`):

| Exception | Thrown when |
|---|---|
| `EidConfigurationException` | `beidpkcs11` not found or cannot be loaded |
| `EidReaderNotFoundException` | No PC/SC reader detected |
| `EidCardNotPresentException` | No eID card in the target reader |
| `EidCommunicationException` | Communication error with the card or OCSP/CRL responder |
| `EidPinIncorrectException` | Wrong PIN — `RemainingAttempts` property indicates how many are left |
| `EidPinBlockedException` | PIN blocked after 3 consecutive failures |
| `EidCertificateRevokedException` | Certificate is revoked — `RevocationTime` property holds the revocation date |
| `EidDataNotFoundException` | Data object or certificate absent from the card |
| `EidCertificateException` | Malformed or unreadable X.509 data |

```csharp
try
{
    using IEidSession session = client.OpenSession();
    pinService.VerifyPin(session, pin);
}
catch (EidPinIncorrectException ex)
{
    Console.WriteLine($"Wrong PIN — {ex.RemainingAttempts} attempt(s) remaining.");
}
catch (EidPinBlockedException)
{
    Console.WriteLine("PIN blocked. Contact your municipality to unblock.");
}
catch (EidCertificateRevokedException ex)
{
    Console.WriteLine($"Certificate revoked on {ex.RevocationTime:d}.");
}
catch (EidException ex)
{
    Console.WriteLine($"eID error: {ex.Message}");
}
```

---

## Architecture

```
library/
├── abstractions/          IEidClient · IEidSession · IEidReaderService · …
├── implementations/       EidClient · EidReaderService · OcspValidationService · …
├── models/                EidCardData · EidIdentity · AuthenticationResult · OcspResult · …
├── exceptions/            EidException and all typed subtypes
├── configuration/         BelgianEidOptions  (IOptions<T> pattern)
├── dependency-injection/  AddBelgianEid() IServiceCollection extension
├── utilities/             TlvParser · SigningUtils · EidInternals  (internal)
└── native/
    ├── win-x64/           beidpkcs11.dll          (Windows 64-bit — bundled)
    ├── win-x86/           beidpkcs11.dll          (Windows 32-bit — add manually)
    ├── linux-x64/         libbeidpkcs11.so        (Linux x64 — add manually)
    ├── linux-arm64/       libbeidpkcs11.so        (Linux ARM64 — add manually)
    ├── osx-x64/           libbeidpkcs11.dylib     (macOS Intel — add manually)
    └── osx-arm64/         libbeidpkcs11.dylib     (macOS Apple Silicon — add manually)
```

**Service layer diagram**

```
┌─────────────────────────────────────────────────┐
│  IEidClient                                     │
│  High-level facade — recommended entry point    │
├──────────────┬──────────────┬───────────────────┤
│ IEidIdentity │   Crypto     │  IOcsp            │
│ Service      │   Services   │  ValidationService│
├──────────────┴──────────────┴───────────────────┤
│  IEidSession                                    │
│  PKCS#11 primitive operations — fully mockable  │
├─────────────────────────────────────────────────┤
│  IPkcs11LibraryProvider                         │
│  Thread-safe loader for beidpkcs11 native lib   │
└─────────────────────────────────────────────────┘
```

**Design principles**

| Principle | Application |
|---|---|
| **Single Responsibility** | Each service handles exactly one concern (reading, signing, OCSP, etc.) |
| **Open/Closed** | Add a new operation by adding a service; existing services never change |
| **Dependency Inversion** | All services depend on interfaces, never on concrete implementations |
| **Interface Segregation** | Consumers inject only the interfaces they need — no fat interfaces |

---

## Testing

The test project (`tests/BelgianEid.Tests`) contains **17 unit tests** covering `OcspValidationService`. No reader, no card, and no network connection are required — `HttpClient` is stubbed via `FakeHttpMessageHandler`, and certificates and OCSP responses are generated in memory using BouncyCastle.

```powershell
dotnet test tests/BelgianEid.Tests
dotnet test tests/BelgianEid.Tests --verbosity normal
```

**Test coverage**

| Group | What is tested |
|---|---|
| OCSP statuses | `Good` · `Revoked` (with date) · `Unknown` |
| `EnsureNotRevokedAsync` | Does not throw on `Good`; throws `EidCertificateRevokedException` on `Revoked` |
| Network errors | HTTP 500 · `HttpRequestException` · malformed response · OCSP error status |
| URL routing | From AIA extension · fallback to configured URL when AIA is absent |
| HTTP compliance | POST method · `Content-Type: application/ocsp-request` · `Accept: application/ocsp-response` |
| Null arguments | `ArgumentNullException` for null `certificate` or `issuer` |

**Mocking services that depend on a physical card**

`IEidSession` is a plain interface — mock it with any framework:

```csharp
// NSubstitute example
var session = Substitute.For<IEidSession>();
session.GetCertificateRaw(EidCertificateKind.Authentication).Returns(certDerBytes);
session.Sign(EidPrivateKeyKind.Authentication, Arg.Any<byte[]>(), Arg.Any<EidSignatureMechanism>())
       .Returns(signatureBytes);

var authService = new EidAuthenticationService(new EidCertificateService());
AuthenticationResult result = await authService.AuthenticateAsync(session, challenge, pin: "0000");
```

---

## License

MIT — see [LICENSE](../LICENSE).

`beidpkcs11` is distributed under LGPLv3 by SPF BOSA. See
[THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) for redistribution
obligations.
