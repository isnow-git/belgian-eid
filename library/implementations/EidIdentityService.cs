using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using BelgianEid.Abstractions;
using BelgianEid.Configuration;
using BelgianEid.Exceptions;
using BelgianEid.Models;
using BelgianEid.Utilities;
using Microsoft.Extensions.Options;

namespace BelgianEid.Implementations;

/// <summary>
/// Reads and decodes personal data (identity, address, photo) from the eID card.
/// </summary>
public sealed class EidIdentityService : IEidIdentityService
{
    private readonly BelgianEidOptions _options;

    public EidIdentityService(IOptions<BelgianEidOptions> options) => _options = options.Value;

    /// <inheritdoc/>
    public EidIdentity ReadIdentity(IEidSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        byte[] raw = ResolveDataObject(
            session, _options.DataObjectLabels.Identity,
            value => LooksLikeIdentity(value),
            "identity file");

        IReadOnlyDictionary<int, byte[]> tlv = TlvParser.Parse(raw);

        return new EidIdentity
        {
            CardNumber = Text(tlv, IdentityTags.CardNumber),
            ChipNumber = Text(tlv, IdentityTags.ChipNumber),
            LastName = Text(tlv, IdentityTags.LastName),
            FirstNames = Text(tlv, IdentityTags.FirstNames),
            NationalNumber = Text(tlv, IdentityTags.NationalNumber),
            Nationality = Text(tlv, IdentityTags.Nationality),
            BirthLocation = Text(tlv, IdentityTags.BirthLocation),
            BirthDateRaw = Text(tlv, IdentityTags.BirthDate),
            BirthDate = ParseBirthDate(Text(tlv, IdentityTags.BirthDate)),
            Gender = ParseGender(Text(tlv, IdentityTags.Gender)),
            IssuingMunicipality = Text(tlv, IdentityTags.IssuingMunicipality),
            ValidityBegin = ParseCardDate(Text(tlv, IdentityTags.ValidityBegin)),
            ValidityEnd = ParseCardDate(Text(tlv, IdentityTags.ValidityEnd)),
            PhotoHash = tlv.TryGetValue(IdentityTags.PhotoHash, out var h) ? h : [],
        };
    }

    /// <inheritdoc/>
    public EidAddress ReadAddress(IEidSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        byte[] raw = ResolveDataObject(
            session, _options.DataObjectLabels.Address,
            value => LooksLikeAddress(value),
            "address file");

        IReadOnlyDictionary<int, byte[]> tlv = TlvParser.Parse(raw);

        return new EidAddress
        {
            Street = Text(tlv, AddressTags.Street),
            ZipCode = Text(tlv, AddressTags.ZipCode),
            Municipality = Text(tlv, AddressTags.Municipality),
        };
    }

    /// <inheritdoc/>
    public EidPhoto? ReadPhoto(IEidSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        EidDataObject? photo = session.GetDataObjects()
            .FirstOrDefault(o =>
                o.Label.Contains(_options.DataObjectLabels.Photo, StringComparison.OrdinalIgnoreCase)
                || IsJpeg(o.Value));

        return photo is null || photo.Value.Length == 0
            ? null
            : new EidPhoto { Data = photo.Value };
    }

    /// <summary>
    /// Locates a data object: first by configured label, then by heuristic content detection.
    /// </summary>
    private static byte[] ResolveDataObject(
        IEidSession session, string configuredLabel, Func<byte[], bool> contentMatches, string what)
    {
        IReadOnlyList<EidDataObject> objects = session.GetDataObjects();

        EidDataObject? byLabel = objects.FirstOrDefault(o =>
            o.Label.Contains(configuredLabel, StringComparison.OrdinalIgnoreCase));
        if (byLabel is { Value.Length: > 0 })
            return byLabel.Value;

        EidDataObject? byContent = objects.FirstOrDefault(o => contentMatches(o.Value));
        if (byContent is not null)
            return byContent.Value;

        throw new EidDataNotFoundException(
            $"The {what} was not found among the card's data objects.");
    }

    // --- Content classification heuristics ---

    private static bool IsJpeg(byte[] value)
        => value.Length >= 3 && value[0] == 0xFF && value[1] == 0xD8 && value[2] == 0xFF;

    private static bool LooksLikeIdentity(byte[] value)
    {
        if (IsJpeg(value))
            return false;
        var tlv = TlvParser.Parse(value);
        // The identity file contains both the national register number AND the last name.
        return tlv.ContainsKey(IdentityTags.NationalNumber) && tlv.ContainsKey(IdentityTags.LastName);
    }

    private static bool LooksLikeAddress(byte[] value)
    {
        if (IsJpeg(value))
            return false;
        var tlv = TlvParser.Parse(value);
        // The address file contains only tags 1 to 3.
        return tlv.Count is > 0 and <= 4
               && tlv.Keys.All(k => k <= AddressTags.Municipality)
               && tlv.ContainsKey(AddressTags.ZipCode);
    }

    // --- Field decoding ---

    private static string Text(IReadOnlyDictionary<int, byte[]> tlv, int tag)
        => tlv.TryGetValue(tag, out var bytes) ? Encoding.UTF8.GetString(bytes).Trim() : string.Empty;

    private static EidGender ParseGender(string value) => value.ToUpperInvariant() switch
    {
        "M" => EidGender.Male,
        "F" or "V" => EidGender.Female,
        _ => EidGender.Unknown,
    };

    private static DateOnly? ParseCardDate(string value)
        => DateOnly.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date) ? date : null;

    /// <summary>
    /// Attempts to parse the date of birth, whose format depends on the card's language
    /// (e.g. "07 JAN 1985", "07 JANV 1985", "1985-01-07").
    /// </summary>
    private static DateOnly? ParseBirthDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
            return iso;

        string[] parts = value.Split([' ', '.', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3
            && int.TryParse(parts[0], out int day)
            && int.TryParse(parts[2], out int year))
        {
            string key = parts[1].ToUpperInvariant();
            int month = MonthNames
                .Where(kv => key.StartsWith(kv.Key, StringComparison.Ordinal))
                .Select(kv => kv.Value)
                .DefaultIfEmpty(0)
                .First();

            if (month is >= 1 and <= 12 && day is >= 1 and <= 31)
            {
                try { return new DateOnly(year, month, day); }
                catch (ArgumentOutOfRangeException) { return null; }
            }
        }

        return null;
    }

    /// <summary>Month abbreviations (FR / NL / EN / DE) recognised on the eID card.</summary>
    private static readonly IReadOnlyDictionary<string, int> MonthNames = new Dictionary<string, int>
    {
        ["JAN"] = 1,
        ["FEV"] = 2, ["FEB"] = 2,
        ["MAR"] = 3, ["MAA"] = 3, ["MRT"] = 3,
        ["AVR"] = 4, ["APR"] = 4,
        ["MAI"] = 5, ["MEI"] = 5, ["MAY"] = 5,
        ["JUN"] = 6, ["JUIN"] = 6,
        ["JUL"] = 7, ["JUIL"] = 7,
        ["AOU"] = 8, ["AUG"] = 8,
        ["SEP"] = 9,
        ["OCT"] = 10, ["OKT"] = 10,
        ["NOV"] = 11,
        ["DEC"] = 12, ["DEZ"] = 12,
    };
}
