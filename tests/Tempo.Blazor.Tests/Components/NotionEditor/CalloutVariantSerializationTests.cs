using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public class CalloutVariantSerializationTests
{
    [Fact]
    public void CalloutBlockContent_WithVariant_RoundtripsWithoutLoss()
    {
        var content = new CalloutBlockContent
        {
            Html = "Deployment is paused until rollback checks pass.",
            IconEmoji = "ignored for semantic variants",
            Variant = CalloutVariant.Warning,
            BackgroundColor = "yellow",
            TextColor = "brown",
            Alignment = TextAlignment.Center
        };

        var json = JsonSerializer.Serialize(content);
        var restored = JsonSerializer.Deserialize<CalloutBlockContent>(json);

        restored.Should().NotBeNull();
        restored!.Variant.Should().Be(CalloutVariant.Warning);
        restored.Html.Should().Be(content.Html);
        restored.BackgroundColor.Should().Be(content.BackgroundColor);
        restored.TextColor.Should().Be(content.TextColor);
        restored.Alignment.Should().Be(content.Alignment);
    }

    [Fact]
    public void CalloutBlockContent_WithoutVariant_DeserializesAsDefault()
    {
        const string LegacyJson = """
                                  {
                                    "IconEmoji": "💡",
                                    "Html": "Legacy callout content",
                                    "BackgroundColor": "blue",
                                    "Alignment": 2
                                  }
                                  """;

        var restored = JsonSerializer.Deserialize<CalloutBlockContent>(LegacyJson);

        restored.Should().NotBeNull();
        restored!.Variant.Should().Be(CalloutVariant.Default);
        restored.Html.Should().Be("Legacy callout content");
    }
}
