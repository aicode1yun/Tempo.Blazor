using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Tempo.Blazor.Demo.Api.Data;
using Tempo.Blazor.DocumentFormats;
using Tempo.Blazor.DocumentFormats.Docx;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Dm = Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Demo.Api.Tests;

public class NotionImportExportEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public NotionImportExportEndpointTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Theory]
    [InlineData(NotionExportFormat.Markdown, "text/markdown", ".md", "# CF25")]
    [InlineData(NotionExportFormat.Html, "text/html", ".html", "CF25")]
    [InlineData(NotionExportFormat.Pdf, "application/pdf", ".pdf", "%PDF")]
    [InlineData(NotionExportFormat.Docx, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx", "PK")]
    [InlineData(NotionExportFormat.Odt, "application/vnd.oasis.opendocument.text", ".odt", "PK")]
    public async Task ExportPage_ReturnsDocumentArtifactForEverySupportedFormat(
        NotionExportFormat format,
        string mediaType,
        string extension,
        string signature)
    {
        await SeedExportPageAsync();

        using var response = await _client.GetAsync($"/api/notion/pages/{MockNotionDataStore.Page1Id:D}/export/{format}?includeSubpages=false");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.EndsWith(extension, response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'), StringComparison.OrdinalIgnoreCase);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        if (format is NotionExportFormat.Markdown or NotionExportFormat.Html)
        {
            Assert.Contains(signature, Encoding.UTF8.GetString(bytes));
        }
        else
        {
            Assert.StartsWith(signature, Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, signature.Length)));
        }
    }

    [Fact]
    public async Task ExportPage_WithSubpages_IncludesDescendantPages()
    {
        await SeedExportPageAsync();

        var markdown = await _client.GetStringAsync($"/api/notion/pages/{MockNotionDataStore.Page1Id:D}/export/{NotionExportFormat.Markdown}?includeSubpages=true");

        Assert.Contains("CF25 Export Bridge", markdown);
        Assert.Contains("CF25 Export Child", markdown);
        Assert.Contains("CF25 Export Grandchild", markdown);
        Assert.Contains("Grandchild page content proves recursive subtree export.", markdown);
    }

    [Fact]
    public async Task ExportPage_UnsupportedFormat_ReturnsBadRequest()
    {
        await SeedExportPageAsync();

        using var response = await _client.GetAsync($"/api/notion/pages/{MockNotionDataStore.Page1Id:D}/export/xlsx?includeSubpages=false");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImportPage_WordDocument_CreatesPageAndConvertedBlocks()
    {
        var exported = await new DocumentDocxExporter().ExportAsync(CreateImportDocument(), new DocumentFormatExportOptions
        {
            FileName = "cf26-word-import",
            AllowImagePlaceholders = true
        });

        var page = await ImportAsync(exported.Content, "cf26-word-import.docx", NotionImportFormat.Word);

        Assert.Equal("CF26 Word Import", page.Title);
        var aggregate = await GetAggregateJsonAsync(page.Id);
        Assert.Contains("Imported heading", aggregate);
        Assert.Contains("Converted from DOCX bridge", aggregate);
        Assert.Contains("Ready", aggregate);
    }

    [Theory]
    [InlineData(NotionImportFormat.Markdown, "cf26-markdown.md", "text/markdown", "# CF26 Markdown Import\n\nImported from markdown.\n\n| Name | Status |\n| --- | --- |\n| CF26 | Ready |")]
    [InlineData(NotionImportFormat.Html, "cf26-html.html", "text/html", "<h1>CF26 HTML Import</h1><p>Imported from HTML.</p><table><tr><th>Name</th><th>Status</th></tr><tr><td>CF26</td><td>Ready</td></tr></table>")]
    public async Task ImportPage_TextFormats_CreateConvertedPage(
        NotionImportFormat format,
        string fileName,
        string mediaType,
        string body)
    {
        var page = await ImportAsync(Encoding.UTF8.GetBytes(body), fileName, format, mediaType);

        Assert.Contains("CF26", page.Title);
        Assert.Contains("Ready", await GetAggregateJsonAsync(page.Id));
    }

    [Fact]
    public async Task ImportPage_InvalidWordFile_ReturnsBadRequestWithoutCreatingPage()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(NotionImportFormat.Word.ToString()), "format");
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("not a docx package"));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        form.Add(file, "file", "invalid.docx");

        using var response = await _client.PostAsync("/api/notion/pages/import", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task SeedExportPageAsync()
    {
        using var response = await _client.PostAsync("/api/notion/e2e/seed/export", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task<NotionPage> ImportAsync(
        byte[] bytes,
        string fileName,
        NotionImportFormat format,
        string? mediaType = null)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(format.ToString()), "format");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType ?? ContentTypeFor(format));
        form.Add(file, "file", fileName);

        using var response = await _client.PostAsync("/api/notion/pages/import", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<NotionPage>()
            ?? throw new InvalidOperationException("Import response did not contain a page.");
    }

    private static Dm.DocumentEditorDocument CreateImportDocument()
    {
        var document = Dm.DocumentEditorDocument.Empty();
        document.Metadata.Title = "CF26 Word Import";
        document.Blocks =
        [
            new Dm.DocumentBlock
            {
                Type = Dm.DocumentBlockType.Heading,
                Order = 0,
                Content = new Dm.HeadingBlockContent
                {
                    Level = 1,
                    Inlines = [new Dm.TextRun { Text = "Imported heading" }]
                }
            },
            new Dm.DocumentBlock
            {
                Type = Dm.DocumentBlockType.Paragraph,
                Order = 1,
                Content = new Dm.ParagraphBlockContent
                {
                    Inlines = [new Dm.TextRun { Text = "Converted from DOCX bridge" }]
                }
            },
            new Dm.DocumentBlock
            {
                Type = Dm.DocumentBlockType.Table,
                Order = 2,
                Content = new Dm.TableBlockContent
                {
                    Rows =
                    [
                        new Dm.TableRowContent
                        {
                            Cells =
                            [
                                Cell("Name", true),
                                Cell("Status", true)
                            ]
                        },
                        new Dm.TableRowContent
                        {
                            Cells =
                            [
                                Cell("CF26", false),
                                Cell("Ready", false)
                            ]
                        }
                    ]
                }
            }
        ];
        return document;
    }

    private static Dm.TableCellContent Cell(string value, bool isHeader) => new()
    {
        IsHeader = isHeader,
        Blocks =
        [
            new Dm.DocumentBlock
            {
                Type = Dm.DocumentBlockType.Paragraph,
                Content = new Dm.ParagraphBlockContent
                {
                    Inlines = [new Dm.TextRun { Text = value }]
                }
            }
        ]
    };

    private static string ContentTypeFor(NotionImportFormat format) => format switch
    {
        NotionImportFormat.Word => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        NotionImportFormat.Html => "text/html",
        NotionImportFormat.Markdown => "text/markdown",
        _ => "application/octet-stream"
    };

    private Task<string> GetAggregateJsonAsync(Guid pageId)
        => _client.GetStringAsync($"/api/notion/aggregate/pages/{pageId:D}");
}
