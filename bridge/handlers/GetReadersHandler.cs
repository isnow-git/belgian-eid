using System.Text.Json;
using BelgianEid.Bridge.Services;

namespace BelgianEid.Bridge.Handlers;

/// <summary>
/// Handles <c>{ "type": "get_readers" }</c> messages.
/// Returns the list of all PC/SC card readers detected on the machine,
/// with their name, slot ID and card-insertion state.
/// </summary>
public sealed class GetReadersHandler : IMessageHandler
{
    private readonly IEidService _eidService;

    public GetReadersHandler(IEidService eidService)
    {
        _eidService = eidService ?? throw new ArgumentNullException(nameof(eidService));
    }

    public string MessageType => "get_readers";

    public ValueTask<object> HandleAsync(JsonElement message, string? requestId)
    {
        var readers = _eidService.GetReaders()
            .Select(r => new
            {
                name = r.Name,
                slotId = r.SlotId,
                hasCardInserted = r.HasCardInserted,
            })
            .ToArray();

        return ValueTask.FromResult<object>(new { id = requestId, readers });
    }
}
