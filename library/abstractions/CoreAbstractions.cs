using BelgianEid.Models;
using Net.Pkcs11Interop.HighLevelAPI;

namespace BelgianEid.Abstractions;

/// <summary>
/// Provides the shared instance of the native PKCS#11 library for the eID middleware.
/// This abstraction isolates the loading of the native DLL from the rest of the library.
/// </summary>
public interface IPkcs11LibraryProvider : IDisposable
{
    /// <summary>
    /// Returns (loading it on demand) the PKCS#11 library.
    /// </summary>
    /// <exception cref="Exceptions.EidConfigurationException">
    /// The native library cannot be found or cannot be loaded.
    /// </exception>
    IPkcs11Library GetLibrary();
}

/// <summary>
/// Represents an active session with an eID card inserted in a reader.
/// This is the low-level (transport) layer: it exposes primitives, without business logic.
/// Implemented on top of PKCS#11, but fully mockable for unit tests.
/// </summary>
public interface IEidSession : IDisposable
{
    /// <summary>The reader associated with this session.</summary>
    EidReader Reader { get; }

    /// <summary>Indicates whether a card is still present in the reader.</summary>
    bool IsCardPresent { get; }

    /// <summary>Indicates whether the session is currently authenticated by PIN.</summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Authenticates the session with the cardholder's PIN (C_Login operation).
    /// </summary>
    /// <exception cref="Exceptions.EidPinIncorrectException">The PIN is incorrect.</exception>
    /// <exception cref="Exceptions.EidPinBlockedException">The PIN is blocked.</exception>
    void Login(string pin);

    /// <summary>Ends the authentication of the session (C_Logout operation).</summary>
    void Logout();

    /// <summary>
    /// Returns the PIN code status (number of remaining attempts).
    /// </summary>
    EidPinStatus GetPinStatus();

    /// <summary>
    /// Reads all data objects (CKO_DATA) present on the card.
    /// </summary>
    IReadOnlyList<EidDataObject> GetDataObjects();

    /// <summary>
    /// Reads the raw (DER) content of an X.509 certificate from the card.
    /// </summary>
    /// <exception cref="Exceptions.EidDataNotFoundException">The certificate was not found.</exception>
    byte[] GetCertificateRaw(EidCertificateKind kind);

    /// <summary>
    /// Signs data with a private key from the card. The session must be authenticated.
    /// </summary>
    /// <exception cref="Exceptions.EidCommunicationException">The operation failed.</exception>
    byte[] Sign(EidPrivateKeyKind key, byte[] data, EidSignatureMechanism mechanism);
}

/// <summary>
/// Monitors reader and card state changes in the background (hot-plug).
/// Raises <see cref="ReaderChanged"/> each time a reader is connected/disconnected
/// or a card is inserted/removed.
/// </summary>
public interface IEidReaderMonitor : IDisposable
{
    /// <summary>
    /// Raised when the state of a reader or card changes.
    /// The event may be triggered from a background thread.
    /// </summary>
    event EventHandler<Models.EidReaderChangedEventArgs> ReaderChanged;

    /// <summary>Starts background monitoring.</summary>
    void Start(CancellationToken cancellationToken = default);

    /// <summary>Stops monitoring.</summary>
    void Stop();
}

/// <summary>
/// Service for detecting readers and opening sessions to the eID card.
/// </summary>
public interface IEidReaderService
{
    /// <summary>
    /// Enumerates the smart card readers available on the machine.
    /// </summary>
    IReadOnlyList<EidReader> GetAvailableReaders();

    /// <summary>
    /// Opens a session with the card in the specified reader.
    /// If <paramref name="reader"/> is <c>null</c>, the first reader containing a card is used.
    /// </summary>
    /// <exception cref="Exceptions.EidReaderNotFoundException">No reader available.</exception>
    /// <exception cref="Exceptions.EidCardNotPresentException">No card present.</exception>
    IEidSession OpenSession(EidReader? reader = null);
}
