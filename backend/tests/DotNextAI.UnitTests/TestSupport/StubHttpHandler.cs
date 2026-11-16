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

    /// <summary>A streamed (Server-Sent Events) answer, as the OpenAI API sends with "stream": true.</summary>
    public static StubHttpHandler Sse(string body) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") });

    /// <summary>A stream that sends <paramref name="firstChunk"/> and then breaks, like a dropped connection.</summary>
    public static StubHttpHandler SseThatBreaks(string firstChunk) =>
        new(_ =>
        {
            var content = new StreamContent(new BrokenStream(Encoding.UTF8.GetBytes(firstChunk)));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

    private sealed class BrokenStream(byte[] first) : Stream
    {
        private bool _sent;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_sent)
            {
                throw new IOException("The connection was reset.");
            }

            _sent = true;
            Array.Copy(first, 0, buffer, offset, Math.Min(count, first.Length));
            return Math.Min(count, first.Length);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_sent)
            {
                throw new IOException("The connection was reset.");
            }

            _sent = true;
            first.AsMemory(0, Math.Min(buffer.Length, first.Length)).CopyTo(buffer);
            return ValueTask.FromResult(Math.Min(buffer.Length, first.Length));
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

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
