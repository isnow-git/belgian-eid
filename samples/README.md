# BelgianEid — Console Sample

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](../LICENSE)

End-to-end walkthrough of the [BelgianEid library](../library/README.md) against a physical eID card. Runs as a single-file top-level-statements console application.

---

## What it demonstrates

| Step | Operation |
|------|-----------|
| 1 | Enumerate connected PC/SC readers and open a session on the first card found |
| 2 | Query remaining PIN attempts, then verify the PIN with masked console input |
| 3 | Generate a random 32-byte challenge, sign it with the authentication key, and validate the certificate via OCSP |
| 4 | Read identity (name, birth date, nationality, NRN), address, and photo — save the photo to `photo.jpeg` |

---

## Prerequisites

| Requirement | Version |
|---|---|
| .NET 8 SDK | 8.0+ |
| PC/SC smart-card reader | Any ISO 7816-compatible reader |
| Belgian eID card | Inserted in the reader |
| Belgian eID middleware (`beidpkcs11`) | Bundled under `library/native/win-x64/` for Windows 64-bit |

---

## Run

```powershell
dotnet run --project samples/BelgianEid.ConsoleSample.csproj
```

Or from the solution root:

```powershell
dotnet run --project samples
```

---

## Expected output

```
=== Belgian eID library demo ===

[1] Detecting readers...
    - ACS ACR38U-CCID 0 [card present]
    Session opened on: ACS ACR38U-CCID 0

[2] PIN verification
    Remaining attempts: 3
    Enter the card PIN: ****
    PIN correct.

[3] Challenge authentication + OCSP validation
    Challenge signed (256 bytes).
    Authentication certificate: CN=Jan Janssen (Authentication), ...
    OCSP certificate status at 19/05/2026 (valid: yes)

[4] Reading personal data
    Last name   : Janssen
    First names : Jan
    Born        : 01/01/1990 in Brussels
    Nationality : Belgian
    NRN         : 90010112345
    Address     : Rue de la Loi 16, 1000 Brussels
    Photo       : 12345 bytes JPEG
    Photo saved : C:\...\photo.jpeg

=== Demo completed successfully ===
```

---

## Error handling

All typed exceptions from `BelgianEid.Exceptions` are caught:

| Exception | Meaning |
|---|---|
| `EidReaderNotFoundException` | No PC/SC reader detected |
| `EidCardNotPresentException` | No eID card in the reader |
| `EidPinIncorrectException` | Wrong PIN — remaining attempts displayed |
| `EidPinBlockedException` | PIN blocked after 3 failures |
| `EidConfigurationException` | `beidpkcs11` not found or cannot be loaded |
| `EidException` | Any other card error |

---

## License

MIT — see [LICENSE](../LICENSE)
