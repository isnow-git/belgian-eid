# BelgianEid — Tests

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com)
[![Tests](https://img.shields.io/badge/tests-17%20passing-brightgreen?style=flat-square)](.)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](../LICENSE)

xUnit unit tests for `OcspValidationService`. **No physical card, no reader, and no network connection required.**

---

## Run

```powershell
dotnet test tests/BelgianEid.Tests.csproj
dotnet test tests/BelgianEid.Tests.csproj --verbosity normal
```

Or from the solution root:

```powershell
dotnet test BelgianEid.sln
```

---

## Test coverage — 17 tests

| Group | Tests | What is verified |
|---|---|---|
| **OCSP statuses** | 3 | `Good` · `Revoked` (with timestamp) · `Unknown` — correct `OcspResult` fields |
| **`EnsureNotRevokedAsync`** | 2 | No exception on `Good`; throws `EidCertificateRevokedException` on `Revoked` |
| **Network errors** | 4 | HTTP 500 · `HttpRequestException` · garbage bytes · OCSP error status (`MalformedRequest`) |
| **URL routing** | 2 | Picks AIA URL when present; falls back to configured URL when AIA is absent |
| **HTTP compliance** | 4 | POST method · `Content-Type: application/ocsp-request` · `Accept: application/ocsp-response` · non-empty body |
| **Null arguments** | 2 | `ArgumentNullException` for null `certificate` or `issuer` |

---

## How it works

`HttpClient` is replaced by `FakeHttpMessageHandler` — a minimal stub that intercepts all outgoing requests and returns a pre-built response. X.509 certificates and DER-encoded OCSP responses are generated in-process using BouncyCastle (RSA 1 024-bit for speed).

```
tests/
├── OcspValidationServiceTests.cs   17 xUnit tests
└── helpers/
    ├── CertificateFactory.cs       Generates CA + leaf X.509 certs in memory
    ├── OcspResponseFactory.cs      Builds Good / Revoked / Unknown / Error OCSP responses
    └── FakeHttpMessageHandler.cs   Intercepts HttpClient calls — no real network
```

---

## Mocking card-dependent services

`IEidSession` is a plain interface — substitute it with any mocking framework to test services that depend on a physical card:

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

MIT — see [LICENSE](../LICENSE)
