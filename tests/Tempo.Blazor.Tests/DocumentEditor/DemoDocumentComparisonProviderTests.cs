using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Tempo.Blazor.Demo.Services;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DemoDocumentComparisonProviderTests
{
    [Fact]
    public async Task CompareAsync_PostsCompareRequestToDemoApi()
    {
        var compared = new DocumentCompareResult
        {
            Summary = new DocumentCompareSummary { ChangedBlocks = 1 }
        };
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(compared)));
        var provider = new DemoDocumentComparisonProvider(new TestHttpClientFactory(handler));

        var result = await provider.CompareAsync(new DocumentCompareRequest
        {
            DocumentId = "doc-1",
            BaseSource = new DocumentCompareSource { Kind = DocumentCompareSourceKind.Current, Document = DocumentEditorDocument.Empty("doc-1") },
            CompareSource = new DocumentCompareSource { Kind = DocumentCompareSourceKind.DocumentId, DocumentId = "doc-2" }
        });

        result.Summary.ChangedBlocks.Should().Be(1);
        handler.Requests.Should().ContainSingle(request =>
            request.Method == HttpMethod.Post
            && request.RequestUri!.ToString().EndsWith("/api/document-editor/compare", StringComparison.Ordinal)
            && request.Content!.Headers.ContentType!.MediaType == "application/json");
        handler.Bodies.Should().ContainSingle(body => body.Contains("\"compareSource\"", StringComparison.Ordinal));
    }

    private static HttpResponseMessage JsonResponse<T>(T value)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value)
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

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Content is not null)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return await _responder(request);
        }
    }
}
