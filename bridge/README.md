# Belgian eID Bridge

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?style=flat-square&logo=windows)](https://eid.belgium.be)
[![Chrome](https://img.shields.io/badge/Chrome-Native%20Messaging-4285F4?style=flat-square&logo=googlechrome)](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging)
[![Version](https://img.shields.io/badge/version-1.0.0-brightgreen?style=flat-square)](BelgianEidBridge.csproj)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](../LICENSE)

**Chrome Native Messaging host** that exposes the full [BelgianEid library](../library/README.md) to any Chrome extension. Every smart-card operation — reader detection, identity reading, certificate chain, PIN management, electronic signing, and OCSP validation — is available as a typed JSON message over stdin/stdout.

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [API Reference](#api-reference)
- [Push Events — Reader Hot-Plug](#push-events--reader-hot-plug)
- [Error Responses](#error-responses)
- [Getting Started](#getting-started)
- [Extending the Bridge](#extending-the-bridge)
- [Design Principles](#design-principles)
- [License](#license)

---

## Overview

`BelgianEidBridge.exe` is a Windows console application that Chrome launches automatically when a Chrome extension calls `chrome.runtime.connectNative('be.belgianeid.bridge')`. Communication uses the [Chrome Native Messaging protocol](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging): each message is a UTF-8 JSON payload prefixed with a 4-byte little-endian length field.

The bridge uses a **session-per-call** model: each incoming message opens a fresh PC/SC session, performs the requested operation, and immediately closes the session. No long-lived card handles are kept between messages.

```
Chrome extension
    │  chrome.runtime.connectNative('be.belgianeid.bridge')
    │
    │  JSON messages — 4-byte LE length prefix + UTF-8 payload
    ▼
┌────────────────────────────────────────────────────────────┐
│  NativeMessagingHost    async message loop + error routing │
│    └─ MessageRouter     dispatches by "type" field (OCP)   │
│         └─ XxxHandler   one per command (SRP)              │
│              └─ IEidService                                │
│                   └─ EidService  session-per-call facade   │
│                        └─ BelgianEid library (IEidClient)  │
└─────────────────────────────┬──────────────────────────────┘
                              │  PKCS#11  (beidpkcs11.dll)
                              ▼
                       Belgian eID card
```

---

## Architecture

```
bridge/
├── Program.cs                     Composition root — wires DI and starts the host
├── BelgianEidBridge.csproj
├── app.manifest                   Windows application manifest (no UAC elevation)
├── be.belgianeid.bridge.template.json  Native Messaging manifest template (committed)
├── be.belgianeid.bridge.json      Generated host manifest (git-ignored, machine-specific)
├── register.ps1                   Registration script — writes HKCU registry key
│
├── common/
│   ├── BridgeInfo.cs              Service name and version constants
│   ├── BridgeRequestException.cs  Thrown on malformed or invalid requests
│   └── JsonMessageExtensions.cs   Safe JsonElement accessors and domain parsers
│
├── hosting/
│   └── NativeMessagingHost.cs     Async read/write loop with layered error handling
│
├── native-messaging/
│   ├── INativeMessageReader.cs
│   ├── NativeMessageReader.cs     Reads length-prefixed JSON from stdin
│   ├── INativeMessageWriter.cs
│   └── NativeMessageWriter.cs     Writes length-prefixed JSON to stdout
│
├── routing/
│   ├── IMessageRouter.cs
│   └── MessageRouter.cs           Dictionary-based router — dispatches by "type" field
│
├── handlers/                      One class per command (SRP + OCP)
│   ├── IMessageHandler.cs         Contract: ValueTask<object> HandleAsync(JsonElement, string?)
│   ├── PingHandler.cs             ping
│   ├── GetReadersHandler.cs       get_readers
│   ├── StatusHandler.cs           get_status
│   ├── ReadIdentityHandler.cs     read_identity
│   ├── ReadAddressHandler.cs      read_address
│   ├── ReadPhotoHandler.cs        read_photo
│   ├── ReadCardHandler.cs         read_card
│   ├── ReadCertificatesHandler.cs read_certificates
│   ├── ReadCertificateHandler.cs  read_certificate
│   ├── GetPinStatusHandler.cs     get_pin_status
│   ├── VerifyPinHandler.cs        verify_pin
│   ├── SignChallengeHandler.cs    sign_challenge
│   ├── SignDataHandler.cs         sign_data
│   ├── SignHashHandler.cs         sign_hash
│   └── ValidateCertificateHandler.cs validate_certificate
│
└── services/
    ├── IEidService.cs             Full-coverage abstraction — handlers never touch the library directly
    └── EidService.cs              Facade: one method per library operation, session-per-call
```

---

## API Reference

All messages carry an optional `"id"` field (string) that is echoed verbatim in the response — use it to correlate concurrent requests. All binary fields (`challenge`, `data`, `hash`, `signature`, `certificate`, `photoBase64`) use **standard Base64 encoding**.

### Message types at a glance

| `type` | Required fields | Optional fields |
|---|---|---|
| `ping` | — | — |
| `get_readers` | — | — |
| `get_status` | — | — |
| `read_identity` | — | — |
| `read_address` | — | — |
| `read_photo` | — | — |
| `read_card` | — | `includePhoto` (bool, default `false`) |
| `read_certificates` | — | — |
| `read_certificate` | `kind` | — |
| `get_pin_status` | — | — |
| `verify_pin` | `pin` | — |
| `sign_challenge` | `pin`, `challenge` | `algorithm` |
| `sign_data` | `pin`, `data` | `algorithm` |
| `sign_hash` | `pin`, `hash` | `algorithm` |
| `validate_certificate` | `kind` | — |

**`kind` values:** `authentication` · `signature` · `intermediateCA` · `root`

**`algorithm` values:** `sha256` (default) · `sha384` · `sha512` · `sha1`

---

### `ping`

Health check — confirms the bridge process is running and responsive.

```jsonc
// Request
{ "id": "r1", "type": "ping" }

// Response
{ "id": "r1", "status": "ok", "service": "belgian-eid-bridge", "version": "1.0.0" }
```

---

### `get_readers`

Returns all connected PC/SC readers and their card state.

```jsonc
// Request
{ "id": "r2", "type": "get_readers" }

// Response
{
  "id": "r2",
  "readers": [
    { "name": "ACS ACR38U-CCID", "slotId": 0, "hasCardInserted": true }
  ]
}
```

---

### `get_status`

Returns aggregate reader and card presence — the fastest way to know whether the bridge can operate.

```jsonc
// Request
{ "id": "r3", "type": "get_status" }

// Response
{ "id": "r3", "readerPresent": true, "cardPresent": true }
```

---

### `read_identity`

Reads all personal data from the card identity file.

```jsonc
// Request
{ "id": "r4", "type": "read_identity" }

// Response
{
  "id": "r4",
  "cardNumber":          "592-0000000-80",
  "chipNumber":          "...",
  "lastName":            "Dupont",
  "firstNames":          "Jean",
  "nationalNumber":      "85061512345",
  "nationality":         "Belge",
  "birthLocation":       "Liège",
  "birthDateRaw":        "15 JUN 1985",
  "birthDate":           "1985-06-15",
  "gender":              "Male",
  "issuingMunicipality": "Liège",
  "validityBegin":       "2020-01-01",
  "validityEnd":         "2030-01-01"
}
```

---

### `read_address`

```jsonc
// Request
{ "id": "r5", "type": "read_address" }

// Response
{ "id": "r5", "street": "Rue de la Loi 42", "zipCode": "4000", "municipality": "Liège" }
```

---

### `read_photo`

```jsonc
// Request
{ "id": "r6", "type": "read_photo" }

// Response
{ "id": "r6", "photoBase64": "<Base64-JPEG | null>" }
```

---

### `read_card`

Reads identity + address + optional photo in a single round-trip.

```jsonc
// Request
{ "id": "r7", "type": "read_card", "includePhoto": true }

// Response
{
  "id": "r7",
  "identity": {
    "cardNumber": "...", "chipNumber": "...",
    "lastName": "Dupont", "firstNames": "Jean",
    "nationalNumber": "85061512345", "nationality": "Belge",
    "birthLocation": "Liège", "birthDateRaw": "15 JUN 1985", "birthDate": "1985-06-15",
    "gender": "Male", "issuingMunicipality": "Liège",
    "validityBegin": "2020-01-01", "validityEnd": "2030-01-01"
  },
  "address": { "street": "Rue de la Loi 42", "zipCode": "4000", "municipality": "Liège" },
  "photoBase64": "<Base64-JPEG>"
}
```

---

### `read_certificates`

Returns all four X.509 certificates as Base64-DER.

```jsonc
// Request
{ "id": "r8", "type": "read_certificates" }

// Response
{
  "id": "r8",
  "authentication": "<Base64-DER>",
  "signature":      "<Base64-DER>",
  "intermediateCA": "<Base64-DER>",
  "root":           "<Base64-DER>"
}
```

---

### `read_certificate`

Returns a single certificate by `kind`.

```jsonc
// Request
{ "id": "r9", "type": "read_certificate", "kind": "authentication" }

// Response
{ "id": "r9", "kind": "authentication", "certificate": "<Base64-DER>" }
```

---

### `get_pin_status`

```jsonc
// Request
{ "id": "r10", "type": "get_pin_status" }

// Response
{ "id": "r10", "remainingAttempts": 3, "isBlocked": false }
```

---

### `verify_pin`

```jsonc
// Request
{ "id": "r11", "type": "verify_pin", "pin": "1234" }

// Response (success)
{ "id": "r11", "verified": true }
```

---

### `sign_challenge`

Signs a server-issued challenge with the **authentication key**. Pass raw challenge bytes — the library computes the digest internally. Used for server-side identity verification (challenge-response authentication).

```jsonc
// Request
{
  "id": "r12", "type": "sign_challenge",
  "pin": "1234", "challenge": "<Base64-32-bytes>", "algorithm": "sha256"
}

// Response
{
  "id": "r12",
  "signature":   "<Base64>",
  "certificate": "<Base64-DER — authentication certificate>",
  "algorithm":   "sha256"
}
```

---

### `sign_data`

Signs small raw data with the **non-repudiation key** (signature key). The library hashes internally.

```jsonc
// Request
{ "id": "r13", "type": "sign_data", "pin": "1234", "data": "<Base64>", "algorithm": "sha256" }

// Response
{
  "id": "r13",
  "signature":   "<Base64>",
  "certificate": "<Base64-DER — signature certificate>",
  "algorithm":   "sha256",
  "signedAtUtc": "2026-01-15T10:30:00.000+00:00"
}
```

---

### `sign_hash`

Signs a **pre-computed hash** with the **non-repudiation key** — preferred for large documents to avoid transmitting the full payload over the messaging channel. Produces a legally-binding qualified electronic signature under Belgian law and eIDAS regulation.

```jsonc
// Request
{ "id": "r14", "type": "sign_hash", "pin": "1234", "hash": "<Base64-SHA-256>", "algorithm": "sha256" }

// Response
{
  "id": "r14",
  "signature":   "<Base64>",
  "certificate": "<Base64-DER — signature certificate>",
  "algorithm":   "sha256",
  "signedAtUtc": "2026-01-15T10:30:00.000+00:00"
}
```

---

### `validate_certificate`

Queries the official Belgian OCSP responder. Requires internet access.

```jsonc
// Request
{ "id": "r15", "type": "validate_certificate", "kind": "authentication" }

// Response
{
  "id": "r15",
  "kind":             "authentication",
  "status":           "Good",
  "isValid":          true,
  "producedAt":       "2026-01-15T10:30:00.000+00:00",
  "revocationTime":   null,
  "revocationReason": null
}
```

---

## Push Events — Reader Hot-Plug

The bridge runs a background monitor (`IEidReaderMonitor`, PKCS#11 polling every 500 ms). When reader or card state changes, a push message is sent to Chrome **without any prior request** — no polling needed on the extension side.

```jsonc
// Push message format — sent spontaneously by the bridge
{
  "type": "reader_state_changed",
  "eventKind": "cardInserted",
  "reader": {
    "name": "ACS ACR38U-CCID",
    "slotId": 0,
    "hasCardInserted": true
  }
}
```

| `eventKind` | Trigger |
|---|---|
| `readerConnected` | A USB reader was plugged in |
| `readerDisconnected` | A reader was unplugged |
| `cardInserted` | An eID card was inserted in a known reader |
| `cardRemoved` | An eID card was removed from a known reader |

```javascript
// Extension-side listener
port.onMessage.addListener(msg => {
  if (msg.type === 'reader_state_changed') {
    console.log(msg.eventKind, msg.reader.name);
    updateUI(msg.reader);
  }
});
```

---

## Error Responses

**Standard error** — returned for any unhandled exception, missing card, or missing reader:

```jsonc
{ "id": "...", "error": "No eID card is present in the reader." }
```

**PIN incorrect** — returned by `verify_pin` and all `sign_*` commands:

```jsonc
{
  "id": "...",
  "error": "Wrong PIN. Remaining attempts: 2.",
  "triesRemaining": 2,
  "blocked": false
}
```

**PIN blocked** — card is permanently locked:

```jsonc
{
  "id": "...",
  "error": "PIN is blocked.",
  "triesRemaining": 0,
  "blocked": true
}
```

> **Warning.** After 3 consecutive wrong PIN attempts the card is permanently blocked. Unblocking requires a visit to the citizen's municipality in person.

---

## Getting Started

### Prerequisites

| Requirement | Notes |
|---|---|
| Windows 10 (1809+) or 11 | HKCU registry key — no admin rights required for registration |
| .NET 8 SDK | Build-time only — self-contained publish ships the runtime |
| Belgian eID middleware | [eid.belgium.be](https://eid.belgium.be) — provides `beidpkcs11.dll` |
| PC/SC smart-card reader | Any ISO 7816-compatible reader |
| Google Chrome 111+ | Required for Native Messaging host registration |

### Build

```powershell
dotnet build BelgianEidBridge.csproj
```

### Publish (self-contained)

```powershell
dotnet publish BelgianEidBridge.csproj -c Release -r win-x64 --self-contained -o publish
```

### Register the native host

```powershell
.\register.ps1 -ExtensionId <your-32-char-extension-id>
```

The script writes `be.belgianeid.bridge.json` and creates the registry key under `HKCU\Software\Google\Chrome\NativeMessagingHosts\be.belgianeid.bridge`. Restart Chrome after the first registration.

```powershell
# Specify an explicit path if the binary is not in publish\
.\register.ps1 -ExtensionId <id> -ExePath "C:\Program Files\BelgianEid\bridge\BelgianEidBridge.exe"
```

---

## Extending the Bridge

Adding a new command requires exactly two steps.

**1. Create a handler — implement `IMessageHandler`**

```csharp
public sealed class MyCommandHandler : IMessageHandler
{
    private readonly IEidService _eid;

    public MyCommandHandler(IEidService eid) => _eid = eid;

    public string MessageType => "my_command";

    public async ValueTask<object> HandleAsync(JsonElement message, string? requestId)
    {
        var result = await _eid.DoSomethingAsync();
        return new { id = requestId, result };
    }
}
```

**2. Register it in `Program.cs` — add one line**

```csharp
IMessageHandler[] handlers =
[
    // existing handlers ...
    new MyCommandHandler(eidService),   // that's all
];
```

`MessageRouter` picks it up automatically. No other file changes required.

---

## Design Principles

| Principle | Applied where |
|---|---|
| **Single Responsibility** | Each handler manages exactly one command. `NativeMessagingHost` only runs the loop. `EidService` only wraps the library. |
| **Open/Closed** | `MessageRouter` is never modified. Adding a command means adding one file. |
| **Liskov Substitution** | All handlers are interchangeable via `IMessageHandler`. `EidService` is swappable via `IEidService`. |
| **Interface Segregation** | Five focused interfaces: `INativeMessageReader` · `INativeMessageWriter` · `IMessageRouter` · `IMessageHandler` · `IEidService`. |
| **Dependency Inversion** | `NativeMessagingHost` depends on abstractions. Handlers depend on `IEidService`. `Program.cs` is the sole composition root. |

---

## License

MIT — see [LICENSE](../LICENSE).
