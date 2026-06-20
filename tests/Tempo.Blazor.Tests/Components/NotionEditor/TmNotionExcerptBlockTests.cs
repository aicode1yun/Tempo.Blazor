using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Blocks.Special;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionExcerptBlockTests : LocalizationTestBase
{
    public TmNotionExcerptBlockTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Notion_Excerpt_Title"] = "Excerpt",
            ["Notion_Excerpt_Placeholder"] = "Write a reusable page summary"
        });
    }

    [Fact]
    public void ExcerptBlock_RendersEditableExcerpt()
    {
        var content = new ExcerptBlockContent { Html = "Reusable <strong>summary</strong>" };

        var cut = RenderComponent<TmNotionExcerptBlock>(parameters => parameters
            .Add(component => component.Block, MakeBlock(content))
            .Add(component => component.Content, content));

        var editable = cut.Find(".tm-excerpt__editor");
        editable.GetAttribute("contenteditable").Should().Be("true");
        editable.GetAttribute("data-placeholder").Should().Be("Write a reusable page summary");
        editable.InnerHtml.Should().Contain("Reusable <strong>summary</strong>");
    }

    [Fact]
    public void ExcerptBlock_SavesEditedHtmlOnBlur()
    {
        JSInterop.Setup<string>("tmNotionEditor.getHtml", _ => true).SetResult("Updated <em>summary</em>");
        ExcerptBlockContent? saved = null;
        var content = new ExcerptBlockContent { Html = "Initial summary" };

        var cut = RenderComponent<TmNotionExcerptBlock>(parameters => parameters
            .Add(component => component.Block, MakeBlock(content))
            .Add(component => component.Content, content)
            .Add(component => component.OnContentChanged, EventCallback.Factory.Create<ExcerptBlockContent>(
                this,
                value => saved = value)));

        cut.Find(".tm-excerpt__editor").Input(string.Empty);
        cut.Find(".tm-excerpt__editor").Blur();

        saved.Should().NotBeNull();
        saved!.Html.Should().Be("Updated <em>summary</em>");
    }

    private static PageBlock MakeBlock(IBlockContent content) => new()
    {
        Id = Guid.Parse("cf140000-0000-0000-0000-000000000010"),
        PageId = Guid.Parse("cf140000-0000-0000-0000-000000000001"),
        Type = BlockType.Excerpt,
        Order = 0,
        Content = content
    };
}
