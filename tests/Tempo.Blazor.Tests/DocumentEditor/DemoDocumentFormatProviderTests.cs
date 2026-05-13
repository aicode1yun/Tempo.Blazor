using System.Net;
using System.Net.Http.Json;
using Tempo.Blazor.Demo.Services;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor;

public class DemoDocumentFormatProviderTests
{
    [Fact]
    public async Task ImportAsync_PostsMultipartBytesToDemoApi()
    {
        var imported = new DocumentFormatImportProviderResult
        {
            Document = DocumentEditorDocument.Empty()
        };
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(imported)));
        var provider = new DemoDocumentFormatProvider(new TestHttpClientFactory(handler));

        var result = await provider.ImportAsync(new DocumentFormatImportProviderRequest
        {
            DocumentId = "doc-1",
            Format = DocumentFormatProviderKind.Docx,
            FileName = "provider.docx",
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            Content = [1, 2, 3]
        });

        result.Success.Should().BeTrue();
        handler.Requests.Should().ContainSingle(request =>
            request.Method == HttpMethod.Post
            && request.RequestUri!.ToString().EndsWith("/api/document-editor/formats/import?format=Docx", StringComparison.Ordinal)
            && request.Content!.Headers.ContentType!.MediaType == "multipart/form-data");
        handler.Bodies.Should().ContainSingle(body => body.Contains("provider.docx", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExportAsync_PostsDocumentSnapshotToDemoApi()
    {
        var exported = new DocumentFormatExportProviderResult
        {
            Content = [1, 2, 3],
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileName = "provider.docx"
        };
        var handler = new RecordingHandler(_ => Task.FromResult(JsonResponse(exported)));
        var provider = new DemoDocumentFormatProvider(new TestHttpClientFactory(handler));

        var result = await provider.ExportAsync(new DocumentFormatExportProviderRequest
        {
            DocumentId = "doc-1",
            Format = DocumentFormatProviderKind.Docx,
            Document = DocumentEditorDocument.Empty(),
            FileName = "provider"
        });

        result.FileName.Should().Be("provider.docx");
        handler.Requests.Should().ContainSingle(request =>
            request.Method == HttpMethod.Post
            && request.RequestUri!.ToString().EndsWith("/api/document-editor/formats/export", StringComparison.Ordinal)
            && request.Content!.Headers.ContentType!.MediaType == "application/json");
        handler.Bodies.Should().ContainSingle(body => body.Contains("\"documentId\"", StringComparison.Ordinal));
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
