using System.Net;
using System.Text;

namespace DotNextAI.UnitTests.TestSupport;

/// <summary>Answers every request with a canned response (or exception) and records what was sent.</summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    public static StubHttpHandler Json(HttpStatusCode status, string body) =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

    public static StubHttpHandler Throws(Exception exception) => new(_ => throw exception);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.Authorization?.ToString(),
            body));
        return respond(request);
    }
}

internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, string? Body);

/// <summary>Hands out clients over one handler, as <c>IHttpClientFactory</c> does in production.</summary>
internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
