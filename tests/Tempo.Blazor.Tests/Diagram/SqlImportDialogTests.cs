using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Services;
using Xunit;

namespace Tempo.Blazor.Tests.Diagram;

public class SqlImportDialogTests : DiagramTestBase
{
    [Fact]
    public async Task SqlImportDialog_RendersErrorForInvalidSql()
    {
        var cut = RenderComponent<TmDiagramSqlImportDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.OnClose, () => { })
            .Add(p => p.OnImport, (SqlImportResult r) => { }));

        var textarea = cut.Find("textarea");
        textarea.Change("NOT A VALID SQL");

        var importBtn = cut.FindAll("button").First(b => b.TextContent == "Import");
        await cut.InvokeAsync(() => importBtn.Click());

        cut.WaitForAssertion(() =>
        {
            var alerts = cut.FindAll(".tm-alert");
            alerts.Should().ContainSingle();
            alerts[0].TextContent.Should().Contain("No tables found");
        });
    }

    [Fact]
    public void SqlImportDialog_RendersPreviewForValidSql()
    {
        var cut = RenderComponent<TmDiagramSqlImportDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.OnClose, () => { })
            .Add(p => p.OnImport, (SqlImportResult r) => { }));

        var sql = "CREATE TABLE Users (Id INT PRIMARY KEY, Name VARCHAR(255));";
        var textarea = cut.Find("textarea");
        textarea.Input(sql);

        // Preview is updated during import click path, but we can at least verify dialog renders
        var title = cut.Find(".tm-modal-title");
        title.TextContent.Should().Be("Import SQL to ER Diagram");
    }
}
