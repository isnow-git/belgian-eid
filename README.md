# Belgian eID SDK

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com)
[![NuGet](https://img.shields.io/badge/NuGet-BelgianEid-004880?style=flat-square&logo=nuget)](https://www.nuget.org/packages/BelgianEid)
[![CI](https://github.com/isnow-git/belgian-eid/actions/workflows/ci.yml/badge.svg)](https://github.com/isnow-git/belgian-eid/actions/workflows/ci.yml)
[![Chrome MV3](https://img.shields.io/badge/Chrome-Manifest%20V3-4285F4?style=flat-square&logo=googlechrome)](https://developer.chrome.com/docs/extensions/mv3/)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?style=flat-square&logo=windows)](https://eid.belgium.be)
[![Tests](https://img.shields.io/badge/tests-17%20passing-brightgreen?style=flat-square)](tests)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)

A toolkit for interacting with the Belgian electronic identity card (eID) from a
.NET application or a web browser. It ships as several layers that can be used
independently: a standalone **.NET library** (`BelgianEid` on NuGet), a Chrome
**Native Messaging bridge**, a Chrome **MV3 extension**, and a Windows
**installer** that deploys the browser stack in one click — no plugins, no
developer mode.

> **Just need the .NET library?** Run `dotnet add package BelgianEid` and read
> [library/README.md](library/README.md). The browser stack (bridge + extension + installer) is optional and only needed to reach the card from a web page.

---

## Components

| Component                             | Technology                            | Role                                                              |
| ------------------------------------- | ------------------------------------- | ----------------------------------------------------------------- |
| [**library/**](library/README.md)     | .NET 8 class library                  | Read card data · verify PIN · authenticate · sign · validate OCSP |
| [**bridge/**](bridge/README.md)       | .NET 8 · Chrome Native Messaging host | Expose every library operation to Chrome extensions over stdio    |
| [**extension/**](extension/README.md) | JavaScript · Chrome MV3               | Bridge web pages to the native host via `window.postMessage`      |
| [**installer/**](installer/README.md) | PowerShell · Inno Setup 6             | One-click Windows installer — deploys bridge + extension silently |
| [**samples/**](samples/README.md)     | .NET 8 console application            | End-to-end walkthrough: read · PIN · sign · OCSP                  |
| [**tests/**](tests/README.md)         | xUnit · BouncyCastle                  | 17 OCSP unit tests — no card, no reader, no network required      |

---

## How it works

```
Web app  (any website)
    │
    │  window.postMessage({ type: 'SIGN_CHALLENGE', pin, challenge })
    ▼
extension/content.js          Chrome ISOLATED world — relays to Service Worker
    │
    │  chrome.runtime.sendMessage(...)
    ▼
extension/background/         Service Worker — owns the persistent native port
    │
    │  Chrome Native Messaging  (stdin / stdout · 4-byte LE length framing)
    ▼
bridge/                       .NET 8 process — launched on demand by Chrome
    │
    │  PKCS#11  (beidpkcs11.dll — Belgian eID middleware)
    ▼
Belgian eID card              RSA / ECDSA keys · identity files · X.509 certificates
```

- The **library** does all smart-card work via PKCS#11 and is independently
  usable in any .NET application.
- The **bridge** wraps every library operation as a typed JSON message reachable
  from Chrome.
- The **extension** makes those messages available to a web page, gated to the
  origins you allow.

---

## Getting started

### End-user installation (recommended)

The Windows installer deploys everything in a single step — no developer mode,
no manual configuration.

1. Download `BelgianEidSetup-x.x.x.exe` from Releases
2. Run as Administrator
3. Restart Google Chrome — the extension activates automatically

**Prerequisites for end users**

| Requirement                      | Version                        |
| -------------------------------- | ------------------------------ |
| Windows 10 (1809+) or 11, 64-bit | —                              |
| Google Chrome                    | 111+                           |
| PC/SC smart-card reader          | Any ISO 7816-compatible reader |

---

### Use only the .NET library

```powershell
dotnet add package BelgianEid
```

```csharp
using BelgianEid.Abstractions;
using BelgianEid.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

await using var provider = new ServiceCollection()
    .AddBelgianEid()
    .BuildServiceProvider();

var client = provider.GetRequiredService<IEidClient>();
using var session = client.OpenSession();
var card = client.ReadCardData(session);
Console.WriteLine($"{card.Identity.FirstNames} {card.Identity.LastName}");
```

Full API, configuration, and error handling: [library/README.md](library/README.md).

---

### Developer installation (full browser stack)

**Prerequisites**

| Tool                    | Version                        |
| ----------------------- | ------------------------------ |
| .NET 8 SDK              | 8.0+                           |
| Google Chrome           | 111+                           |
| PC/SC smart-card reader | Any ISO 7816-compatible reader |

**1. Build the solution**

```powershell
dotnet build BelgianEid.sln
```

**2. Publish and register the bridge**

```powershell
dotnet publish bridge/BelgianEidBridge.csproj -c Release -r win-x64 --self-contained -o bridge/publish
cd bridge
.\register.ps1 -ExtensionId <your-32-char-extension-id>
```

**3. Load the extension**

1. Open `chrome://extensions`
2. Enable **Developer mode** (top right)
3. Click **Load unpacked** → select the `extension/` folder
4. Copy the extension ID shown on the card → pass it to `register.ps1`
5. Edit the placeholder origins in `extension/manifest.json` to match your web
   app (see [extension/README.md](extension/README.md#point-the-extension-at-your-web-app))
6. Restart Chrome

**4. Run the console sample** (optional — requires a physical card)

```powershell
dotnet run --project samples
```

---

### Build the installer

```powershell
# Prerequisite: Inno Setup 6 — https://jrsoftware.org/
cd installer
.\build.ps1
# Output: installer/output/BelgianEidSetup-x.x.x.exe
```

> **Note.** On first run, `build.ps1` prompts you to close Chrome so it can
> package the extension. An `extension-key.pem` is generated in `installer/` —
> keep it safe. Losing it changes the extension ID on already-deployed machines.
> See [installer/README.md](installer/README.md) for full documentation.

---

## Repository structure

```
belgian-eid/
│
├── BelgianEid.sln                     Visual Studio solution (library + bridge + samples + tests)
├── Directory.Build.props              Shared MSBuild metadata (version, authors, license)
├── LICENSE                            MIT
├── THIRD-PARTY-NOTICES.md             Bundled dependencies and the LGPLv3 obligation
├── CHANGELOG.md
├── .github/workflows/ci.yml           Build + test on every push / PR
│
├── library/                           .NET 8 class library — core smart-card operations
│   ├── abstractions/                  Public interfaces  (IEidClient, IEidSession, …)
│   ├── implementations/               Service implementations (PKCS#11 bindings)
│   ├── models/                        Domain models and result types
│   ├── exceptions/                    Typed exception hierarchy (EidException subtypes)
│   ├── configuration/                 BelgianEidOptions  (Options pattern)
│   ├── dependency-injection/          AddBelgianEid() IServiceCollection extension
│   ├── utilities/                     TLV parser, signing helpers  (internal)
│   ├── native/                        Bundled beidpkcs11 per runtime identifier
│   └── README.md
│
├── bridge/                            Chrome Native Messaging host  (.NET 8 console)
│   ├── handlers/                      One handler per message type  (SRP)
│   ├── hosting/                       Async message loop
│   ├── native-messaging/              stdin/stdout framing layer
│   ├── routing/                       Dictionary-based message router  (OCP)
│   ├── services/                      IEidService facade over the library
│   ├── common/                        Shared types and helpers
│   ├── register.ps1                   Registration script (HKCU registry key)
│   └── README.md
│
├── extension/                         Chrome MV3 extension  (JavaScript ES modules)
│   ├── background/                    Service Worker  (NativeClient · EidApi · MessageDispatcher)
│   ├── content/                       Content script  (postMessage relay)
│   ├── inject/                        Injected script  (extension detection marker)
│   ├── popup/                         Extension popup UI
│   ├── shared/                        Shared constants
│   └── README.md
│
├── installer/                         Windows installer  (Inno Setup 6)
│   ├── build.ps1                      Build pipeline (bridge → crx → installer)
│   ├── setup.iss                      Inno Setup script
│   └── README.md
│
├── samples/                           Console walkthrough  (read · PIN · sign · OCSP)
│   └── README.md
│
└── tests/                             17 OCSP unit tests  (no card, no network)
    └── README.md
```

---

## Compatibility

| Component | Windows 10/11 |   macOS   |   Linux   |
| --------- | :-----------: | :-------: | :-------: |
| library   |      Yes      | Partial\* | Partial\* |
| bridge    |      Yes      |     —     |     —     |
| extension |      Yes      |     —     |     —     |
| installer |      Yes      |     —     |     —     |

\*macOS / Linux: the library compiles and runs if `beidpkcs11` is installed
system-wide. Bridge and installer are Windows-only.

---

## Running the tests

No physical card, no reader, no network required.

```powershell
dotnet test tests
```

---

## Contributing

Issues and pull requests are welcome. The CI workflow builds the solution and
runs the test suite on Windows for every push and pull request. Please keep the
public API documented (XML doc comments) and update [CHANGELOG.md](CHANGELOG.md)
for user-facing changes.

---

## License

| Component                                        | License    |
| ------------------------------------------------ | ---------- |
| Belgian eID SDK (this repository)                | MIT        |
| `beidpkcs11` (Belgian eID middleware — SPF BOSA) | LGPLv3     |
| Pkcs11Interop                                    | Apache-2.0 |
| BouncyCastle.Cryptography                        | MIT        |
| Microsoft.Extensions.\*                          | MIT        |

> **LGPLv3 notice.** The native library `beidpkcs11.dll` bundled under
> `library/native/win-x64/` is distributed under the GNU Lesser General Public
> License v3. If you redistribute it you must: (1) provide access to the
> LGPL-covered source, and (2) allow end users to replace the binary with their
> own build. Download the source at
> [github.com/Fedict/eid-mw](https://github.com/Fedict/eid-mw). See
> [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
