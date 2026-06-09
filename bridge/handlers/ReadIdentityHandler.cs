using System.Text.Json;
using BelgianEid.Bridge.Services;

namespace BelgianEid.Bridge.Handlers;

/// <summary>
/// Handles <c>{ "type": "read_identity" }</c> messages.
/// Reads only the identity fields from the eID chip (name, NRN, birth date, gender, validity, …).
/// Use <c>read_card</c> to retrieve identity + address + photo in a single round-trip.
/// </summary>
public sealed class ReadIdentityHandler : IMessageHandler
{
    private readonly IEidService _eidService;

    public ReadIdentityHandler(IEidService eidService)
    {
        _eidService = eidService ?? throw new ArgumentNullException(nameof(eidService));
    }

    public string MessageType => "read_identity";

    public ValueTask<object> HandleAsync(JsonElement message, string? requestId)
    {
        var id = _eidService.ReadIdentity();

        return ValueTask.FromResult<object>(new
        {
            id = requestId,
            cardNumber = id.CardNumber,
            chipNumber = id.ChipNumber,
            lastName = id.LastName,
            firstNames = id.FirstNames,
            nationalNumber = id.NationalNumber,
            nationality = id.Nationality,
            birthLocation = id.BirthLocation,
            birthDateRaw = id.BirthDateRaw,
            birthDate = id.BirthDate?.ToString("yyyy-MM-dd"),
            gender = id.Gender.ToString(),
            issuingMunicipality = id.IssuingMunicipality,
            validityBegin = id.ValidityBegin?.ToString("yyyy-MM-dd"),
            validityEnd = id.ValidityEnd?.ToString("yyyy-MM-dd"),
        });
    }
}
