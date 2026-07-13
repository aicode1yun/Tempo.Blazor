using System.Text;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.ImportExport;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.ImportExport;

/// <summary>bUnit tests for the K10 parse + column-mapping wiring on <see cref="TmImportWizard"/>.</summary>
public class TmImportWizardMappingTests : LocalizationTestBase
{
    private static readonly List<ImportTargetField> TargetFields =
    [
        new("name", "Name"),
        new("email", "Email"),
    ];

    private IRenderedComponent<TmImportWizard> RenderWizard(
        Action<ImportMappingResult> onMapped, IImportFileParser? parser = null)
    {
        parser ??= new CsvImportFileParser();
        return RenderComponent<TmImportWizard>(p => p
            .Add(c => c.Parser, parser)
            .Add(c => c.TargetFields, TargetFields)
            .Add(c => c.OnMapped, EventCallback.Factory.Create<ImportMappingResult>(this, onMapped)));
    }

    private static async Task ParseAsync(IRenderedComponent<TmImportWizard> cut, string csv)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        await cut.InvokeAsync(() => cut.Instance.ParseAsync(stream));
    }

    [Fact]
    public async Task Parse_Shows_Detected_Columns()
    {
        var cut = RenderWizard(_ => { });

        await ParseAsync(cut, "Name,Email\nAlice,alice@x.com");

        cut.FindAll("[data-testid='import-map-select-0']").Count.Should().Be(1);
        cut.FindAll("[data-testid='import-map-select-1']").Count.Should().Be(1);
        cut.Markup.Should().Contain("Name");
        cut.Markup.Should().Contain("Email");
    }

    [Fact]
    public async Task Auto_Mapped_Apply_Produces_Mapped_Rows()
    {
        ImportMappingResult? captured = null;
        var cut = RenderWizard(r => captured = r);

        await ParseAsync(cut, "Name,Email\nAlice,alice@x.com\nBob,bob@x.com");
        cut.Find("[data-testid='import-map-apply'] button").Click();

        captured.Should().NotBeNull();
        captured!.Rows.Should().HaveCount(2);
        captured.Rows[0]["name"].Should().Be("Alice");
        captured.Rows[0]["email"].Should().Be("alice@x.com");
        captured.Rows[1]["name"].Should().Be("Bob");
        captured.Mappings.Should().Contain(m => m.ColumnIndex == 0 && m.TargetFieldKey == "name");
        captured.Mappings.Should().Contain(m => m.ColumnIndex == 1 && m.TargetFieldKey == "email");
    }

    [Fact]
    public async Task Changing_A_Mapping_Select_Is_Reflected_In_Output()
    {
        ImportMappingResult? captured = null;
        var cut = RenderWizard(r => captured = r);

        // Header names don't match any target field, so nothing is auto-mapped.
        await ParseAsync(cut, "ColA,ColB\nAlice,alice@x.com");
        cut.Find("[data-testid='import-map-select-0']").Change("email");
        cut.Find("[data-testid='import-map-apply'] button").Click();

        captured.Should().NotBeNull();
        captured!.Rows[0]["email"].Should().Be("Alice");
        captured.Rows[0].Should().NotContainKey("name");
    }

    [Fact]
    public async Task Ignored_Column_Is_Excluded_From_Output()
    {
        ImportMappingResult? captured = null;
        var cut = RenderWizard(r => captured = r);

        await ParseAsync(cut, "Name,Email\nAlice,alice@x.com");
        cut.Find("[data-testid='import-map-select-1']").Change(string.Empty); // ignore Email
        cut.Find("[data-testid='import-map-apply'] button").Click();

        captured.Should().NotBeNull();
        captured!.Rows[0].Should().ContainKey("name");
        captured.Rows[0].Should().NotContainKey("email");
    }

    [Fact]
    public void No_Parser_Renders_No_Mapping_Panel()
    {
        var cut = RenderComponent<TmImportWizard>(p => p
            .AddChildContent<TmImportWizardStep>(sp => sp
                .Add(s => s.Title, "Step 1")
                .AddChildContent("<p>Content</p>")));

        cut.FindAll("[data-testid='import-map-apply']").Count.Should().Be(0);
    }
}
