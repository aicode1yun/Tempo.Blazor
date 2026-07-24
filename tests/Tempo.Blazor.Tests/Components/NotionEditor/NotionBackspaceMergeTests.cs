using Bunit;
using FluentAssertions;
using NSubstitute;
using Tempo.Blazor.Components.NotionEditor.Blocks;
using Tempo.Blazor.Components.NotionEditor.Blocks.Lists;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.Components.NotionEditor.Blocks.Text;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// notion-editor.js calls OnBackspaceAtStart when the caret sits before the first character of a
/// non-empty block. Every block that installs the shared keyboard handler must expose that
/// callback — a missing one makes the JS invoke throw — and the HTML it hands over must already
/// be sanitized, because it comes straight out of a contenteditable.
/// </summary>
public sealed class NotionBackspaceMergeTests : LocalizationTestBase
{
    [Fact]
    public async Task TextBlock_BackspaceAtStart_RaisesMergeWithTheBlockHtml()
    {
        string? merged = null;
        var cut = Render<TmNotionTextBlock>(parameters => parameters
            .Add(p => p.Content, new TextBlockContent { Html = "beta" })
            .Add(p => p.OnMergeWithPrevious, html => merged = html));

        await cut.InvokeAsync(() => cut.Instance.OnBackspaceAtStart("<em>beta</em>"));

        merged.Should().Be("<em>beta</em>");
    }

    [Fact]
    public async Task TextBlock_BackspaceAtStart_SanitizesTheHtmlItHandsOver()
    {
        string? merged = null;
        var cut = Render<TmNotionTextBlock>(parameters => parameters
            .Add(p => p.Content, new TextBlockContent { Html = "beta" })
            .Add(p => p.OnMergeWithPrevious, html => merged = html));

        await cut.InvokeAsync(() =>
            cut.Instance.OnBackspaceAtStart("""beta<img src=q onerror="alert(1)">"""));

        merged.Should().NotBeNull();
        merged.Should().NotContain("onerror");
        merged.Should().Contain("beta");
    }

    [Fact]
    public async Task TextBlock_BackspaceAtStart_WithoutAConsumer_DoesNotThrow()
    {
        // The shared JS handler invokes this on every block; a block used standalone by a 2.0.x
        // consumer has no merge callback wired and must simply do nothing.
        var cut = Render<TmNotionTextBlock>(parameters => parameters
            .Add(p => p.Content, new TextBlockContent { Html = "beta" }));

        var act = async () => await cut.InvokeAsync(() => cut.Instance.OnBackspaceAtStart("beta"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HeadingBlock_BackspaceAtStart_RaisesMerge()
    {
        string? merged = null;
        var cut = Render<TmNotionHeadingBlock>(parameters => parameters
            .Add(p => p.Content, new HeadingBlockContent { Html = "beta", Level = 1 })
            .Add(p => p.OnMergeWithPrevious, html => merged = html));

        await cut.InvokeAsync(() => cut.Instance.OnBackspaceAtStart("beta"));

        merged.Should().Be("beta");
    }

    [Fact]
    public async Task QuoteBlock_BackspaceAtStart_RaisesMerge()
    {
        string? merged = null;
        var cut = Render<TmNotionQuoteBlock>(parameters => parameters
            .Add(p => p.Content, new TextBlockContent { Html = "beta" })
            .Add(p => p.OnMergeWithPrevious, html => merged = html));

        await cut.InvokeAsync(() => cut.Instance.OnBackspaceAtStart("beta"));

        merged.Should().Be("beta");
    }

    [Fact]
    public async Task CalloutBlock_BackspaceAtStart_RaisesMerge()
    {
        string? merged = null;
        var cut = Render<TmNotionCalloutBlock>(parameters => parameters
            .Add(p => p.Content, new CalloutBlockContent { Html = "beta" })
            .Add(p => p.OnMergeWithPrevious, html => merged = html));

        await cut.InvokeAsync(() => cut.Instance.OnBackspaceAtStart("beta"));

        merged.Should().Be("beta");
    }

    [Fact]
    public async Task BulletListBlock_BackspaceAtStart_RaisesMerge()
    {
        string? merged = null;
        var cut = Render<TmNotionBulletListBlock>(parameters => parameters
            .Add(p => p.Content, new ListBlockContent { Html = "beta" })
            .Add(p => p.OnMergeWithPrevious, html => merged = html));

        await cut.InvokeAsync(() => cut.Instance.OnBackspaceAtStart("beta"));

        merged.Should().Be("beta");
    }

    [Fact]
    public async Task NumberedListBlock_BackspaceAtStart_RaisesMerge()
    {
        string? merged = null;
        var cut = Render<TmNotionNumberedListBlock>(parameters => parameters
            .Add(p => p.Content, new ListBlockContent { Html = "beta" })
            .Add(p => p.OnMergeWithPrevious, html => merged = html));

        await cut.InvokeAsync(() => cut.Instance.OnBackspaceAtStart("beta"));

        merged.Should().Be("beta");
    }

    [Fact]
    public async Task TodoBlock_BackspaceAtStart_RaisesMerge()
    {
        string? merged = null;
        var cut = Render<TmNotionTodoBlock>(parameters => parameters
            .AddCascadingValue(EditorContext())
            .Add(p => p.Content, new TodoBlockContent { Html = "beta" })
            .Add(p => p.OnMergeWithPrevious, html => merged = html));

        await cut.InvokeAsync(() => cut.Instance.OnBackspaceAtStart("beta"));

        merged.Should().Be("beta");
    }

    [Fact]
    public async Task ToggleBlock_BackspaceAtStart_RaisesMerge()
    {
        string? merged = null;
        var cut = Render<TmNotionToggleBlock>(parameters => parameters
            .AddCascadingValue(EditorContext())
            .Add(p => p.Content, new ToggleBlockContent { Html = "beta" })
            .Add(p => p.OnMergeWithPrevious, html => merged = html));

        await cut.InvokeAsync(() => cut.Instance.OnBackspaceAtStart("beta"));

        merged.Should().Be("beta");
    }

    private static NotionEditorContext EditorContext() => new()
    {
        BlockService = Substitute.For<INotionEditorBlockService>()
    };

    [Fact]
    public void TodoBlock_UsesItsOwnPlaceholder_NotTheListOne()
    {
        // An empty to-do prompting "List item" reads as a bullet; it needs its own wording.
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["TmNotionBlock_TodoPlaceholder"] = "To-do",
            ["TmNotionBlock_ListPlaceholder"] = "List item"
        });

        var cut = Render<TmNotionBlock>(parameters => parameters
            .AddCascadingValue(EditorContext())
            .Add(p => p.Block, new PageBlock
            {
                Id = Guid.NewGuid(),
                PageId = Guid.NewGuid(),
                Type = BlockType.TodoItem,
                Content = new TodoBlockContent { Html = "" }
            })
            .Add(p => p.ReadOnly, false));

        var placeholder = cut.Find(".tm-notion-todo__text[data-placeholder]").GetAttribute("data-placeholder");
        placeholder.Should().Be("To-do");
    }
}
