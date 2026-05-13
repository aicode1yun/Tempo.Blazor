using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Tempo.Blazor.Demo.Services;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor;

public class DemoDocumentPdfExportProviderTests
{
    [Fact]
    public async Task ExportPdfAsync_PostsDocumentSnapshotToDemoApi()
    {
        var exported = new DocumentPdfExportResult
        {
            Content = [0x25, 0x50, 0x44, 0x46],
            ContentType = "application/pdf",
            FileName = "provider.pdf"
        };
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(exported)));
        var provider = new DemoDocumentPdfExportProvider(new TestHttpClientFactory(handler));

        var result = await provider.ExportPdfAsync(new DocumentPdfExportRequest
        {
            DocumentId = "doc-1",
            Document = DocumentEditorDocument.Empty("doc-1"),
            FileName = "provider",
            Options = new DocumentPdfExportOptions
            {
                IncludeComments = false,
                IncludeSuggestions = false
            }
        });

        result.FileName.Should().Be("provider.pdf");
        handler.Requests.Should().ContainSingle(request =>
            request.Method == HttpMethod.Post
            && request.RequestUri!.ToString().EndsWith("/api/document-editor/doc-1/export/pdf", StringComparison.Ordinal)
            && request.Content!.Headers.ContentType!.MediaType == "application/json");
        handler.Bodies.Should().ContainSingle(body => body.Contains("\"includeComments\":false", StringComparison.Ordinal));
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
