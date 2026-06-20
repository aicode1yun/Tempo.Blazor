using Tempo.Blazor.EmailTemplates.Abstractions.Layout;
using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;
using Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests.Rendering;

public class EmailDocumentValidatorTests
{
    private static readonly EmailDocumentValidator Validator = new();

    private static EmailTemplateDocument DocWith(EmailBlockBase block)
    {
        var doc = new EmailTemplateDocument();
        var section = new EmailSection();
        var col = new EmailColumn();
        col.Blocks.Add(block);
        section.Columns.Add(col);
        doc.Sections.Add(section);
        return doc;
    }

    [Fact]
    public void Button_WithoutHref_IsError()
    {
        var messages = Validator.Validate(DocWith(new EmailButtonBlock { Text = "Go" }));

        messages.Should().ContainSingle(m => m.Key == DocumentValidationKeys.ButtonHrefMissing)
            .Which.Severity.Should().Be(LayoutSeverity.Error);
    }

    [Fact]
    public void Image_WithoutAlt_IsWarning()
    {
        var messages = Validator.Validate(DocWith(new EmailImageBlock { Src = "https://a/b.png", Alt = "" }));

        messages.Should().ContainSingle(m => m.Key == DocumentValidationKeys.ImageAltMissing)
            .Which.Severity.Should().Be(LayoutSeverity.Warning);
    }

    [Fact]
    public void Image_WithoutSrc_IsError()
    {
        var messages = Validator.Validate(DocWith(new EmailImageBlock { Src = "", Alt = "x" }));

        messages.Should().Contain(m => m.Key == DocumentValidationKeys.ImageSrcMissing
                                       && m.Severity == LayoutSeverity.Error);
    }

    [Fact]
    public void IncludesLayoutFindings()
    {
        var doc = new EmailTemplateDocument();
        doc.Sections.Add(new EmailSection()); // empty section → layout warning

        var messages = Validator.Validate(doc);

        messages.Should().Contain(m => m.Key == LayoutValidationKeys.EmptySection);
    }

    [Fact]
    public void ValidDocument_HasNoErrors()
    {
        var doc = DocWith(new EmailButtonBlock { Text = "Go", Href = "https://ok" });

        var messages = Validator.Validate(doc);

        messages.Should().NotContain(m => m.Severity == LayoutSeverity.Error);
    }

    [Fact]
    public void Messages_UseKeysNotText()
    {
        var messages = Validator.Validate(DocWith(new EmailButtonBlock { Text = "Go" }));
        messages.Should().OnlyContain(m => m.Key.Contains('.'));
    }
}
