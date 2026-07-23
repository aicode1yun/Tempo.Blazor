using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.NotionEditor.Blocks.Table;
using Tempo.Blazor.Components.NotionEditor.Blocks.Text;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// Table cells are written into the DOM with innerHTML, exactly like the core text blocks. Stored
/// markup must therefore be sanitized on the way out, or an onerror payload saved into a cell runs
/// on every render for every reader of the page.
/// </summary>
public sealed class NotionTableCellSanitizationTests : LocalizationTestBase
{
    private const string Payload = """x<img src=q onerror="alert(1)">""";

    [Fact]
    public void ACellIsSanitizedBeforeItIsWrittenIntoTheDom()
    {
        var cut = RenderRow(Payload);

        var written = SetHtmlArguments(cut);
        written.Should().ContainSingle();
        written[0].Should().NotContain("onerror");
        written[0].Should().NotContain("<img");
        written[0].Should().Contain("x", "the surrounding text survives");
    }

    [Fact]
    public void ACellKeepsItsInlineFormatting()
    {
        var cut = RenderRow("<strong>bold</strong> and <em>italic</em>");

        SetHtmlArguments(cut)[0].Should().Be("<strong>bold</strong> and <em>italic</em>");
    }

    [Fact]
    public void ReadOnlyHistoricalCellIsSanitizedBeforeInitialMarkupRender()
    {
        var cut = RenderRow(Payload, readOnly: true);

        cut.Markup.Should().NotContain("onerror");
        cut.Markup.Should().NotContain("<img");
        cut.Markup.Should().Contain("x", "safe surrounding text remains visible");
    }

    [Theory]
    [InlineData("url(https://evil.test/x)")]
    [InlineData("var(--evil)")]
    [InlineData("red;position:fixed")]
    [InlineData("\" onmouseover=\"alert(1)")]
    public void HistoricalUnsafeCellColorIsNotRenderedIntoStyle(string color)
    {
        var cut = RenderRow("safe", readOnly: true, backgroundColor: color);

        cut.Find("td.tm-notion-table__cell-td")
            .GetAttribute("style")
            .Should()
            .NotContain("--tm-notion-table-cell-background");
    }

    [Fact]
    public void HistoricalSafeCellColorIsNormalizedBeforeRender()
    {
        var cut = RenderRow("safe", readOnly: true, backgroundColor: "  #1F4E78  ");

        cut.Find("td.tm-notion-table__cell-td")
            .GetAttribute("style")
            .Should()
            .Contain("--tm-notion-table-cell-background:#1f4e78");
    }

    [Fact]
    public void ReadOnlyHistoricalTextBlockIsSanitizedBeforeInitialMarkupRender()
    {
        var cut = Render<TmNotionTextBlock>(parameters => parameters
            .Add(component => component.ReadOnly, true)
            .Add(
                component => component.Content,
                new TextBlockContent { Html = Payload }));

        cut.Markup.Should().NotContain("onerror");
        cut.Markup.Should().NotContain("<img");
        cut.Markup.Should().Contain("x");
    }

    [Fact]
    public void ACellKeepsTheEditorsOwnChips()
    {
        const string chip = """<span class="tm-notion-status tm-notion-status--green" data-status-label="Done">Done</span>""";

        SetHtmlArguments(RenderRow(chip))[0].Should().Contain("tm-notion-status");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private List<string> SetHtmlArguments(IRenderedComponent<TmNotionTableRowBlock> _) =>
        JSInterop.Invocations
            .Where(invocation => invocation.Identifier == "tmNotionEditor.setHtml")
            .Select(invocation => (string)invocation.Arguments[1]!)
            .ToList();

    private IRenderedComponent<TmNotionTableRowBlock> RenderRow(
        string cellHtml,
        bool readOnly = false,
        string? backgroundColor = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var row = new PageBlock
        {
            Id = Guid.NewGuid(),
            PageId = Guid.NewGuid(),
            Type = BlockType.TableRow,
            Order = 0,
            Content = new TableRowBlockContent
            {
                RichCells =
                [
                    new NotionTableCell
                    {
                        Html = cellHtml,
                        BackgroundColor = backgroundColor
                    }
                ]
            }
        };

        return Render<TmNotionTableRowBlock>(parameters => parameters
            .Add(p => p.Row, row)
            .Add(p => p.ColumnCount, 1)
            .Add(p => p.ReadOnly, readOnly));
    }
}
