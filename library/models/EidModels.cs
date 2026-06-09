namespace BelgianEid.Models;

/// <summary>
/// Arguments for the event raised by <see cref="BelgianEid.Abstractions.IEidReaderMonitor"/>
/// when the state of a reader or card changes.
/// </summary>
public sealed class EidReaderChangedEventArgs : EventArgs
{
    /// <summary>Type of event (reader connected/disconnected, card inserted/removed).</summary>
    public EidReaderEventKind Kind { get; init; }

    /// <summary>The reader affected by the event.</summary>
    public EidReader Reader { get; init; } = null!;
}

/// <summary>
/// Represents a smart card reader detected on the machine.
/// </summary>
public sealed class EidReader
{
    /// <summary>Internal identifier of the PKCS#11 slot.</summary>
    public ulong SlotId { get; init; }

    /// <summary>Human-readable name of the reader (slot description).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Indicates whether a card is inserted in this reader.</summary>
    public bool HasCardInserted { get; init; }

    /// <inheritdoc/>
    public override string ToString()
        => $"{Name} (slot {SlotId}, card {(HasCardInserted ? "present" : "absent")})";
}

/// <summary>
/// Raw data object read from the card (identity file, address, photo...).
/// </summary>
public sealed class EidDataObject
{
    /// <summary>PKCS#11 label (CKA_LABEL) of the object.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Raw binary content of the object.</summary>
    public byte[] Value { get; init; } = [];
}

/// <summary>
/// PIN code status as reported by the card (attempt counter).
/// </summary>
public sealed class EidPinStatus
{
    /// <summary>Estimated number of remaining attempts before blocking (0 to 3).</summary>
    public int RemainingAttempts { get; init; }

    /// <summary>Indicates whether the PIN is currently blocked.</summary>
    public bool IsBlocked => RemainingAttempts <= 0;
}

/// <summary>
/// Personal identity data extracted from the eID card.
/// </summary>
public sealed class EidIdentity
{
    /// <summary>Card number.</summary>
    public string CardNumber { get; init; } = string.Empty;

    /// <summary>Chip number.</summary>
    public string ChipNumber { get; init; } = string.Empty;

    /// <summary>Last name.</summary>
    public string LastName { get; init; } = string.Empty;

    /// <summary>First names.</summary>
    public string FirstNames { get; init; } = string.Empty;

    /// <summary>National register number (11 digits).</summary>
    public string NationalNumber { get; init; } = string.Empty;

    /// <summary>Nationality.</summary>
    public string Nationality { get; init; } = string.Empty;

    /// <summary>Place of birth.</summary>
    public string BirthLocation { get; init; } = string.Empty;

    /// <summary>Date of birth as printed on the card (raw text).</summary>
    public string BirthDateRaw { get; init; } = string.Empty;

    /// <summary>Parsed date of birth (if the format could be recognised).</summary>
    public DateOnly? BirthDate { get; init; }

    /// <summary>Gender.</summary>
    public EidGender Gender { get; init; }

    /// <summary>Municipality that issued the card.</summary>
    public string IssuingMunicipality { get; init; } = string.Empty;

    /// <summary>Start of card validity.</summary>
    public DateOnly? ValidityBegin { get; init; }

    /// <summary>End of card validity.</summary>
    public DateOnly? ValidityEnd { get; init; }

    /// <summary>Hash of the photo, as stored in the identity file.</summary>
    public byte[] PhotoHash { get; init; } = [];

    /// <inheritdoc/>
    public override string ToString() => $"{FirstNames} {LastName} ({NationalNumber})";
}

/// <summary>
/// Residence address extracted from the eID card.
/// </summary>
public sealed class EidAddress
{
    /// <summary>Street and number.</summary>
    public string Street { get; init; } = string.Empty;

    /// <summary>Zip code.</summary>
    public string ZipCode { get; init; } = string.Empty;

    /// <summary>Municipality.</summary>
    public string Municipality { get; init; } = string.Empty;

    /// <inheritdoc/>
    public override string ToString() => $"{Street}, {ZipCode} {Municipality}";
}

/// <summary>
/// Identity photo extracted from the eID card.
/// </summary>
public sealed class EidPhoto
{
    /// <summary>Binary image data (JPEG format).</summary>
    public byte[] Data { get; init; } = [];

    /// <summary>MIME type of the image.</summary>
    public string MimeType => "image/jpeg";

    /// <summary>
    /// Saves the photo to disk in JPEG format.
    /// </summary>
    /// <param name="filePath">Full file path (e.g. C:\Users\...\photo.jpg)</param>
    /// <param name="overwrite">True to overwrite if the file already exists</param>
    /// <returns>True if the save succeeded</returns>
    public bool SaveToFile(string filePath, bool overwrite = true)
    {
        if (Data.Length == 0)
            return false;

        if (!overwrite && File.Exists(filePath))
            return false;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllBytes(filePath, Data);
        return true;
    }

    /// <summary>
    /// Saves the photo to disk asynchronously.
    /// </summary>
    public async Task<bool> SaveToFileAsync(string filePath, bool overwrite = true, CancellationToken ct = default)
    {
        if (Data.Length == 0)
            return false;

        if (!overwrite && File.Exists(filePath))
            return false;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllBytesAsync(filePath, Data, ct).ConfigureAwait(false);
        return true;
    }
}

/// <summary>
/// Aggregate of all data readable from an eID card.
/// </summary>
public sealed class EidCardData
{
    /// <summary>Identity data.</summary>
    public required EidIdentity Identity { get; init; }

    /// <summary>Residence address.</summary>
    public required EidAddress Address { get; init; }

    /// <summary>Identity photo (may be <c>null</c> if not read).</summary>
    public EidPhoto? Photo { get; init; }

    /// <summary>Set of X.509 certificates from the card.</summary>
    public required EidCertificateSet Certificates { get; init; }
}
