using BelgianEid.Abstractions;
using BelgianEid.Configuration;
using BelgianEid.Implementations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BelgianEid.DependencyInjection;

/// <summary>
/// Extension methods for registering the Belgian eID library in a
/// <see cref="IServiceCollection"/> dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all services of the Belgian eID library.
    /// </summary>
    /// <param name="services">The service container to configure.</param>
    /// <param name="configure">
    /// Optional action for configuring <see cref="BelgianEidOptions"/>
    /// (native DLL path, OCSP responder URL, object labels...).
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/>, for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddBelgianEid(options =>
    /// {
    ///     options.OcspTimeout = TimeSpan.FromSeconds(15);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddBelgianEid(
        this IServiceCollection services,
        Action<BelgianEidOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // 1. Centralised configuration (Options pattern).
        OptionsServiceCollectionExtensions.AddOptions<BelgianEidOptions>(services)
            .Configure(o => configure?.Invoke(o))
            .Validate(o => o.OcspResponderUrl is not null,
                "The OCSP responder URL cannot be null.");

        // 2. Native PKCS#11 library loading (single shared instance).
        services.TryAddSingleton<IPkcs11LibraryProvider, Pkcs11LibraryProvider>();

        // 3. Reader detection / session opening.
        services.TryAddSingleton<IEidReaderService, EidReaderService>();

        // 4. Specialised business services (one responsibility each).
        services.TryAddSingleton<IEidIdentityService, EidIdentityService>();
        services.TryAddSingleton<IEidCertificateService, EidCertificateService>();
        services.TryAddSingleton<IEidPinService, EidPinService>();
        services.TryAddSingleton<IEidSignatureService, EidSignatureService>();
        services.TryAddSingleton<IEidAuthenticationService, EidAuthenticationService>();

        // 5. OCSP validation: typed HttpClient managed by HttpClientFactory.
        services.AddHttpClient<IOcspValidationService, OcspValidationService>();
        services.AddHttpClient<ICrlValidationService, CrlValidationService>();

        // 6. High-level facade.
        services.TryAddSingleton<IEidClient, EidClient>();

        // 7. Hot-plug reader monitoring (polling every 500 ms).
        services.TryAddSingleton<IEidReaderMonitor, EidReaderMonitor>();

        return services;
    }
}
