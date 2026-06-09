using BelgianEid.Abstractions;
using BelgianEid.Exceptions;
using BelgianEid.Models;
using BelgianEid.Utilities;
using Microsoft.Extensions.Logging;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;

namespace BelgianEid.Implementations;

/// <summary>
/// PKCS#11 implementation of a session with an eID card.
/// Exposes only low-level primitives; all business logic lives in the services.
/// </summary>
internal sealed class EidSession(Pkcs11InteropFactories factories, ISlot slot, EidReader reader, ILogger logger) : IEidSession
{
    private readonly Pkcs11InteropFactories _factories = factories;
    private readonly ISlot _slot = slot;
    private readonly ISession _session = slot.OpenSession(SessionType.ReadOnly);
    private readonly ILogger _logger = logger;

    private bool _authenticated;
    private bool _disposed;

    /// <inheritdoc/>
    public EidReader Reader { get; } = reader;

    /// <inheritdoc/>
    public bool IsAuthenticated => _authenticated;

    /// <inheritdoc/>
    public bool IsCardPresent
    {
        get
        {
            try
            {
                return _slot.GetSlotInfo().SlotFlags.TokenPresent;
            }
            catch (Pkcs11Exception)
            {
                return false;
            }
        }
    }

    /// <inheritdoc/>
    public void Login(string pin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrEmpty(pin))
            throw new EidPinIncorrectException(GetPinStatus().RemainingAttempts);

        try
        {
            _session.Login(CKU.CKU_USER, pin);
            _authenticated = true;
        }
        catch (Pkcs11Exception ex) when (ex.RV == CKR.CKR_USER_ALREADY_LOGGED_IN)
        {
            _authenticated = true;
        }
        catch (Pkcs11Exception ex) when (ex.RV == CKR.CKR_PIN_LOCKED)
        {
            throw new EidPinBlockedException();
        }
        catch (Pkcs11Exception ex) when (ex.RV is CKR.CKR_PIN_INCORRECT or CKR.CKR_PIN_INVALID or CKR.CKR_PIN_LEN_RANGE)
        {
            var status = GetPinStatus();
            if (status.IsBlocked)
                throw new EidPinBlockedException();
            throw new EidPinIncorrectException(status.RemainingAttempts);
        }
        catch (Pkcs11Exception ex)
        {
            throw new EidCommunicationException(
                $"PIN verification failed (PKCS#11 code: {ex.RV}).", ex);
        }
    }

    /// <inheritdoc/>
    public void Logout()
    {
        if (_disposed || !_authenticated)
            return;
        try
        {
            _session.Logout();
        }
        catch (Pkcs11Exception ex)
        {
            _logger.LogDebug("PKCS#11 logout ignored: {Rv}", ex.RV);
        }
        finally
        {
            _authenticated = false;
        }
    }

    /// <inheritdoc/>
    public EidPinStatus GetPinStatus()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            ITokenFlags flags = _slot.GetTokenInfo().TokenFlags;
            int remaining =
                flags.UserPinLocked ? 0 :
                flags.UserPinFinalTry ? 1 :
                flags.UserPinCountLow ? 2 : 3;
            return new EidPinStatus { RemainingAttempts = remaining };
        }
        catch (Pkcs11Exception ex)
        {
            throw new EidCommunicationException("Unable to read PIN code status.", ex);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<EidDataObject> GetDataObjects()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var template = new List<IObjectAttribute>
        {
            _factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, (ulong)CKO.CKO_DATA),
        };

        var results = new List<EidDataObject>();
        try
        {
            foreach (IObjectHandle handle in _session.FindAllObjects(template))
            {
                List<IObjectAttribute> attrs = _session.GetAttributeValue(
                    handle, [CKA.CKA_LABEL, CKA.CKA_VALUE]);

                results.Add(new EidDataObject
                {
                    Label = SafeString(attrs[0]),
                    Value = attrs[1].GetValueAsByteArray() ?? [],
                });
            }
        }
        catch (Pkcs11Exception ex)
        {
            throw new EidCommunicationException("Failed to read data objects from the card.", ex);
        }

        return results;
    }

    /// <inheritdoc/>
    public byte[] GetCertificateRaw(EidCertificateKind kind)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string label = EidObjectLabels.ForCertificate(kind);
        var template = new List<IObjectAttribute>
        {
            _factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, (ulong)CKO.CKO_CERTIFICATE),
            _factories.ObjectAttributeFactory.Create(CKA.CKA_LABEL, label),
        };

        try
        {
            List<IObjectHandle> handles = _session.FindAllObjects(template);
            if (handles.Count == 0)
                throw new EidDataNotFoundException(
                    $"Certificate '{label}' not found on the card.");

            List<IObjectAttribute> attrs = _session.GetAttributeValue(handles[0], [CKA.CKA_VALUE]);
            return attrs[0].GetValueAsByteArray()
                   ?? throw new EidDataNotFoundException($"Certificate '{label}' is empty.");
        }
        catch (Pkcs11Exception ex)
        {
            throw new EidCommunicationException($"Failed to read certificate '{label}'.", ex);
        }
    }

    /// <inheritdoc/>
    public byte[] Sign(EidPrivateKeyKind key, byte[] data, EidSignatureMechanism mechanism)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(data);

        if (!_authenticated)
            throw new EidCommunicationException(
                "The session must be authenticated (Login) before signing.");

        string label = EidObjectLabels.ForPrivateKey(key);
        var template = new List<IObjectAttribute>
        {
            _factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, (ulong)CKO.CKO_PRIVATE_KEY),
            _factories.ObjectAttributeFactory.Create(CKA.CKA_LABEL, label),
        };

        try
        {
            List<IObjectHandle> keys = _session.FindAllObjects(template);
            if (keys.Count == 0)
                throw new EidDataNotFoundException($"Private key '{label}' not found on the card.");

            // If an explicit mechanism was specified (not Auto)
            if (mechanism != EidSignatureMechanism.Auto)
            {
                CKM mech = MapMechanism(mechanism);
                ValidateDataLengthForMechanism(mech, data);
                using IMechanism pkcs11Mech = _factories.MechanismFactory.Create(mech);
                return _session.Sign(pkcs11Mech, keys[0], data);
            }

            // Automatic mode: try ECDSA mechanisms first, then RSA
            var mechanismsToTry = new List<(CKM Mechanism, int ExpectedHashLength, bool IsEcdsa)>
            {
                // Modern cards (ECDSA)
                (CKM.CKM_ECDSA_SHA512, 64, true),
                (CKM.CKM_ECDSA_SHA384, 48, true),
                (CKM.CKM_ECDSA_SHA256, 32, true),
                (CKM.CKM_ECDSA,        -1, true),   // external hash, no size check

                // Compatibility with older cards (RSA)
                (CKM.CKM_SHA256_RSA_PKCS, 32, false),
                (CKM.CKM_SHA1_RSA_PKCS,   20, false),
                (CKM.CKM_RSA_PKCS,        -1, false)
            };

            Exception? lastException = null;
            foreach (var (mech, expectedLen, isEcdsa) in mechanismsToTry)
            {
                try
                {
                    // Check hash size if expected
                    if (expectedLen != -1 && data.Length != expectedLen)
                    {
                        _logger.LogDebug("[size] {DataLen} != {ExpectedLen} for {Mechanism}", data.Length, expectedLen, mech);
                        continue;
                    }

                    _logger.LogDebug("Trying {Mechanism} (ECDSA={IsEcdsa})", mech, isEcdsa);
                    using IMechanism pkcs11Mech = _factories.MechanismFactory.Create(mech);
                    byte[] signature = _session.Sign(pkcs11Mech, keys[0], data);
                    _logger.LogInformation("Signature succeeded with {Mechanism}", mech);
                    return signature;
                }
                catch (Pkcs11Exception ex) when (ex.RV == CKR.CKR_MECHANISM_INVALID)
                {
                    _logger.LogDebug("Mechanism {Mechanism} not supported", mech);
                    lastException = ex;
                    continue;
                }
                catch (Pkcs11Exception ex) when (ex.RV == CKR.CKR_DATA_LEN_RANGE)
                {
                    _logger.LogDebug("Invalid data length for {Mechanism}", mech);
                    lastException = ex;
                    continue;
                }
                catch (Pkcs11Exception ex)
                {
                    _logger.LogDebug("PKCS#11 error {Rv} with {Mechanism}", ex.RV, mech);
                    lastException = ex;
                    continue;
                }
            }

            throw lastException is null
                ? new EidCommunicationException(
                    $"No supported signature mechanism found for key '{label}'.")
                : new EidCommunicationException(
                    $"No supported signature mechanism found for key '{label}'. " +
                    $"Last error: {lastException.Message}", lastException);
        }
        catch (Pkcs11Exception ex) when (ex.RV == CKR.CKR_MECHANISM_INVALID)
        {
            throw new EidCommunicationException(
                $"The specified mechanism is not supported by the card for key '{label}'. " +
                "Use EidSignatureMechanism.EcdsaSha256, EcdsaSha384 or EcdsaSha512.", ex);
        }
        catch (Pkcs11Exception ex)
        {
            throw new EidCommunicationException(
                $"Signing failed with key '{label}' (PKCS#11 code: {ex.RV}).", ex);
        }
    }

    private static CKM MapMechanism(EidSignatureMechanism mechanism)
    {
        return mechanism switch
        {
            // ECDSA
            EidSignatureMechanism.EcdsaSha256 => CKM.CKM_ECDSA_SHA256,
            EidSignatureMechanism.EcdsaSha384 => CKM.CKM_ECDSA_SHA384,
            EidSignatureMechanism.EcdsaSha512 => CKM.CKM_ECDSA_SHA512,
            EidSignatureMechanism.Ecdsa => CKM.CKM_ECDSA,
            // RSA (compatibility)
            EidSignatureMechanism.Sha256RsaPkcs1 => CKM.CKM_SHA256_RSA_PKCS,
            EidSignatureMechanism.Sha1RsaPkcs1 => CKM.CKM_SHA1_RSA_PKCS,
            EidSignatureMechanism.RsaPkcs1 => throw new NotSupportedException(
                "RSA_PKCS without hashing is not supported by modern cards."),
            EidSignatureMechanism.Auto => throw new ArgumentException(
                "Auto must be handled before calling MapMechanism"),
            _ => throw new ArgumentOutOfRangeException(nameof(mechanism), mechanism, "Unknown mechanism")
        };
    }

    private static void ValidateDataLengthForMechanism(CKM mechanism, byte[] data)
    {
        int expectedLength = mechanism switch
        {
            CKM.CKM_SHA256_RSA_PKCS => 32,
            CKM.CKM_SHA1_RSA_PKCS => 20,
            _ => -1
        };
        if (expectedLength != -1 && data.Length != expectedLength)
        {
            throw new ArgumentException(
                $"Data must be a {expectedLength}-byte hash for mechanism {mechanism}.");
        }
    }

    private static string SafeString(IObjectAttribute attribute)
    {
        try { return attribute.GetValueAsString() ?? string.Empty; }
        catch { return string.Empty; }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            if (_authenticated)
                _session.Logout();
        }
        catch (Pkcs11Exception) { /* ignored: the card may have been removed */ }
        finally
        {
            _session.Dispose();
        }
    }
}
