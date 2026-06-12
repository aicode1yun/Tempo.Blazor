using System.Globalization;
using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Layout;

/// <summary>Severity of a layout validation message.</summary>
public enum LayoutSeverity
{
    /// <summary>A non-fatal issue worth surfacing.</summary>
    Warning,

    /// <summary>A structural problem that would produce broken or invalid output.</summary>
    Error,
}

/// <summary>Localization keys for layout validation messages (never raw text).</summary>
public static class LayoutValidationKeys
{
    /// <summary>Explicit column widths in a section do not sum to 100%.</summary>
    public const string ColumnWidths = "layout.column_widths";

    /// <summary>A section has no columns.</summary>
    public const string EmptySection = "layout.empty_section";

    /// <summary>Container nesting exceeds the supported depth.</summary>
    public const string MaxNesting = "layout.max_nesting";
}

/// <summary>A single layout validation finding.</summary>
/// <param name="Severity">How serious the finding is.</param>
/// <param name="Key">The localization key describing the finding.</param>
/// <param name="Path">A locator for the offending node (section/block identifier).</param>
public sealed record LayoutValidationMessage(LayoutSeverity Severity, string Key, string Path);

/// <summary>
/// Validates the structural layout of a document: column-width totals, empty sections and excessive
/// container nesting. Findings carry localization keys, never localized text.
/// </summary>
public sealed class LayoutValidator
{
    /// <summary>The maximum supported depth of nested wrapper/group/hero containers.</summary>
    public const int MaxNestingDepth = 3;

    private const decimal WidthTolerance = 0.5m;

    /// <summary>Validates the document and returns all layout findings.</summary>
    public IReadOnlyList<LayoutValidationMessage> Validate(EmailTemplateDocument document)
    {
        var messages = new List<LayoutValidationMessage>();

        foreach (var section in DocumentTree.AllSections(document))
        {
            if (section.Columns.Count == 0)
            {
                messages.Add(new(LayoutSeverity.Warning, LayoutValidationKeys.EmptySection, section.Id.ToString()));
                continue;
            }

            ValidateColumnWidths(section, messages);
        }

        foreach (var section in document.Sections)
            foreach (var column in section.Columns)
                foreach (var block in column.Blocks)
                    CheckNesting(block, 1, messages);

        return messages;
    }

    private static void ValidateColumnWidths(EmailSection section, List<LayoutValidationMessage> messages)
    {
        // Only validate when every column declares an explicit width; null widths mean MJML auto-split.
        if (section.Columns.Any(c => c.Width is null)) return;

        decimal total = 0;
        foreach (var column in section.Columns)
        {
            if (!TryParsePercent(column.Width!, out var value)) return; // non-% widths (px) are not summed
            total += value;
        }

        if (Math.Abs(total - 100m) > WidthTolerance)
            messages.Add(new(LayoutSeverity.Error, LayoutValidationKeys.ColumnWidths, section.Id.ToString()));
    }

    private static void CheckNesting(EmailBlockBase block, int depth, List<LayoutValidationMessage> messages)
    {
        var isContainer = block is EmailWrapperBlock or EmailGroupBlock or EmailHeroBlock;
        if (isContainer && depth > MaxNestingDepth)
            messages.Add(new(LayoutSeverity.Error, LayoutValidationKeys.MaxNesting, block.Id.ToString()));

        switch (block)
        {
            case EmailHeroBlock hero:
                foreach (var b in hero.Blocks) CheckNesting(b, depth + 1, messages);
                break;
            case EmailGroupBlock group:
                foreach (var column in group.Columns)
                    foreach (var b in column.Blocks) CheckNesting(b, depth + 1, messages);
                break;
            case EmailWrapperBlock wrapper:
                foreach (var section in wrapper.Sections)
                    foreach (var column in section.Columns)
                        foreach (var b in column.Blocks) CheckNesting(b, depth + 1, messages);
                break;
        }
    }

    private static bool TryParsePercent(string width, out decimal value)
    {
        value = 0;
        if (!width.EndsWith('%')) return false;
        return decimal.TryParse(width.TrimEnd('%'), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
