using System.Runtime.InteropServices;

namespace BelgianEid.Configuration;

/// <summary>
/// Configuration options for the Belgian eID library.
/// Centralises all adjustable parameters (Options pattern).
/// </summary>
public sealed class BelgianEidOptions
{
    /// <summary>Recommended section name for binding from appsettings.json.</summary>
    public const string SectionName = "BelgianEid";

    /// <summary>
    /// Explicit path to the eID middleware PKCS#11 library.
    /// If <c>null</c>, the path is resolved automatically:
    /// 1) <c>native/&lt;rid&gt;/</c> folder next to the application (bundled DLL);
    /// 2) if not found, the standard name is left to the operating system.
    /// </summary>
    public string? Pkcs11LibraryPath { get; set; }

    /// <summary>
    /// Root folder containing the bundled native libraries, organised by RID
    /// (<c>win-x64</c>, <c>linux-x64</c>, <c>osx-x64</c>...). Default: <c>native</c>.
    /// </summary>
    public string BundledNativeDirectory { get; set; } = "native";

    /// <summary>
    /// URL of the official Belgian OCSP responder used to verify certificate revocation.
    /// </summary>
    public Uri OcspResponderUrl { get; set; } = new("http://ocsp.eidpki.belgium.be/eid/0");

    /// <summary>
    /// URL of the CRL (Certificate Revocation List) used as a fallback when OCSP
    /// is unavailable. Default: <c>http://crl.eid.belgium.be/eidc201108.crl</c>.
    /// </summary>
    public string CrlUrl { get; set; } = "http://crl.eid.belgium.be/eidc201108.crl";

    /// <summary>Maximum wait time for an OCSP response.</summary>
    public TimeSpan OcspTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Labels (CKA_LABEL) of the PKCS#11 data objects to prefer when reading.
    /// If no object matches, heuristic content-based detection takes over.
    /// </summary>
    public DataObjectLabels DataObjectLabels { get; set; } = new();

    /// <summary>
    /// Returns the default PKCS#11 library file name for the current platform.
    /// </summary>
    public static string GetDefaultLibraryFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "beidpkcs11.dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "libbeidpkcs11.dylib";
        return "libbeidpkcs11.so.0";
    }

    /// <summary>
    /// Returns the simplified runtime identifier (RID) of the current system,
    /// used to locate the bundled native DLL.
    /// </summary>
    public static string GetCurrentRuntimeIdentifier()
    {
        string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win"
                  : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx"
                  : "linux";
        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            _ => "x64",
        };
        return $"{os}-{arch}";
    }
}

/// <summary>
/// Expected labels of data objects stored on the eID card.
/// </summary>
public sealed class DataObjectLabels
{
    /// <summary>Label of the object containing the identity file (TLV).</summary>
    public string Identity { get; set; } = "identity";

    /// <summary>Label of the object containing the address file (TLV).</summary>
    public string Address { get; set; } = "address";

    /// <summary>Label of the object containing the photo (JPEG).</summary>
    public string Photo { get; set; } = "photo_file";
}
