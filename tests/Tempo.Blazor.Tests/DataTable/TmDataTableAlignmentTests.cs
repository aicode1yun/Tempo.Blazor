using System.Text.RegularExpressions;
using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DataTable;
using Tempo.Blazor.Tests.Localization;
using Tempo.Blazor.Tests.Theme;

namespace Tempo.Blazor.Tests.DataTable;

/// <summary>
/// Guards <c>TmDataTableColumn.Align</c> — that it reaches the header as well as the cells.
/// <para>
/// The markup half was never the problem: the table has always put <c>tm-text-right</c> on both the
/// <c>th</c> and the <c>td</c>. The stylesheet threw it away. <c>.tm-data-table th, .tm-data-table td</c>
/// declares <c>text-align: left</c> at specificity 0-1-1, while the helper <c>.tm-text-right</c> is
/// 0-1-0, so the base rule won regardless of source order and <c>Align</c> was inert inside a data table.
/// Applications did not notice for cells because a right-aligned cell usually holds its own flex
/// container; a header is bare text, so it stayed visibly left while its column read right.
/// </para>
/// <para>
/// A class assertion cannot catch that — the class was always there — so the second half of this file
/// resolves the cascade out of the shipped stylesheet and asserts the value that actually applies.
/// </para>
/// </summary>
public class TmDataTableAlignmentTests : LocalizationTestBase
{
    private sealed record AlignPerson(string Name, decimal Amount);

    private IRenderedComponent<TmDataTable<AlignPerson>> RenderWithAlignedColumn(ColumnAlign align)
        => Render<TmDataTable<AlignPerson>>(p =>
        {
            p.Add(c => c.Items, new List<AlignPerson> { new("Alice", 12m) });
            p.AddChildContent(b =>
            {
                b.OpenComponent<TmDataTableColumn<AlignPerson>>(0);
                b.AddAttribute(1, "Title", "Amount");
                b.AddAttribute(2, "PropertyName", "Amount");
                b.AddAttribute(3, "Align", align);
                b.AddAttribute(4, "Field", (Func<AlignPerson, object?>)(x => x.Amount));
                b.CloseComponent();
            });
        });

    // ── Markup: the header carries the same alignment class as its cells ──

    [Fact]
    public void Align_Right_PutsTheSameAlignmentClassOnTheHeaderAndTheCells()
    {
        var cut = RenderWithAlignedColumn(ColumnAlign.Right);

        cut.Find("thead th").ClassList.Should().Contain("tm-text-right");
        cut.Find("tbody td").ClassList.Should().Contain("tm-text-right");
    }

    [Fact]
    public void Align_Center_PutsTheSameAlignmentClassOnTheHeaderAndTheCells()
    {
        var cut = RenderWithAlignedColumn(ColumnAlign.Center);

        cut.Find("thead th").ClassList.Should().Contain("tm-text-center");
        cut.Find("tbody td").ClassList.Should().Contain("tm-text-center");
    }

    [Fact]
    public void Align_Left_AddsNoAlignmentClass()
    {
        var cut = RenderWithAlignedColumn(ColumnAlign.Left);

        cut.Find("thead th").ClassList.Should().NotContain("tm-text-right").And.NotContain("tm-text-center");
        cut.Find("tbody td").ClassList.Should().NotContain("tm-text-right").And.NotContain("tm-text-center");
    }

    // ── Cascade: the class the header carries is the one that wins ──

    [Theory]
    [InlineData("th", "tm-text-right", "right")]
    [InlineData("td", "tm-text-right", "right")]
    [InlineData("th", "tm-text-center", "center")]
    [InlineData("td", "tm-text-center", "center")]
    public void AlignmentClass_BeatsTheTablesOwnLeftAlignedBaseRule(string tag, string alignClass, string expected)
        => DataTableCascade.WinningTextAlign(tag, alignClass).Should().Be(
            expected,
            "an Align other than Left has to survive `.tm-data-table {tag} {{ text-align: left }}`");

    [Theory]
    [InlineData("th")]
    [InlineData("td")]
    public void WithoutAnAlignmentClass_TheTableStaysLeftAligned(string tag)
        => DataTableCascade.WinningTextAlign(tag, alignClass: null).Should().Be("left");

    /// <summary>
    /// A deliberately small slice of the CSS cascade: enough to answer "what <c>text-align</c> applies to
    /// <c>&lt;th class='…'&gt;</c> inside <c>&lt;table class='tm-data-table'&gt;</c>", read out of the file
    /// that ships, so a specificity regression is caught instead of a missing class.
    /// <para>
    /// Only descendant combinators of type/class compounds are modelled. Any selector that reaches for
    /// something else — a child/sibling combinator, an attribute, a pseudo-class or pseudo-element, or a
    /// class this element does not have — is treated as not matching, which is correct for the plain,
    /// non-card table element modelled here.
    /// </para>
    /// </summary>
    private static class DataTableCascade
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private static readonly Regex RuleBlock =
            new(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.Compiled, Timeout);

        private static readonly Regex TextAlign =
            new(@"(?:^|;)\s*text-align\s*:\s*(?<value>[^;]+)", RegexOptions.Compiled, Timeout);

        /// <summary>The element under test and its ancestors, outermost first.</summary>
        private static IReadOnlyList<(string Tag, HashSet<string> Classes)> ElementChain(string tag, string? alignClass)
            =>
            [
                ("table", ["tm-data-table"]),
                ("thead", []),
                ("tr", []),
                (tag, alignClass is null ? [] : [alignClass]),
            ];

        public static string WinningTextAlign(string tag, string? alignClass)
        {
            var chain = ElementChain(tag, alignClass);
            var css = ThemeCss.ComponentCss("_data-table.css");

            string? winner = null;
            var bestSpecificity = (-1, -1, -1);

            var order = 0;
            foreach (Match rule in RuleBlock.Matches(css))
            {
                order++;
                var declared = TextAlign.Match(rule.Groups["body"].Value);
                if (!declared.Success) continue;

                var value = ThemeCss.Normalise(declared.Groups["value"].Value);

                foreach (var part in rule.Groups["selector"].Value.Split(','))
                {
                    var specificity = SpecificityIfMatches(ThemeCss.Normalise(part), chain);
                    if (specificity is null) continue;

                    // Later source order wins a tie — the loop walks the file top to bottom.
                    if (specificity.Value.CompareTo(bestSpecificity) >= 0)
                    {
                        bestSpecificity = specificity.Value;
                        winner = value;
                    }
                }
            }

            winner.Should().NotBeNull($"_data-table.css must declare a text-align that reaches <{tag}>");
            return winner!;
        }

        /// <summary>Specificity of a descendant selector when it matches the chain, otherwise null.</summary>
        private static (int Id, int Class, int Type)? SpecificityIfMatches(
            string selector,
            IReadOnlyList<(string Tag, HashSet<string> Classes)> chain)
        {
            if (selector.Length == 0) return null;

            // Anything beyond descendant combinators of types and classes is out of the model, and every
            // such selector in this file reaches for a card-mode wrapper, an attribute or a pseudo-element
            // — none of which this element has.
            if (selector.IndexOfAny(['>', '+', '~', '[', ':', '#', '*', '@']) >= 0) return null;

            var compounds = selector.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // The rightmost compound must match the element itself; the rest must match ancestors in order.
            if (!CompoundMatches(compounds[^1], chain[^1])) return null;

            var ancestorIndex = chain.Count - 2;
            for (var i = compounds.Length - 2; i >= 0; i--)
            {
                while (ancestorIndex >= 0 && !CompoundMatches(compounds[i], chain[ancestorIndex]))
                    ancestorIndex--;

                if (ancestorIndex < 0) return null;
                ancestorIndex--;
            }

            var classCount = compounds.Sum(c => c.Count(ch => ch == '.'));
            var typeCount = compounds.Count(c => !c.StartsWith('.'));
            return (0, classCount, typeCount);
        }

        private static bool CompoundMatches(string compound, (string Tag, HashSet<string> Classes) element)
        {
            var parts = compound.Split('.');
            if (parts[0].Length > 0 && !parts[0].Equals(element.Tag, StringComparison.OrdinalIgnoreCase))
                return false;

            return parts.Skip(1).All(element.Classes.Contains);
        }
    }
}
