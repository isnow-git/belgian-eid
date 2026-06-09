# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project
follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] — 2026-06-09

First public release.

### Added

- **library** — .NET 8 class library for the Belgian eID card: reader
  detection and hot-plug monitoring, identity / address / photo reading, X.509
  certificate reading, PIN verification, electronic signature, challenge
  authentication, and OCSP / CRL revocation checking. Ships a high-level
  `IEidClient` facade and `services.AddBelgianEid()` for dependency injection.
- **bridge** — Chrome Native Messaging host exposing every library operation as
  typed JSON messages over stdio.
- **extension** — Chrome MV3 extension bridging web pages to the native host
  via `window.postMessage`, shipped as a reusable reference implementation.
- **installer** — Inno Setup 6 pipeline that deploys the bridge and extension
  silently on Windows.
- **samples** — console walkthrough (read · PIN · sign · OCSP).
- **tests** — 17 OCSP unit tests that need no card, reader, or network.

[1.0.0]: https://github.com/isnow-git/belgian-eid/releases/tag/v1.0.0
