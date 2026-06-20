using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.NotionEditor.Blocks.Text;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public class TmNotionCalloutBlockVariantTests : LocalizationTestBase
{
    [Theory]
    [InlineData(CalloutVariant.Info, "tm-notion-callout--info", "ℹ️")]
    [InlineData(CalloutVariant.Note, "tm-notion-callout--note", "📝")]
    [InlineData(CalloutVariant.Warning, "tm-notion-callout--warning", "⚠️")]
    [InlineData(CalloutVariant.Error, "tm-notion-callout--error", "❌")]
    [InlineData(CalloutVariant.Success, "tm-notion-callout--success", "✅")]
    public void CalloutBlock_RendersVariantClassAndSemanticIcon(
        CalloutVariant variant,
        string expectedClass,
        string expectedIcon)
    {
        var content = new CalloutBlockContent
        {
            Html = "Panel content",
            Variant = variant
        };

        var cut = RenderComponent<TmNotionCalloutBlock>(parameters => parameters
            .Add(p => p.Content, content)
            .Add(p => p.ReadOnly, true));

        var callout = cut.Find(".tm-notion-callout");
        callout.ClassList.Should().Contain(expectedClass);
        callout.GetAttribute("data-variant").Should().Be(variant.ToString().ToLowerInvariant());
        cut.Find(".tm-notion-callout__icon").TextContent.Should().Contain(expectedIcon);
    }

    [Fact]
    public void CalloutBlock_DefaultVariant_KeepsEmojiPickerButton()
    {
        var content = new CalloutBlockContent
        {
            Html = "Default callout",
            IconEmoji = "💡",
            Variant = CalloutVariant.Default
        };

        var cut = RenderComponent<TmNotionCalloutBlock>(parameters => parameters
            .Add(p => p.Content, content)
            .Add(p => p.ReadOnly, false));

        cut.Find(".tm-notion-callout").ClassList.Should().NotContain("tm-notion-callout--info");
        cut.Find(".tm-notion-callout__icon").TagName.Should().Be("BUTTON");
    }
}
