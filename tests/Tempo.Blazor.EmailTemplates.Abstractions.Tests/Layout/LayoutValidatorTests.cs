using Tempo.Blazor.EmailTemplates.Abstractions.Layout;
using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests.Layout;

public class LayoutValidatorTests
{
    private static readonly LayoutValidator Validator = new();

    private static EmailTemplateDocument WithSection(EmailSection section)
    {
        var doc = new EmailTemplateDocument();
        doc.Sections.Add(section);
        return doc;
    }

    [Fact]
    public void ValidWidths_ProduceNoWidthMessage()
    {
        var section = new EmailSection();
        section.AddColumn(new EmailColumn());
        section.AddColumn(new EmailColumn()); // 50/50

        var messages = Validator.Validate(WithSection(section));

        messages.Should().NotContain(m => m.Key == LayoutValidationKeys.ColumnWidths);
    }

    [Fact]
    public void ExplicitWidthsNotSummingTo100_AreAnError()
    {
        var section = new EmailSection();
        section.Columns.Add(new EmailColumn { Width = "50%" });
        section.Columns.Add(new EmailColumn { Width = "60%" });

        var messages = Validator.Validate(WithSection(section));

        messages.Should().ContainSingle(m => m.Key == LayoutValidationKeys.ColumnWidths)
            .Which.Severity.Should().Be(LayoutSeverity.Error);
    }

    [Fact]
    public void EmptySection_IsAWarning()
    {
        var messages = Validator.Validate(WithSection(new EmailSection()));

        messages.Should().ContainSingle(m => m.Key == LayoutValidationKeys.EmptySection)
            .Which.Severity.Should().Be(LayoutSeverity.Warning);
    }

    [Fact]
    public void ExcessiveNesting_IsAnError()
    {
        // wrapper → wrapper → wrapper → wrapper exceeds the max nesting depth
        EmailBlockBase Nest(int depth)
        {
            if (depth == 0) return new EmailTextBlock();
            var wrapper = new EmailWrapperBlock();
            var section = new EmailSection();
            var column = new EmailColumn();
            column.Blocks.Add(Nest(depth - 1));
            section.Columns.Add(column);
            wrapper.Sections.Add(section);
            return wrapper;
        }

        var outerSection = new EmailSection();
        var outerColumn = new EmailColumn();
        outerColumn.Blocks.Add(Nest(4));
        outerSection.Columns.Add(outerColumn);

        var messages = Validator.Validate(WithSection(outerSection));

        messages.Should().Contain(m => m.Key == LayoutValidationKeys.MaxNesting
                                        && m.Severity == LayoutSeverity.Error);
    }

    [Fact]
    public void Messages_UseKeys_NotLocalizedText()
    {
        var messages = Validator.Validate(WithSection(new EmailSection()));
        messages.Should().OnlyContain(m => m.Key.StartsWith("layout."));
    }
}
