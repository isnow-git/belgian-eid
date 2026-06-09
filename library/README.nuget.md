# BelgianEid

[![NuGet](https://img.shields.io/nuget/v/BelgianEid.svg?logo=nuget)](https://www.nuget.org/packages/BelgianEid)
[![Downloads](https://img.shields.io/nuget/dt/BelgianEid.svg)](https://www.nuget.org/packages/BelgianEid)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/isnow-git/belgian-eid/blob/main/LICENSE)

.NET 8 library for the Belgian electronic identity card (eID) over PKCS#11: read
identity / address / photo, read the X.509 certificates, verify the PIN, produce
electronic signatures, run challenge-response authentication, and check
certificate revocation via OCSP / CRL. Dependency-injection first and fully
mockable (every operation sits behind an interface).

> **Full documentation, browser bridge/extension, installer and source code:**
> **https://github.com/isnow-git/belgian-eid**

## Install

```powershell
dotnet add package BelgianEid
```

## Native middleware (required)

This package talks to the card through the Belgian eID middleware's native
PKCS#11 library (`beidpkcs11`). For licensing reasons (LGPLv3) the package does
**not** bundle it. Provide it in any one of these ways:

1. **Install the official middleware system-wide** (recommended) from
   <https://eid.belgium.be> — most cardholders already have it. The library then
   loads `beidpkcs11` from the operating system search path automatically.
2. **Point at an explicit path:**
   `options.Pkcs11LibraryPath = @"C:\Program Files\Belgium Identity Card\FireFox-Plugins\beidpkcs11.dll";`
3. **Ship the native library next to your app** under `native/<rid>/` and the
   library resolves it automatically:

   | Platform            | RID         | File name              |
   | ------------------- | ----------- | ---------------------- |
   | Windows x64         | `win-x64`   | `beidpkcs11.dll`       |
   | Windows x86         | `win-x86`   | `beidpkcs11.dll`       |
   | Linux x64           | `linux-x64` | `libbeidpkcs11.so`     |
   | macOS Intel         | `osx-x64`   | `libbeidpkcs11.dylib`  |
   | macOS Apple Silicon | `osx-arm64` | `libbeidpkcs11.dylib`  |

   Download the native libraries from <https://eid.belgium.be> or
   <https://github.com/Fedict/eid-mw>. Resolution order: explicit path →
   bundled `native/<rid>/` → operating system path.

## Quick start

```csharp
using BelgianEid.Abstractions;
using BelgianEid.DependencyInjection;
using BelgianEid.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;

await using var provider = new ServiceCollection()
    .AddBelgianEid()
    .BuildServiceProvider();

var client = provider.GetRequiredService<IEidClient>();

// Open a session on the first reader that holds a card.
using IEidSession session = client.OpenSession();

// Read every field in one call.
EidCardData card = client.ReadCardData(session);
Console.WriteLine($"{card.Identity.FirstNames} {card.Identity.LastName}");
Console.WriteLine($"{card.Address.Street}, {card.Address.ZipCode} {card.Address.Municipality}");

// Sign a server challenge with the authentication key.
byte[] challenge = RandomNumberGenerator.GetBytes(32);
AuthenticationResult auth = await client.AuthenticateAsync(session, challenge, pin: "1234");
// auth.Signature                 → byte[]
// auth.AuthenticationCertificate → X509Certificate2  (send this to the server)

// Check the authentication certificate against the Belgian OCSP responder.
OcspResult ocsp = await client.ValidateCertificateAsync(session, EidCertificateKind.Authentication);
Console.WriteLine(ocsp.IsValid ? "Certificate valid" : $"Revoked on {ocsp.RevocationTime:d}");
```

Without a DI container, the services can be constructed directly — see the
[full library guide](https://github.com/isnow-git/belgian-eid/blob/main/library/README.md).

## Configuration

```csharp
services.AddBelgianEid(options =>
{
    options.Pkcs11LibraryPath = @"C:\path\to\beidpkcs11.dll"; // optional explicit path
    options.OcspTimeout       = TimeSpan.FromSeconds(10);
});
```

| Option                   | Default                                     | Purpose                                            |
| ------------------------ | ------------------------------------------- | -------------------------------------------------- |
| `Pkcs11LibraryPath`      | `null` (auto-detect)                        | Explicit path to the native `beidpkcs11` library.  |
| `BundledNativeDirectory` | `"native"`                                  | Folder holding per-RID native binaries.            |
| `OcspResponderUrl`       | `http://ocsp.eidpki.belgium.be/eid/0`       | Belgian OCSP responder (fallback when no AIA).      |
| `OcspTimeout`            | `00:00:15`                                  | Maximum wait for an OCSP response.                 |

## Error handling

All exceptions derive from `EidException` (namespace `BelgianEid.Exceptions`):

| Exception                       | Thrown when                                                  |
| ------------------------------- | ----------------------------------------------------------- |
| `EidConfigurationException`     | `beidpkcs11` not found or cannot be loaded                   |
| `EidReaderNotFoundException`    | No PC/SC reader detected                                     |
| `EidCardNotPresentException`    | No eID card in the reader                                    |
| `EidPinIncorrectException`      | Wrong PIN — `RemainingAttempts` says how many are left       |
| `EidPinBlockedException`        | PIN blocked after 3 failures                                 |
| `EidCertificateRevokedException`| Certificate revoked — `RevocationTime` holds the date        |
| `EidCommunicationException`     | Card or OCSP/CRL communication failure                       |
| `EidDataNotFoundException`      | Expected data object / certificate absent from the card      |

## License

MIT — see the [LICENSE](https://github.com/isnow-git/belgian-eid/blob/main/LICENSE).
The native `beidpkcs11` middleware is LGPLv3 (SPF BOSA); see
[THIRD-PARTY-NOTICES](https://github.com/isnow-git/belgian-eid/blob/main/THIRD-PARTY-NOTICES.md).
