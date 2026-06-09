using BelgianEid.Abstractions;
using BelgianEid.Configuration;
using BelgianEid.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;

namespace BelgianEid.Implementations;

/// <summary>
/// Loads the native PKCS#11 library for the eID middleware on demand
/// and guarantees a single shared instance (thread-safe).
/// </summary>
public sealed class Pkcs11LibraryProvider : IPkcs11LibraryProvider
{
    private readonly BelgianEidOptions _options;
    private readonly ILogger<Pkcs11LibraryProvider> _logger;
    private readonly Pkcs11InteropFactories _factories = new();
    private readonly object _lock = new();

    private IPkcs11Library? _library;
    private bool _disposed;

    public Pkcs11LibraryProvider(
        IOptions<BelgianEidOptions> options,
        ILogger<Pkcs11LibraryProvider>? logger = null)
    {
        _options = options.Value;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<Pkcs11LibraryProvider>.Instance;
    }

    /// <inheritdoc/>
    public IPkcs11Library GetLibrary()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_library is not null)
            return _library;

        lock (_lock)
        {
            if (_library is not null)
                return _library;

            string path = ResolveLibraryPath();
            _logger.LogInformation("Loading PKCS#11 library: {Path}", path);

            try
            {
                _library = _factories.Pkcs11LibraryFactory.LoadPkcs11Library(
                    _factories, path, AppType.MultiThreaded);
            }
            catch (Exception ex)
            {
                throw new EidConfigurationException(
                    $"Unable to load the eID PKCS#11 library ('{path}'). " +
                    "Verify that beidpkcs11.dll is present (in the 'native' folder or the installed middleware).", ex);
            }

            return _library;
        }
    }

    /// <summary>
    /// Resolves the native library path:
    /// explicit path first, then the library bundled in <c>native/&lt;rid&gt;/</c>, then the default system name.
    /// </summary>
    private string ResolveLibraryPath()
    {
        // 1) Explicitly configured path.
        if (!string.IsNullOrWhiteSpace(_options.Pkcs11LibraryPath))
        {
            if (!File.Exists(_options.Pkcs11LibraryPath))
                throw new EidConfigurationException(
                    $"The configured PKCS#11 path was not found: '{_options.Pkcs11LibraryPath}'.");
            return _options.Pkcs11LibraryPath;
        }

        // 2) Library bundled next to the application.
        string fileName = BelgianEidOptions.GetDefaultLibraryFileName();
        string rid = BelgianEidOptions.GetCurrentRuntimeIdentifier();
        string bundled = Path.Combine(
            AppContext.BaseDirectory, _options.BundledNativeDirectory, rid, fileName);

        if (File.Exists(bundled))
            return bundled;

        // 3) Fallback: let the operating system resolve the name.
        _logger.LogWarning(
            "No bundled native DLL found ({Bundled}). Attempting via the system middleware.", bundled);
        return fileName;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _library?.Dispose();
        _library = null;
    }
}
