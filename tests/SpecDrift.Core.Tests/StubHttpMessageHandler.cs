namespace SpecDrift.Core.Tests;

/// <summary>Intercepts every request instead of sending it anywhere — the mechanism
/// behind "no test makes a network call" for <c>AnthropicLlmClient</c>'s own tests.</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string?, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return _responder(request, LastRequestBody);
    }
}
