using System.Text.Json;
using BelgianEid.Bridge.Services;

namespace BelgianEid.Bridge.Handlers;

/// <summary>
/// Handles <c>{ "type": "read_address" }</c> messages.
/// Reads only the address file from the eID chip (street, zip code, municipality).
/// </summary>
public sealed class ReadAddressHandler : IMessageHandler
{
    private readonly IEidService _eidService;

    public ReadAddressHandler(IEidService eidService)
    {
        _eidService = eidService ?? throw new ArgumentNullException(nameof(eidService));
    }

    public string MessageType => "read_address";

    public ValueTask<object> HandleAsync(JsonElement message, string? requestId)
    {
        var addr = _eidService.ReadAddress();

        return ValueTask.FromResult<object>(new
        {
            id = requestId,
            street = addr.Street,
            zipCode = addr.ZipCode,
            municipality = addr.Municipality,
        });
    }
}
