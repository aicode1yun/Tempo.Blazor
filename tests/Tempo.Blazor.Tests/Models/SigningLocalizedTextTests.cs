using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Tests.Models;

public class SigningLocalizedTextTests
{
    [Fact]
    public void Resolve_EmptyText_ReturnsEmptyString()
    {
        new SigningLocalizedText().Resolve("cs-CZ").Should().BeEmpty();
    }

    [Fact]
    public void Resolve_WithoutCulture_UsesDefault()
    {
        new SigningLocalizedText { Default = "Default label" }.Resolve().Should().Be("Default label");
    }

    [Fact]
    public void Resolve_ExactCulture_WinsOverNeutralCulture()
    {
        var text = new SigningLocalizedText
        {
            Default = "Default",
            Translations =
            {
                ["cs"] = "Česky",
                ["cs-CZ"] = "Česky pro ČR"
            }
        };

        text.Resolve("cs-CZ").Should().Be("Česky pro ČR");
    }

    [Fact]
    public void Resolve_NeutralCulture_IsUsedForSpecificCulture()
    {
        var text = new SigningLocalizedText
        {
            Default = "Default",
            Translations = { ["cs"] = "Česky" }
        };

        text.Resolve("cs-CZ").Should().Be("Česky");
    }

    [Fact]
    public void Resolve_FallbackCulture_WinsOverDefault()
    {
        var text = new SigningLocalizedText
        {
            Default = "Default",
            Translations = { ["en"] = "English" }
        };

        text.Resolve("de-DE", "en-US").Should().Be("English");
    }

    [Fact]
    public void Resolve_TrimsCultureAndIgnoresCase()
    {
        var text = new SigningLocalizedText
        {
            Translations = { ["CS"] = "Česky" }
        };

        text.Resolve(" cs-CZ ").Should().Be("Česky");
    }

    [Fact]
    public void Resolve_JsonRoundtrip_PreservesDefaultAndTranslations()
    {
        var original = new SigningLocalizedText
        {
            Default = "Default",
            Translations = { ["cs"] = "Česky" }
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<SigningLocalizedText>(json);

        restored.Should().NotBeNull();
        restored!.Default.Should().Be("Default");
        restored.Resolve("cs-CZ").Should().Be("Česky");
    }

    [Fact]
    public void ResolveFieldOption_UsesLabelWithoutChangingValue()
    {
        var option = new SigningFieldOption
        {
            Uuid = "country-cz",
            Value = "CZ",
            Labels = { Translations = { ["cs"] = "Česká republika" } }
        };

        SigningLocalizationResolver.ResolveOptionLabel(option, "cs-CZ").Should().Be("Česká republika");
        option.Value.Should().Be("CZ");
    }
}
