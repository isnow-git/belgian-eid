# Third-party notices

`BelgianEid` is distributed under the [MIT License](LICENSE). It depends on,
and in one case bundles, third-party components that keep their own licenses.
This file lists them and the obligations that apply when you redistribute the
project.

| Component                  | Role                                        | License    |
| -------------------------- | ------------------------------------------- | ---------- |
| `beidpkcs11`               | Belgian eID middleware (native PKCS#11)     | LGPLv3     |
| Pkcs11Interop              | Managed PKCS#11 bindings                     | Apache-2.0 |
| BouncyCastle.Cryptography  | OCSP / ASN.1 / X.509 helpers                 | MIT        |
| Microsoft.Extensions.\*    | Dependency injection, options, logging, HTTP | MIT        |

## `beidpkcs11` — LGPLv3 (important)

The file `library/native/win-x64/beidpkcs11.dll` is part of the official
Belgian eID middleware published by the Belgian government (SPF BOSA / Fedict)
and is licensed under the **GNU Lesser General Public License v3**.

It is bundled here only to make the library work out of the box on Windows. If
you redistribute a build that includes this binary, the LGPLv3 requires you to:

1. provide access to the LGPL-covered source code, and
2. allow end users to relink or replace the binary with their own build.

Source code: <https://github.com/Fedict/eid-mw>

If you prefer not to redistribute the binary, install the eID middleware
system-wide instead and remove `library/native/**`. The library will then load
`beidpkcs11` from the operating system search path. See the comment in
`library/BelgianEid.csproj` and `.gitignore`.

## Apache-2.0 and MIT components

Pkcs11Interop, BouncyCastle.Cryptography and the `Microsoft.Extensions.*`
packages are restored from NuGet and are not redistributed in source form by
this repository. Their licenses permit redistribution under their respective
terms; refer to each package on <https://www.nuget.org> for the full text.
