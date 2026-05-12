using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class SigningTextResolverTests : LocalizationTestBase
{
    private readonly MockTmLocalizer _localizer = new(new Dictionary<string, string>
    {
        ["TmSigning_Field_Text"] = "Text",
        ["TmSigning_Field_Signature"] = "Signature"
    });

    [Fact]
    public void FieldLabel_ReturnsCzechTranslation()
    {
        var field = CreateField();
        field.Labels.Translations["cs"] = "Celé jméno";

        SigningTextResolver.FieldLabel(field, "cs-CZ", "en-US", _localizer).Should().Be("Celé jméno");
    }

    [Fact]
    public void FieldLabel_ReturnsEnglishTranslation()
    {
        var field = CreateField();
        field.Labels.Translations["en"] = "Full name";

        SigningTextResolver.FieldLabel(field, "en-US", "cs-CZ", _localizer).Should().Be("Full name");
    }

    [Fact]
    public void FieldLabel_ReturnsFallbackWhenTranslationIsMissing()
    {
        var field = CreateField();
        field.Labels.Default = "Výchozí jméno";

        SigningTextResolver.FieldLabel(field, "de-DE", "cs-CZ", _localizer).Should().Be("Výchozí jméno");
    }

    [Fact]
    public void FieldLabel_UsesLocalizerAsFinalFieldTypeFallback()
    {
        var field = new SigningField
        {
            Uuid = "signature",
            Type = SigningFieldType.Signature
        };

        SigningTextResolver.FieldLabel(field, "de-DE", "cs-CZ", _localizer).Should().Be("Signature");
    }

    [Fact]
    public void OptionLabel_KeepsOptionValueStable()
    {
        var option = new SigningFieldOption
        {
            Uuid = "delivery-email",
            Value = "email",
            Labels = { Translations = { ["cs"] = "E-mail" } }
        };

        SigningTextResolver.OptionLabel(option, "cs-CZ", "en-US").Should().Be("E-mail");
        option.Value.Should().Be("email");
    }

    [Fact]
    public void FieldLabel_PreservesCzechDiacritics()
    {
        var field = CreateField();
        field.Labels.Translations["cs"] = "Podepisující osoba";

        SigningTextResolver.FieldLabel(field, "cs-CZ", "en-US", _localizer).Should().Be("Podepisující osoba");
    }

    [Fact]
    public void FieldLabel_FallsBackFromSpecificCultureToNeutralCulture()
    {
        var field = CreateField();
        field.Labels.Translations["cs"] = "Český popisek";

        SigningTextResolver.FieldLabel(field, "cs-CZ", "en-US", _localizer).Should().Be("Český popisek");
    }

    [Fact]
    public void FieldLabel_UnknownCultureFallsBackToDefault()
    {
        var field = CreateField();
        field.Labels.Default = "Default label";

        SigningTextResolver.FieldLabel(field, "zz-ZZ", "de-DE", _localizer).Should().Be("Default label");
    }

    private static SigningField CreateField()
    {
        return new SigningField
        {
            Uuid = "full-name",
            Name = "Full name",
            Type = SigningFieldType.Text
        };
    }
}
