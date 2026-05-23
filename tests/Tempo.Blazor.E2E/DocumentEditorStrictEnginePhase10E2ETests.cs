using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E;

/// <summary>Strict tests for page frames, pagination, and header/footer region layout.</summary>
[TestClass]
[DoNotParallelize]
public sealed class DocumentEditorStrictEnginePhase10E2ETests : DocumentEditorE2ETestBase
{
    [TestMethod]
    public async Task DocumentEditor_Strict_PageLayout_ExposesExplicitFramesAndRenderedBodyFrame()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<PageFrameProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase10-frame',
                    Blocks: [{ Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Frame text' }] } }]
                });
                const paragraph = engine.textLayout.createParagraphLayoutEngine(null, {
                    page: { x: 0, y: 0, width: 420, height: 540 },
                    margins: { top: 40, right: 30, bottom: 50, left: 20 },
                    headerHeight: 36,
                    footerHeight: 32
                });
                const layout = paragraph.layoutDocument(model);
                const snapshot = engine.rendering.createRenderSnapshot(model, layout, { blockId: 'p1', offset: 0 });
                const root = document.createElement('div');
                document.body.appendChild(root);
                engine.rendering.createAtomicRenderer().render(root, snapshot);
                const renderedBody = root.querySelector('[data-render-frame="body"]');
                const renderedHeader = root.querySelector('[data-render-frame="header"]');
                const renderedFooter = root.querySelector('[data-render-frame="footer"]');
                root.remove();
                return {
                    pageCount: layout.pages.length,
                    pageWidth: layout.pageMetrics.pageSize.width,
                    pageHeight: layout.pageMetrics.pageSize.height,
                    bodyX: layout.pages[0].bodyFrame.x,
                    bodyY: layout.pages[0].bodyFrame.y,
                    bodyWidth: layout.pages[0].bodyFrame.width,
                    bodyHeight: layout.pages[0].bodyFrame.height,
                    headerHeight: layout.pages[0].headerFrame.height,
                    footerHeight: layout.pages[0].footerFrame.height,
                    renderedBodyFrame: !!renderedBody,
                    renderedBodyLeft: renderedBody ? renderedBody.style.left : '',
                    renderedBodyWidth: renderedBody ? renderedBody.style.width : '',
                    renderedHeaderFrame: !!renderedHeader,
                    renderedFooterFrame: !!renderedFooter
                };
            }
            """);

        result.PageCount.Should().Be(1);
        result.PageWidth.Should().Be(420);
        result.PageHeight.Should().Be(540);
        result.BodyX.Should().Be(20);
        result.BodyY.Should().Be(76);
        result.BodyWidth.Should().Be(370);
        result.BodyHeight.Should().Be(382);
        result.HeaderHeight.Should().Be(36);
        result.FooterHeight.Should().Be(32);
        result.RenderedBodyFrame.Should().BeTrue();
        result.RenderedBodyLeft.Should().Be("20px");
        result.RenderedBodyWidth.Should().Be("370px");
        result.RenderedHeaderFrame.Should().BeTrue();
        result.RenderedFooterFrame.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_PageLayout_FlowsBlocksInOrderAndPaginatesWithoutBodyOverflow()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<BlockFlowProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const blocks = Array.from({ length: 10 }, (_, index) => ({
                    Id: `p${index + 1}`,
                    Type: 'Paragraph',
                    Content: { Inlines: [{ Id: `r${index + 1}`, Text: `Paragraph ${index + 1} with enough words to use measurable line height.` }] }
                }));
                blocks.splice(2, 0, {
                    Id: 'img1',
                    Type: 'Image',
                    Content: { Id: 'obj1', AltText: 'Image', Layout: { Width: 180, Height: 72, WrapMode: 'TopBottom' } }
                });
                const model = engine.model.importFromCSharpJson({ DocumentId: 'phase10-flow', Blocks: blocks });
                const layout = engine.textLayout.createParagraphLayoutEngine(null, {
                    page: { x: 0, y: 0, width: 300, height: 210 },
                    margins: { top: 18, right: 20, bottom: 18, left: 20 },
                    headerHeight: 16,
                    footerHeight: 16,
                    blockGap: 8
                }).layoutDocument(model);
                const ordered = layout.blocks.map(block => block.blockId);
                const overflowCount = layout.blocks.filter(block => {
                    const page = layout.pages[block.pageIndex || 0];
                    const body = page.bodyFrame;
                    return block.rect.y < body.y - 0.1 || block.rect.y + block.rect.height > body.y + body.height + 0.1;
                }).length;
                const image = layout.blocks.find(block => block.blockId === 'img1');
                const afterImage = layout.blocks.find(block => block.blockId === 'p3');
                return {
                    pageCount: layout.pages.length,
                    ordered,
                    overflowCount,
                    hasExplicitSpacing: layout.debug.explicitParagraphSpacing === true,
                    currentYOwnedByLayout: layout.debug.currentYOwnedByLayout === true,
                    imageCreatesExclusion: layout.pages.some(page => (page.exclusions || []).some(item => item.objectId === 'obj1')),
                    followingBlockRespectsImage: !!image && !!afterImage && afterImage.rect.y >= image.rect.y + image.rect.height
                };
            }
            """);

        result.PageCount.Should().BeGreaterThan(1);
        result.Ordered.Should().ContainInOrder("p1", "p2", "img1", "p3", "p4", "p5");
        result.OverflowCount.Should().Be(0);
        result.HasExplicitSpacing.Should().BeTrue();
        result.CurrentYOwnedByLayout.Should().BeTrue();
        result.ImageCreatesExclusion.Should().BeTrue();
        result.FollowingBlockRespectsImage.Should().BeTrue();
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_PageLayout_SupportsManualBreakParagraphSplitAndCaretPage()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<PageBreakProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const longText = Array.from({ length: 60 }, (_, index) => `word${index}`).join(' ');
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase10-break',
                    Blocks: [
                        { Id: 'p1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r1', Text: 'Before break' }] } },
                        { Id: 'break1', Type: 'PageBreak', Content: {} },
                        { Id: 'p2', Type: 'Paragraph', Content: { Inlines: [{ Id: 'r2', Text: longText }] } }
                    ]
                });
                const layout = engine.textLayout.createParagraphLayoutEngine(null, {
                    page: { x: 0, y: 0, width: 260, height: 180 },
                    margins: { top: 12, right: 18, bottom: 12, left: 18 },
                    headerHeight: 12,
                    footerHeight: 12,
                    blockGap: 6
                }).layoutDocument(model, { selection: { blockId: 'p2', offset: 12, isCollapsed: true } });
                const p2Fragments = layout.blocks.filter(block => block.blockId === 'p2');
                const caret = layout.caretStops.find(stop => stop.blockId === 'p2' && Number(stop.offset) === 12);
                const lineOverflowCount = p2Fragments.flatMap(block => block.lines || []).filter(line => {
                    const body = layout.pages[line.pageIndex || 0].bodyFrame;
                    return line.rect.y < body.y - 0.1 || line.rect.y + line.rect.height > body.y + body.height + 0.1;
                }).length;
                return {
                    pageCount: layout.pages.length,
                    breakPageIndex: layout.blocks.find(block => block.blockId === 'break1')?.pageIndex ?? -1,
                    p2FirstPageIndex: p2Fragments[0]?.pageIndex ?? -1,
                    p2FragmentCount: p2Fragments.length,
                    lineOverflowCount,
                    keepWithNextPrepared: layout.debug.keepWithNextPrepared === true,
                    caretPageIndex: caret?.pageIndex ?? -1
                };
            }
            """);

        result.PageCount.Should().BeGreaterThan(2);
        result.BreakPageIndex.Should().Be(0);
        result.P2FirstPageIndex.Should().Be(1);
        result.P2FragmentCount.Should().BeGreaterThan(1);
        result.LineOverflowCount.Should().Be(0);
        result.KeepWithNextPrepared.Should().BeTrue();
        result.CaretPageIndex.Should().BeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public async Task DocumentEditor_Strict_PageLayout_RendersHeaderFooterFieldsAndRegionInputImmediately()
    {
        var page = await OpenDocumentEditorAsync(width: 1280, height: 720);

        var result = await page.EvaluateAsync<HeaderFooterProbe>(
            """
            () => {
                const engine = window.tmDocumentEditorEngine;
                const model = engine.model.importFromCSharpJson({
                    DocumentId: 'phase10-hf',
                    Blocks: Array.from({ length: 8 }, (_, index) => ({
                        Id: `p${index + 1}`,
                        Type: 'Paragraph',
                        Content: { Inlines: [{ Id: `r${index + 1}`, Text: `Body paragraph ${index + 1} wraps onto pages.` }] }
                    })),
                    HeadersFooters: [
                        { Id: 'h1', Region: 'Header', Blocks: [{ Id: 'hp1', Type: 'Paragraph', Content: { Inlines: [
                            { Id: 'hr1', Text: 'Page ' },
                            { Id: 'hfPage', Type: 'Field', FieldType: 'PageNumber' },
                            { Id: 'hr2', Text: '/' },
                            { Id: 'hfTotal', Type: 'Field', FieldType: 'TotalPages' }
                        ] } }] },
                        { Id: 'f1', Region: 'Footer', Blocks: [{ Id: 'fp1', Type: 'Paragraph', Content: { Inlines: [{ Id: 'fr1', Text: 'Footer' }] } }] }
                    ]
                });
                const paragraph = engine.textLayout.createParagraphLayoutEngine(null, {
                    page: { x: 0, y: 0, width: 280, height: 170 },
                    margins: { top: 12, right: 18, bottom: 12, left: 18 },
                    headerHeight: 18,
                    footerHeight: 18,
                    blockGap: 6
                });
                const layout = paragraph.layoutDocument(model);
                const snapshot = engine.rendering.createRenderSnapshot(model, layout, { region: 'Header', blockId: 'hp1', offset: 0 });
                const root = document.createElement('div');
                document.body.appendChild(root);
                engine.rendering.createAtomicRenderer().render(root, snapshot);
                const firstHeaderText = root.querySelector('[data-render-region="Header"][data-render-page-index="0"]')?.textContent || '';
                const secondHeaderText = root.querySelector('[data-render-region="Header"][data-render-page-index="1"]')?.textContent || '';
                const footerRegionCount = root.querySelectorAll('[data-render-region="Footer"]').length;
                const input = engine.input.createInputPipeline({
                    model,
                    selection: { region: 'Footer', headerFooterId: 'f1', blockId: 'fp1', offset: 6, isCollapsed: true },
                    page: { x: 0, y: 0, width: 280, height: 170 }
                });
                const typed = input.insertText(' now');
                const footerText = model.footers[0].blocks[0].content.runs.map(run => run.text).join('');
                const bodyText = model.body.blocks[0].content.runs.map(run => run.text).join('');
                root.remove();
                return {
                    pageCount: layout.pages.length,
                    headerRegionCount: layout.headerFooterRegions.filter(region => region.region === 'Header').length,
                    footerRegionCount,
                    firstHeaderText,
                    secondHeaderText,
                    typedOk: typed.ok === true,
                    typedRegion: String(typed.selection?.region || ''),
                    footerText,
                    bodyText
                };
            }
            """);

        result.PageCount.Should().BeGreaterThan(1);
        result.HeaderRegionCount.Should().Be(result.PageCount);
        result.FooterRegionCount.Should().Be(result.PageCount);
        result.FirstHeaderText.Should().Contain($"Page 1/{result.PageCount}");
        result.SecondHeaderText.Should().Contain($"Page 2/{result.PageCount}");
        result.TypedOk.Should().BeTrue();
        result.TypedRegion.Should().Be("Footer");
        result.FooterText.Should().Be("Footer now");
        result.BodyText.Should().StartWith("Body paragraph 1");
    }

    public sealed class PageFrameProbe
    {
        [JsonPropertyName("pageCount")] public int PageCount { get; set; }
        [JsonPropertyName("pageWidth")] public double PageWidth { get; set; }
        [JsonPropertyName("pageHeight")] public double PageHeight { get; set; }
        [JsonPropertyName("bodyX")] public double BodyX { get; set; }
        [JsonPropertyName("bodyY")] public double BodyY { get; set; }
        [JsonPropertyName("bodyWidth")] public double BodyWidth { get; set; }
        [JsonPropertyName("bodyHeight")] public double BodyHeight { get; set; }
        [JsonPropertyName("headerHeight")] public double HeaderHeight { get; set; }
        [JsonPropertyName("footerHeight")] public double FooterHeight { get; set; }
        [JsonPropertyName("renderedBodyFrame")] public bool RenderedBodyFrame { get; set; }
        [JsonPropertyName("renderedBodyLeft")] public string RenderedBodyLeft { get; set; } = string.Empty;
        [JsonPropertyName("renderedBodyWidth")] public string RenderedBodyWidth { get; set; } = string.Empty;
        [JsonPropertyName("renderedHeaderFrame")] public bool RenderedHeaderFrame { get; set; }
        [JsonPropertyName("renderedFooterFrame")] public bool RenderedFooterFrame { get; set; }
    }

    public sealed class BlockFlowProbe
    {
        [JsonPropertyName("pageCount")] public int PageCount { get; set; }
        [JsonPropertyName("ordered")] public string[] Ordered { get; set; } = [];
        [JsonPropertyName("overflowCount")] public int OverflowCount { get; set; }
        [JsonPropertyName("hasExplicitSpacing")] public bool HasExplicitSpacing { get; set; }
        [JsonPropertyName("currentYOwnedByLayout")] public bool CurrentYOwnedByLayout { get; set; }
        [JsonPropertyName("imageCreatesExclusion")] public bool ImageCreatesExclusion { get; set; }
        [JsonPropertyName("followingBlockRespectsImage")] public bool FollowingBlockRespectsImage { get; set; }
    }

    public sealed class PageBreakProbe
    {
        [JsonPropertyName("pageCount")] public int PageCount { get; set; }
        [JsonPropertyName("breakPageIndex")] public int BreakPageIndex { get; set; }
        [JsonPropertyName("p2FirstPageIndex")] public int P2FirstPageIndex { get; set; }
        [JsonPropertyName("p2FragmentCount")] public int P2FragmentCount { get; set; }
        [JsonPropertyName("lineOverflowCount")] public int LineOverflowCount { get; set; }
        [JsonPropertyName("keepWithNextPrepared")] public bool KeepWithNextPrepared { get; set; }
        [JsonPropertyName("caretPageIndex")] public int CaretPageIndex { get; set; }
    }

    public sealed class HeaderFooterProbe
    {
        [JsonPropertyName("pageCount")] public int PageCount { get; set; }
        [JsonPropertyName("headerRegionCount")] public int HeaderRegionCount { get; set; }
        [JsonPropertyName("footerRegionCount")] public int FooterRegionCount { get; set; }
        [JsonPropertyName("firstHeaderText")] public string FirstHeaderText { get; set; } = string.Empty;
        [JsonPropertyName("secondHeaderText")] public string SecondHeaderText { get; set; } = string.Empty;
        [JsonPropertyName("typedOk")] public bool TypedOk { get; set; }
        [JsonPropertyName("typedRegion")] public string TypedRegion { get; set; } = string.Empty;
        [JsonPropertyName("footerText")] public string FooterText { get; set; } = string.Empty;
        [JsonPropertyName("bodyText")] public string BodyText { get; set; } = string.Empty;
    }
}
