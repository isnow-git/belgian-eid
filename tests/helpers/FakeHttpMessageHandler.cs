namespace BelgianEid.Tests.Helpers;

/// <summary>
/// Stub HTTP handler: intercepts all <see cref="HttpClient.SendAsync"/> calls
/// and returns the response produced by the delegate provided at construction.
/// No real network call is made.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Intentionally synchronous call: the delegate is always trivial in tests.
        return Task.FromResult(_respond(request));
    }
}
