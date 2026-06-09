using System.Text.Json;
using BelgianEid.Bridge.Common;
using BelgianEid.Bridge.Services;

namespace BelgianEid.Bridge.Handlers;

/// <summary>
/// Handles <c>{ "type": "read_card" }</c> messages.
/// Reads identity, address and optionally the photo in a single operation.
/// Returns a structured response with nested <c>identity</c> and <c>address</c> objects
/// that mirror the BelgianEid library model.
/// </summary>
/// <remarks>
/// Optional request field:
/// <list type="bullet">
///   <item><c>includePhoto</c> (bool, default <c>false</c>) — also reads the JPEG photo.</item>
/// </list>
/// For certificate data use <c>read_certificates</c>.
/// For identity or address only use <c>read_identity</c> / <c>read_address</c>.
/// </remarks>
public sealed class ReadCardHandler : IMessageHandler
{
    private readonly IEidService _eidService;

    public ReadCardHandler(IEidService eidService)
    {
        _eidService = eidService ?? throw new ArgumentNullException(nameof(eidService));
    }

    public string MessageType => "read_card";

    public ValueTask<object> HandleAsync(JsonElement message, string? requestId)
    {
        bool includePhoto = message.GetBooleanOrDefault("includePhoto");

        var card = _eidService.ReadCard(includePhoto);
        var id = card.Identity;
        var addr = card.Address;

        return ValueTask.FromResult<object>(new
        {
            id = requestId,
            identity = new
            {
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
            },
            address = new
            {
                street = addr.Street,
                zipCode = addr.ZipCode,
                municipality = addr.Municipality,
            },
            photoBase64 = card.Photo is not null
                              ? Convert.ToBase64String(card.Photo.Data)
                              : null,
        });
    }
}
