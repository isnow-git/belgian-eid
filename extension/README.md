# Belgian eID Chrome Extension

[![Manifest V3](https://img.shields.io/badge/Manifest-V3-4285F4?style=flat-square&logo=googlechrome)](https://developer.chrome.com/docs/extensions/mv3/)
[![Version](https://img.shields.io/badge/version-1.0.0-brightgreen?style=flat-square)](manifest.json)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](../LICENSE)

**Chrome MV3 extension** that acts as a secure bridge between a web app and the local [Belgian eID Bridge](../bridge/README.md). It relays `window.postMessage` calls from the web page to the native host over Chrome's Native Messaging channel, and forwards responses and push events back to the page.

> **Reference implementation.** This extension ships as a reusable template. It
> is **not** tied to any specific website: its `manifest.json` declares
> placeholder origins (`http://localhost:3000`, `https://your-app.example`) that
> you replace with your own — see [Point the extension at your web app](#point-the-extension-at-your-web-app).

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [postMessage API](#postmessage-api)
- [Push Events — Reader Hot-Plug](#push-events--reader-hot-plug)
- [Error Handling](#error-handling)
- [Developer Installation](#developer-installation)
- [Web App Integration Guide](#web-app-integration-guide)
- [Design Principles](#design-principles)
- [License](#license)

---

## Overview

```
Web page (your portal)
    │
    │  window.postMessage({ type: 'SIGN_CHALLENGE', id: '...', pin, challenge })
    ▼
inject.js       MAIN world  — stamps window.__EID_EXTENSION_VERSION__
content.js      ISOLATED    — relays postMessage ↔ chrome.runtime.sendMessage
    │
    │  chrome.runtime.sendMessage({ type: 'SIGN_CHALLENGE', ... })
    ▼
background/     Service Worker
    ├── NativeClient.js       manages the native port lifecycle
    ├── EidApi.js             typed operations over NativeClient
    └── MessageDispatcher.js  routes incoming sendMessage calls to EidApi
    │
    │  Chrome Native Messaging — JSON / stdin / stdout
    ▼
BelgianEidBridge.exe          reads the card via PKCS#11
```

The extension never stores card data. It is a pure transport layer.

---

## Architecture

```
extension/
├── manifest.json
├── shared/
│   └── constants.js          Shared constants (message types, timeouts, host name)
├── background/               Service Worker — ES modules
│   ├── index.js              Composition root — wires NativeClient, EidApi, MessageDispatcher
│   ├── NativeClient.js       Port lifecycle, request correlation, push-event forwarding
│   ├── EidApi.js             Typed facade over NativeClient — one method per operation
│   └── MessageDispatcher.js  Routes chrome.runtime.onMessage → EidApi methods
├── content/
│   └── content.js            Relays window.postMessage ↔ chrome.runtime.sendMessage
├── inject/
│   └── inject.js             Sets window.__EID_EXTENSION_VERSION__ in the MAIN world
└── popup/
    ├── popup.html
    ├── popup.css
    └── popup.js              Displays bridge status and live card state
```

**Layer responsibilities**

```
┌──────────────────────────────────────────────┐
│  MessageDispatcher   routing                 │
├──────────────────────────────────────────────┤
│  EidApi              typed operations        │
├──────────────────────────────────────────────┤
│  NativeClient        transport + correlation │
└──────────────────────────────────────────────┘
```

Each layer depends only on the interface of the layer below (Dependency Inversion). `EidApi` never calls `chrome.runtime` directly — it goes through `NativeClient`.

---

## postMessage API

All messages are sent from the web page via `window.postMessage` and received as `window.addEventListener('message', ...)`. Every request includes a unique `id` field that is echoed in the response so requests can be correlated. Responses carry `__eidFrom: 'extension'` to distinguish them from other `postMessage` traffic.

**Response type convention:** `{TYPE}_RESPONSE` (e.g. `READ_CARD` → `READ_CARD_RESPONSE`).

### Message types at a glance

| `type` | Required fields | Response fields |
|---|---|---|
| `CHECK_STATUS` | — | `bridgeActive`, `readerPresent`, `cardPresent` |
| `GET_READERS` | — | `readers: [{ name, slotId, hasCardInserted }]` |
| `READ_CARD` | — | `identity`, `address`, `photoBase64?` |
| `READ_IDENTITY` | — | identity fields |
| `GET_PIN_STATUS` | — | `remainingAttempts`, `isBlocked` |
| `VERIFY_PIN` | `pin` | `verified: true` |
| `SIGN_CHALLENGE` | `pin`, `challenge` (Base64) | `signature`, `certificate`, `algorithm` |
| `SIGN_HASH` | `pin`, `hash` (Base64) | `signature`, `certificate`, `algorithm`, `signedAtUtc` |
| `SIGN_DATA` | `pin`, `data` (Base64) | `signature`, `certificate`, `algorithm`, `signedAtUtc` |

`SIGN_CHALLENGE`, `SIGN_HASH`, and `SIGN_DATA` accept an optional `algorithm` field (`'sha256'` default · `'sha384'` · `'sha512'`).

### Examples

```javascript
// Check bridge status
window.postMessage({ type: 'CHECK_STATUS', id: '1' }, '*');

// Read the full card in one call
window.postMessage({ type: 'READ_CARD', id: '2', includePhoto: false }, '*');

// Authenticate with a server challenge
window.postMessage({
  type: 'SIGN_CHALLENGE',
  id: '3',
  pin: '1234',
  challenge: '<Base64-32-bytes>',   // challenge issued by the server
}, '*');

// Sign a document hash (qualified electronic signature)
window.postMessage({
  type: 'SIGN_HASH',
  id: '4',
  pin: '1234',
  hash: '<Base64-SHA-256>',
}, '*');

// Listen for responses
window.addEventListener('message', (event) => {
  if (event.data?.__eidFrom !== 'extension') return;
  const { type, id, error, ...payload } = event.data;
  // type === 'READ_CARD_RESPONSE', id === '2', payload === { identity, address, ... }
});
```

---

## Push Events — Reader Hot-Plug

The extension forwards unsolicited push events from the bridge when hardware state changes. No polling needed — the web page is notified in real time.

```javascript
window.addEventListener('message', (event) => {
  if (event.data?.__eidFrom !== 'extension') return;
  if (event.data.type !== 'READER_STATE_CHANGED') return;

  const { eventKind, reader } = event.data;
  // eventKind: 'readerConnected' | 'readerDisconnected' | 'cardInserted' | 'cardRemoved'
  // reader:    { name, slotId, hasCardInserted }
  console.log(eventKind, reader.name);
});
```

---

## Error Handling

Any failure is returned in the response as `{ error: 'message string' }`:

```javascript
window.addEventListener('message', (event) => {
  if (event.data?.__eidFrom !== 'extension') return;
  if (event.data.error) {
    console.error('eID error:', event.data.error);
    return;
  }
  // ... handle success
});
```

**PIN-specific error fields**

```jsonc
// Wrong PIN
{ "type": "VERIFY_PIN_RESPONSE", "id": "...", "__eidFrom": "extension",
  "error": "Wrong PIN.", "triesRemaining": 2, "blocked": false }

// PIN blocked
{ "type": "VERIFY_PIN_RESPONSE", "id": "...", "__eidFrom": "extension",
  "error": "PIN is blocked.", "triesRemaining": 0, "blocked": true }
```

---

## Developer Installation

### Prerequisites

- Google Chrome 111+
- [Belgian eID Bridge](../bridge/README.md) built and registered:

```powershell
cd ../bridge
dotnet publish BelgianEidBridge.csproj -c Release -r win-x64 --self-contained -o publish
.\register.ps1 -ExtensionId <your-extension-id>
```

### Load the unpacked extension

1. Open `chrome://extensions`
2. Enable **Developer mode** (top-right toggle)
3. Click **Load unpacked** → select the `extension/` folder
4. Copy the extension ID shown on the card
5. Pass the ID to `register.ps1` (if not done already) and restart Chrome

> The committed `manifest.json` has **no** `"key"` field, so Chrome assigns a
> fresh extension ID on first load. Use that ID with `register.ps1`. For a
> stable ID across machines (production), let `installer/build.ps1` generate a
> signing key and inject the matching public key — see
> [installer/README.md](../installer/README.md).

### Point the extension at your web app

The extension only activates on the origins listed in `manifest.json`. Replace
the placeholders with your own domain in **three** places, then reload the
extension:

```jsonc
// manifest.json
"host_permissions": [
  "http://localhost:3000/*",          // dev — keep or change
  "https://your-app.example/*"        // ← your production origin
],
"content_scripts": [
  { "matches": ["http://localhost:3000/*", "https://your-app.example/*"], "js": ["inject/inject.js"],  "world": "MAIN", "run_at": "document_start" },
  { "matches": ["http://localhost:3000/*", "https://your-app.example/*"], "js": ["content/content.js"],                 "run_at": "document_start" }
]
```

Only pages served from these origins can talk to the bridge — this is the
extension's primary security boundary.

---

## Web App Integration Guide

### Detect the extension

`inject.js` sets a marker on `window` in the MAIN world before page scripts run:

```javascript
if (window.__EID_EXTENSION_VERSION__) {
  console.log('Belgian eID extension detected:', window.__EID_EXTENSION_VERSION__);
}
```

### Promise-based utility wrapper

```javascript
function sendToEid(type, params = {}) {
  return new Promise((resolve, reject) => {
    const id = crypto.randomUUID();

    function handler(event) {
      if (event.data?.__eidFrom !== 'extension') return;
      if (event.data.type !== `${type}_RESPONSE` || event.data.id !== id) return;
      window.removeEventListener('message', handler);
      if (event.data.error) {
        const err = Object.assign(new Error(event.data.error), event.data);
        reject(err);
      } else {
        resolve(event.data);
      }
    }

    window.addEventListener('message', handler);
    window.postMessage({ type, id, ...params }, '*');
  });
}

// Usage
const { bridgeActive, readerPresent, cardPresent } = await sendToEid('CHECK_STATUS');
const { identity, address, photoBase64 }           = await sendToEid('READ_CARD', { includePhoto: true });
const { signature, certificate }                   = await sendToEid('SIGN_CHALLENGE', { pin, challenge });
const { signature: docSignature }                  = await sendToEid('SIGN_HASH', { pin, hash: hashBase64 });
```

### Minimal background.js usage (for custom extensions)

```javascript
const HOST = 'be.belgianeid.bridge';
let port    = null;
const pending = new Map();
let nextId  = 0;

function connect() {
  port = chrome.runtime.connectNative(HOST);
  port.onMessage.addListener(msg => {
    const handler = pending.get(msg.id);
    if (!handler) return;                        // push event — handle separately
    pending.delete(msg.id);
    msg.error ? handler.reject(new Error(msg.error)) : handler.resolve(msg);
  });
  port.onDisconnect.addListener(() => {
    port = null;
    pending.forEach(({ reject }) => reject(new Error('Bridge disconnected')));
    pending.clear();
  });
}

function send(payload) {
  if (!port) connect();
  return new Promise((resolve, reject) => {
    const id = `req-${nextId++}`;
    pending.set(id, { resolve, reject });
    port.postMessage({ ...payload, id });
  });
}

// Public API
export const eid = {
  ping:               ()                     => send({ type: 'ping' }),
  getStatus:          ()                     => send({ type: 'get_status' }),
  readCard:           (includePhoto = false) => send({ type: 'read_card', includePhoto }),
  verifyPin:          (pin)                  => send({ type: 'verify_pin', pin }),
  signChallenge:      (pin, challenge, algo) => send({ type: 'sign_challenge', pin, challenge, algorithm: algo }),
  signHash:           (pin, hash, algo)      => send({ type: 'sign_hash',      pin, hash,      algorithm: algo }),
  validateCertificate:(kind)                 => send({ type: 'validate_certificate', kind }),
};
```

---

## Design Principles

| Principle | Applied where |
|---|---|
| **Single Responsibility** | `NativeClient` manages only the port. `EidApi` manages only operations. `MessageDispatcher` manages only routing. `content.js` manages only the postMessage relay. |
| **Open/Closed** | Adding a command = one line in `MessageDispatcher` + one method in `EidApi`. No existing file changes. |
| **Liskov Substitution** | `EidApi` can be replaced with a mock for testing without changing `MessageDispatcher`. |
| **Interface Segregation** | `NativeClient` exposes only `send()` and `onPush()` — no Chrome API surface leaks upward. |
| **Dependency Inversion** | `MessageDispatcher` depends on `EidApi` (not `NativeClient`). `background/index.js` is the sole composition root. |

---

## License

MIT — see [LICENSE](../LICENSE).
