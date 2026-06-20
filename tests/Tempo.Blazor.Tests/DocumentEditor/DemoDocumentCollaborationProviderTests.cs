using System.Net;
using System.Text.Json;
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

    [Fact]
    public async Task BroadcastOperationBatchAsync_PostsOperationIdentityAndProtocolVersion()
    {
        string? requestJson = null;
        var response = new DocumentCollaborationOperationBatch
        {
            Sequence = 7,
            SessionId = "session-1",
            Batch = new DocumentOperationBatch
            {
                DocumentId = "doc-1",
                ProtocolVersion = DocumentOperationBatch.CurrentProtocolVersion,
                Operations =
                [
                    new DocumentOperation
                    {
                        OperationId = "operation-1",
                        Type = DocumentOperationType.InsertText,
                        Target = new DocumentOperationTarget { BlockId = "block-1", Offset = 0, Length = 3 },
                        Text = "Hey"
                    }
                ]
            }
        };
        var handler = new RecordingHandler(async request =>
        {
            requestJson = await request.Content!.ReadAsStringAsync();
            return JsonResponse(response);
        });
        var provider = new DemoDocumentCollaborationProvider(new TestHttpClientFactory(handler));

        var result = await provider.BroadcastOperationBatchAsync("session-1", response.Batch);

        result.Sequence.Should().Be(7);
        requestJson.Should().Contain("\"protocolVersion\"");
        requestJson.Should().Contain("\"operationId\":\"operation-1\"");
        handler.Requests.Should().ContainSingle(request =>
            request.Method == HttpMethod.Post
            && request.RequestUri!.ToString().EndsWith("/api/document-editor/collaboration/session-1/batches", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetOperationBatchesAsync_ReturnsEchoWithClearSessionMetadata()
    {
        var batches = new[]
        {
            new DocumentCollaborationOperationBatch
            {
                Sequence = 3,
                SessionId = "session-1",
                Batch = new DocumentOperationBatch
                {
                    DocumentId = "doc-1",
                    ProtocolVersion = DocumentOperationBatch.CurrentProtocolVersion,
                    Operations = [new DocumentOperation { OperationId = "operation-1", Text = "Echo" }]
                }
            }
        };
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(batches)));
        var provider = new DemoDocumentCollaborationProvider(new TestHttpClientFactory(handler));

        var result = await provider.GetOperationBatchesAsync("doc-1", 0);

        result.Should().ContainSingle(batch =>
            batch.SessionId == "session-1"
            && batch.Batch.ProtocolVersion == DocumentOperationBatch.CurrentProtocolVersion
            && batch.Batch.Operations.Single().OperationId == "operation-1");
    }

    private static HttpResponseMessage JsonResponse<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
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
