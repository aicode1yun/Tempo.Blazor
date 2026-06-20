using FluentAssertions;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetHyperlinkTests
{
    [Fact]
    public void GetUri_Web_ReturnsTarget()
    {
        var link = new SpreadsheetHyperlink { Kind = SpreadsheetHyperlinkKind.Web, Target = "https://example.com" };
        link.GetUri().Should().Be("https://example.com");
    }

    [Fact]
    public void GetUri_Email_BuildsMailto()
    {
        var link = new SpreadsheetHyperlink
        {
            Kind = SpreadsheetHyperlinkKind.Email,
            Target = "test@example.com",
            EmailSubject = "Hello World"
        };
        link.GetUri().Should().Be("mailto:test@example.com?subject=Hello%20World");
    }

    [Fact]
    public void GetUri_Email_WithoutSubject_BuildsSimpleMailto()
    {
        var link = new SpreadsheetHyperlink { Kind = SpreadsheetHyperlinkKind.Email, Target = "test@example.com" };
        link.GetUri().Should().Be("mailto:test@example.com");
    }

    [Fact]
    public void GetUri_InternalRef_ReturnsTarget()
    {
        var link = new SpreadsheetHyperlink { Kind = SpreadsheetHyperlinkKind.InternalRef, Target = "Sheet2!B5" };
        link.GetUri().Should().Be("Sheet2!B5");
    }

    [Fact]
    public void GetUri_NamedRange_ReturnsTarget()
    {
        var link = new SpreadsheetHyperlink { Kind = SpreadsheetHyperlinkKind.NamedRange, Target = "Sales" };
        link.GetUri().Should().Be("Sales");
    }

    [Fact]
    public void Clone_CreatesDeepCopy()
    {
        var link = new SpreadsheetHyperlink
        {
            Kind = SpreadsheetHyperlinkKind.Web,
            Target = "https://example.com",
            Display = "Example",
            Tooltip = "Click me",
            EmailSubject = "Subject"
        };

        var clone = link.Clone();

        clone.Kind.Should().Be(link.Kind);
        clone.Target.Should().Be(link.Target);
        clone.Display.Should().Be(link.Display);
        clone.Tooltip.Should().Be(link.Tooltip);
        clone.EmailSubject.Should().Be(link.EmailSubject);
        clone.Should().NotBeSameAs(link);
    }

    [Fact]
    public void Cell_Clone_CopiesHyperlink()
    {
        var cell = new SpreadsheetCell
        {
            Value = "Click",
            Hyperlink = new SpreadsheetHyperlink
            {
                Kind = SpreadsheetHyperlinkKind.Web,
                Target = "https://example.com",
                Display = "Example"
            }
        };

        var clone = cell.Clone();

        clone.Hyperlink.Should().NotBeNull();
        clone.Hyperlink!.Target.Should().Be("https://example.com");
        clone.Hyperlink.Display.Should().Be("Example");
        clone.Hyperlink.Should().NotBeSameAs(cell.Hyperlink);
    }
}
