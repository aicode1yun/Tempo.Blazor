using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class TmDocumentEditingTests : LocalizationTestBase
{
    [Fact]
    public void Surface_ClickingBlockSetsActiveSelection()
    {
        var document = CreateDocument(Paragraph("Alpha"));
        var selection = new DocumentEditorSelectionState();

        var cut = RenderComponent<TmDocumentSurface>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.ReadOnly, false)
            .Add(p => p.Selection, selection));

        cut.Find(".tm-document-editable-block").Click();

        selection.ActiveBlockId.Should().Be(document.Blocks[0].Id);
        selection.FocusedInlineRange.Should().NotBeNull();
    }

    [Fact]
    public void Surface_ClickingRootClearsActiveSelection()
    {
        var document = CreateDocument(Paragraph("Alpha"));
        var selection = new DocumentEditorSelectionState { ActiveBlockId = document.Blocks[0].Id };

        var cut = RenderComponent<TmDocumentSurface>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.ReadOnly, false)
            .Add(p => p.Selection, selection));

        cut.Find(".tm-document-surface").Click();

        selection.ActiveBlockId.Should().BeNull();
    }

    [Fact]
    public void Surface_ReadOnlyBlockCannotBecomeActive()
    {
        var document = CreateDocument(Paragraph("Alpha"));
        var selection = new DocumentEditorSelectionState();

        var cut = RenderComponent<TmDocumentSurface>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.ReadOnly, true)
            .Add(p => p.Selection, selection));

        cut.Find(".tm-document-block--paragraph").Click();

        selection.ActiveBlockId.Should().BeNull();
    }

    [Fact]
    public void Paragraph_InputChangesTextAndDoesNotFireOnRerender()
    {
        var document = CreateDocument(Paragraph("Alpha"));
        var changes = 0;

        var cut = RenderComponent<TmDocumentSurface>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.ReadOnly, false)
            .Add(p => p.DocumentChanged, EventCallback.Factory.Create<DocumentEditorDocument>(this, _ => changes++)));

        cut.Find("[data-testid='document-paragraph-editor']").Input("Beta");
        cut.Render();

        GetText(document.Blocks[0]).Should().Be("Beta");
        changes.Should().Be(1);
    }

    [Fact]
    public void Paragraph_DoubleBraceOpensTokenMenuAndSelectionInsertsTokenRun()
    {
        var document = CreateDocument(Paragraph("Dear "));
        var cut = RenderComponent<TmDocumentSurface>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.ReadOnly, false)
            .Add(p => p.TokenProvider, new TestTokenProvider()));

        cut.Find("[data-testid='document-paragraph-editor']").Input("Dear {{cl");

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-token-menu']").TextContent.Should().Contain("client.name"));
        cut.Find(".tm-rte-token-item").Click();

        var inlines = ((ParagraphBlockContent)document.Blocks[0].Content).Inlines;
        var token = inlines.OfType<TokenRun>().Single();
        token.Key.Should().Be("client.name");
        token.DisplayName.Should().Be("Client name");
        token.TypeLabel.Should().Be("Text");
        cut.Find("[data-testid='document-edit-token-chip']").TextContent.Should().Contain("Client name");
    }

    [Fact]
    public void Paragraph_TokenDeleteRemovesTokenAsSingleUnit()
    {
        var document = CreateDocument(Paragraph(
            new TextRun { Text = "Dear " },
            new TokenRun { Key = "client.name", DisplayName = "Client name", TypeLabel = "Text" },
            new TextRun { Text = "." }));
        var cut = RenderComponent<TmDocumentSurface>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.ReadOnly, false)
            .Add(p => p.TokenProvider, new TestTokenProvider()));

        cut.Find("[data-testid='document-token-delete']").Click();

        var inlines = ((ParagraphBlockContent)document.Blocks[0].Content).Inlines;
        inlines.Should().NotContain(inline => inline is TokenRun);
        GetText(document.Blocks[0]).Should().Be("Dear .");
    }

    [Fact]
    public void Paragraph_EnterCreatesNewParagraphAndBackspaceMergesEmptyParagraph()
    {
        var document = CreateDocument(Paragraph("Alpha"));
        var cut = RenderComponent<TmDocumentSurface>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.ReadOnly, false));

        cut.Find("[data-testid='document-paragraph-editor']").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        document.Blocks.Should().HaveCount(2);

        cut.FindAll("[data-testid='document-paragraph-editor']")[1].KeyDown(new KeyboardEventArgs { Key = "Backspace" });
        document.Blocks.Should().HaveCount(1);
    }

    [Fact]
    public void Heading_TextLevelAndEnterAreEditable()
    {
        var document = CreateDocument(Heading(1, "Title"));
        var cut = RenderComponent<TmDocumentSurface>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.ReadOnly, false));

        cut.Find("[data-testid='document-heading-editor']").Input("Changed");
        cut.Find(".tm-document-heading-editor__level").Change("2");
        cut.Find("[data-testid='document-heading-editor']").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var heading = (HeadingBlockContent)document.Blocks[0].Content;
        GetText(document.Blocks[0]).Should().Be("Changed");
        heading.Level.Should().Be(2);
        document.Blocks.Should().HaveCount(2);
    }

    [Fact]
    public void List_EnterCreatesNextListItemAndTabChangesIndent()
    {
        var document = CreateDocument(List("Item", ordered: false));
        var cut = RenderComponent<TmDocumentSurface>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.ReadOnly, false));

        var editor = cut.Find("[data-testid='document-list-editor']");
        editor.KeyDown(new KeyboardEventArgs { Key = "Enter" });
        editor.KeyDown(new KeyboardEventArgs { Key = "Tab" });
        editor.KeyDown(new KeyboardEventArgs { Key = "Tab", ShiftKey = true });

        document.Blocks.Should().HaveCount(2);
        document.Blocks[1].Type.Should().Be(DocumentBlockType.List);
        ((ListBlockContent)document.Blocks[0].Content).IndentLevel.Should().Be(0);
    }

    [Fact]
    public void EmptyList_EnterEndsListWithParagraph()
    {
        var document = CreateDocument(List(string.Empty, ordered: true));
        var cut = RenderComponent<TmDocumentSurface>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.ReadOnly, false));

        cut.Find("[data-testid='document-list-editor']").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        document.Blocks[1].Type.Should().Be(DocumentBlockType.Paragraph);
    }

    [Fact]
    public void Table_CanEditCellsRowsColumnsMergeSplitAndNavigate()
    {
        var document = CreateDocument(Table());
        var selection = new DocumentEditorSelectionState { ActiveBlockId = document.Blocks[0].Id };
        var cut = RenderComponent<TmDocumentSurface>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.ReadOnly, false)
            .Add(p => p.Selection, selection));

        cut.Find("[data-testid='document-table-cell']").Change("Cell text");
        cut.Find("[data-testid='document-table-add-row']").Click();
        cut.Find("[data-testid='document-table-add-column']").Click();
        cut.Find("[data-testid='document-table-cell']").Click();
        cut.Find("[data-testid='document-table-merge-right']").Click();

        var table = (TableBlockContent)document.Blocks[0].Content;
        table.Rows.Should().HaveCount(3);
        table.Rows[0].Cells.Should().HaveCount(3);
        table.Rows[0].Cells[0].ColumnSpan.Should().Be(2);

        cut.FindAll("button").First(button => button.TextContent.Contains("Split cell", StringComparison.Ordinal)).Click();
        table.Rows[0].Cells[0].ColumnSpan.Should().Be(1);

        cut.Find("[data-testid='document-table-cell']").KeyDown(new KeyboardEventArgs { Key = "Tab" });
        selection.ActiveTableCellId.Should().NotBeNull();
    }

    [Fact]
    public void HeaderFooterNotesAndRevisionsAreEditable()
    {
        var document = CreateDocument(Paragraph("Alpha"));
        var cut = RenderComponent<TmDocumentSurface>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.ReadOnly, false)
            .Add(p => p.TrackChangesEnabled, true));

        cut.Find("[data-testid='document-header-surface'] input").Change("Header text");
        cut.Find("[data-testid='document-footer-surface'] input").Change("Footer text");
        cut.Find("[data-testid='document-section-options'] input").Change(true);
        cut.Find("[data-testid='document-insert-footnote']").Click();
        cut.Find("[data-testid='document-paragraph-editor']").Input("Changed");

        document.HeadersFooters.Should().HaveCount(2);
        document.Sections[0].Properties.DifferentFirstPage.Should().BeTrue();
        document.Notes.Should().ContainSingle();
        document.Revisions.Should().Contain(revision => revision.Type == DocumentRevisionType.Insertion);

        cut.Find(".tm-document-revisions-panel__item button").Click();
        document.Revisions[0].Action.Should().Be(DocumentRevisionAction.Accepted);
    }

    [Fact]
    public void ImageBlock_CanEditMetadataResizeDeleteAndToggleFloating()
    {
        var document = CreateDocument(Image());
        var cut = RenderComponent<TmDocumentSurface>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.ReadOnly, false));

        cut.Find("[data-testid='document-image-caption']").Change("New caption");
        cut.Find("[data-testid='document-image-floating']").Click();

        var image = (ImageBlockContent)document.Blocks[0].Content;
        image.Caption.Should().Be("New caption");
        image.FloatingLayout.Should().NotBeNull();
        document.Anchors.Should().Contain(anchor => anchor.Type == DocumentAnchorType.FloatingObject);

        cut.Find("[data-testid='document-image-delete']").Click();
        document.Blocks.Should().BeEmpty();
    }

    [Fact]
    public async Task InsertPanel_CanInsertBlocksAndSlashRemainsPlainText()
    {
        var document = CreateDocument(Paragraph("/"));
        var provider = new SeededProvider(document);
        var cut = RenderComponent<TmDocumentEditor>(parameters => parameters
            .Add(p => p.DocumentId, document.DocumentId)
            .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-insert-menu']").Should().NotBeNull());
        cut.Find("[data-testid='document-insert-menu']").Click();
        cut.Find("[data-testid='document-insert-heading']").Click();

        var loaded = await provider.LoadAsync(document.DocumentId);
        loaded.Document!.Blocks.Select(GetText).Should().Contain("/");
        cut.FindAll("[data-testid='document-insert-panel']").Should().BeEmpty();
    }

    [Fact]
    public void ImageDialog_InsertsUrlAndProviderImages()
    {
        var document = CreateDocument(Paragraph("Alpha"));
        var provider = new DemoImageProvider();
        var cut = RenderComponent<TmDocumentEditor>(parameters => parameters
            .Add(p => p.DocumentId, document.DocumentId)
            .Add(p => p.Provider, new SeededProvider(document))
            .Add(p => p.ImageProvider, provider)
            .Add(p => p.ImageUrlResolver, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-insert-menu']").Should().NotBeNull());
        cut.Find("[data-testid='document-insert-menu']").Click();
        cut.Find("[data-testid='document-open-image-dialog']").Click();
        cut.Find("[data-testid='document-image-url-input']").Input(SafePngDataUrl);
        cut.Find("[data-testid='document-insert-image-url']").Click();

        cut.Find("[data-testid='document-insert-menu']").Click();
        cut.Find("[data-testid='document-open-image-dialog']").Click();
        cut.Find("[data-testid='document-upload-demo-image']").Click();

        cut.WaitForAssertion(() => cut.FindAll("img.tm-document-image__media").Should().HaveCount(2));
    }

    [Fact]
    public async Task ClipboardPasteCallback_UploadsImageAndInsertsBlock()
    {
        var document = CreateDocument(Paragraph("Alpha"));
        var provider = new DemoImageProvider();
        var cut = RenderComponent<TmDocumentSurface>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.ReadOnly, false)
            .Add(p => p.ImageProvider, provider));

        await cut.Instance.OnClipboardImagePasted("image/png", "paste.png", 1, "AA==");

        document.Blocks.Should().Contain(block => block.Type == DocumentBlockType.Image);
    }

    [Fact]
    public async Task ClipboardPasteCallback_WithoutProviderShowsLocalizedError()
    {
        var document = CreateDocument(Paragraph("Alpha"));
        var cut = RenderComponent<TmDocumentSurface>(parameters => parameters
            .Add(p => p.Document, document)
            .Add(p => p.ReadOnly, false));

        await cut.Instance.OnClipboardImagePasted("image/png", "paste.png", 1, "AA==");

        cut.Find(".tm-document-paste-error").TextContent.Should().Contain("Image provider is not configured");
    }

    [Fact]
    public async Task Editor_SavePersistsChangedDocumentToProvider()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");
        var cut = RenderComponent<TmDocumentEditor>(parameters => parameters
            .Add(p => p.DocumentId, "doc-1")
            .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-paragraph-editor']").Should().NotBeNull());
        cut.Find("[data-testid='document-paragraph-editor']").Input("Saved text");
        cut.Find("[data-testid='document-save']").Click();

        cut.WaitForAssertion(() => cut.Find(".tm-document-editor__save-message").TextContent.Should().Contain("Saved"));
        var saved = (await provider.LoadAsync("doc-1")).Document!;
        saved.Blocks.Select(GetText).Should().Contain("Saved text");
    }

    private const string SafePngDataUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";

    private static DocumentEditorDocument CreateDocument(params DocumentBlock[] blocks)
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Metadata.Title = "Editing test";
        document.Blocks.AddRange(blocks.Select((block, index) =>
        {
            block.Order = (index + 1) * 10;
            return block;
        }));
        return document;
    }

    private static DocumentBlock Paragraph(string text) => Paragraph(new TextRun { Text = text });

    private static DocumentBlock Paragraph(params InlineContent[] inlines) => new()
    {
        Type = DocumentBlockType.Paragraph,
        Content = new ParagraphBlockContent { Inlines = [.. inlines] }
    };

    private static DocumentBlock Heading(int level, string text) => new()
    {
        Type = DocumentBlockType.Heading,
        Content = new HeadingBlockContent { Level = level, Inlines = [new TextRun { Text = text }] }
    };

    private static DocumentBlock List(string text, bool ordered) => new()
    {
        Type = DocumentBlockType.List,
        Content = new ListBlockContent { Ordered = ordered, Inlines = [new TextRun { Text = text }] }
    };

    private static DocumentBlock Table() => new()
    {
        Type = DocumentBlockType.Table,
        Content = new TableBlockContent
        {
            Rows =
            [
                new TableRowContent { Cells = [Cell("A1"), Cell("A2")] },
                new TableRowContent { Cells = [Cell("B1"), Cell("B2")] }
            ]
        }
    };

    private static DocumentBlock Image() => new()
    {
        Type = DocumentBlockType.Image,
        Content = new ImageBlockContent
        {
            Source = DocumentImageSource.Url,
            Url = SafePngDataUrl,
            AltText = "Alt"
        }
    };

    private static TableCellContent Cell(string text) => new()
    {
        Blocks = [Paragraph(text)]
    };

    private static string GetText(DocumentBlock block)
    {
        return block.Content switch
        {
            ParagraphBlockContent paragraph => GetInlineText(paragraph.Inlines),
            HeadingBlockContent heading => GetInlineText(heading.Inlines),
            ListBlockContent list => GetInlineText(list.Inlines),
            _ => string.Empty
        };
    }

    private static string GetInlineText(IEnumerable<InlineContent> inlines)
    {
        return string.Concat(inlines.Select(inline => inline switch
        {
            TextRun text => text.Text,
            TokenRun token => string.IsNullOrWhiteSpace(token.DisplayName) ? token.Key : token.DisplayName,
            _ => string.Empty
        }));
    }

    private sealed class SeededProvider : InMemoryDocumentEditorProvider
    {
        public SeededProvider(DocumentEditorDocument document)
        {
            SaveAsync(new DocumentEditorSaveRequest
            {
                DocumentId = document.DocumentId,
                Document = document,
                ConcurrencyMode = DocumentEditorConcurrencyMode.Force
            }).GetAwaiter().GetResult();
        }
    }

    private sealed class TestTokenProvider : ITokenDataProvider
    {
        private readonly IReadOnlyList<IToken> _tokens =
        [
            new TestToken("client.name", "Client name", "Client full name", "Client", "Text"),
            new TestToken("case.number", "Case number", "Case reference", "Case", "Text")
        ];

        public bool SupportsCreation => false;

        public Task<IEnumerable<IToken>> SearchTokensAsync(string query, CancellationToken ct = default)
        {
            var result = _tokens.Where(token =>
                token.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
                || token.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(result);
        }

        public void Refresh()
        {
        }
    }

    private sealed record TestToken(
        string Key,
        string DisplayName,
        string? Description,
        string? Category,
        string? TypeLabel) : IToken
    {
        public string? Icon => null;

        public string? ColorClass => null;
    }

    private sealed class DemoImageProvider : IDocumentImageProvider, IDocumentImageUrlResolver
    {
        private readonly Dictionary<string, string> _assets = [];

        public Task<string> ResolveUrlAsync(string documentId, string assetId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_assets.TryGetValue(assetId, out var url) ? url : string.Empty);
        }

        public async Task<DocumentImageUploadResult> UploadAsync(DocumentImageUploadRequest request, Stream stream, CancellationToken cancellationToken = default)
        {
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            var assetId = Guid.NewGuid().ToString("N");
            var url = $"data:{request.ContentType};base64,{Convert.ToBase64String(memory.ToArray())}";
            _assets[assetId] = url;
            return new DocumentImageUploadResult { Success = true, AssetId = assetId, Url = url };
        }

        public Task<DocumentImageResolveResult> ResolveAsync(DocumentImageResolveRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DocumentImageResolveResult());
        }

        public Task DeleteDraftAssetAsync(string documentId, string assetId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<DocumentImageCommitResult> CommitAssetsAsync(string documentId, IReadOnlyList<string> assetIds, CancellationToken cancellationToken = default)
            => Task.FromResult(new DocumentImageCommitResult { Success = true, AssetIds = [.. assetIds] });

        public Task<DocumentImageResolveResult> RefreshUrlAsync(DocumentImageResolveRequest request, CancellationToken cancellationToken = default)
            => ResolveAsync(request, cancellationToken);
    }
}
