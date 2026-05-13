using System.Net;
using System.Net.Http.Json;
using Tempo.Blazor.Demo.Services;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor;

public class DemoDocumentCollaborationProviderTests
{
    [Fact]
    public async Task JoinAsync_PostsToDemoApi()
    {
        var session = new DocumentCollaborationSession
        {
            Id = "session-1",
            DocumentId = "doc-1",
            ClientId = "client-1",
            Author = new DocumentEditorAuthor { Id = "user-1", DisplayName = "User One" }
        };
        var handler = new RecordingHandler(request =>
            JsonContent.Create(session).ReadAsStringAsync().ContinueWith(message =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(message.Result, System.Text.Encoding.UTF8, "application/json")
                }));
        var provider = new DemoDocumentCollaborationProvider(new TestHttpClientFactory(handler));

        var result = await provider.JoinAsync(new DocumentCollaborationJoinRequest
        {
            DocumentId = "doc-1",
            ClientId = "client-1",
            Author = session.Author
        });

        result.Id.Should().Be("session-1");
        handler.Requests.Should().ContainSingle(request =>
            request.Method == HttpMethod.Post
            && request.RequestUri!.ToString().EndsWith("/api/document-editor/collaboration/join", StringComparison.Ordinal));
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public TestHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
            => new(_handler, disposeHandler: false) { BaseAddress = new Uri("https://localhost:5100/") };
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

        public RecordingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return _responder(request);
        }
    }
}
