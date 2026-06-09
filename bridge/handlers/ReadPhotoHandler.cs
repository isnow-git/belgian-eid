using System.Text.Json;
using BelgianEid.Bridge.Services;

namespace BelgianEid.Bridge.Handlers;

/// <summary>
/// Handles <c>{ "type": "read_photo" }</c> messages.
/// Reads the cardholder's photo from the eID chip and returns it as a Base64-encoded JPEG.
/// Returns <c>null</c> for <c>photoBase64</c> if the card carries no photo.
/// </summary>
public sealed class ReadPhotoHandler : IMessageHandler
{
    private readonly IEidService _eidService;

    public ReadPhotoHandler(IEidService eidService)
    {
        _eidService = eidService ?? throw new ArgumentNullException(nameof(eidService));
    }

    public string MessageType => "read_photo";

    public ValueTask<object> HandleAsync(JsonElement message, string? requestId)
    {
        var photo = _eidService.ReadPhoto();

        return ValueTask.FromResult<object>(new
        {
            id = requestId,
            photoBase64 = photo is not null ? Convert.ToBase64String(photo) : null,
        });
    }
}
