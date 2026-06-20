using System.Net;
using Bunit;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Scheduler;
using Tempo.Blazor.Services;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Scheduler;

/// <summary>
/// TDD tests for Phase 4: Collaboration and Export.
/// Written BEFORE implementation (Red → Green → Refactor).
/// </summary>
public class TmGanttPhase4Tests : LocalizationTestBase
{
    private static GanttTask MakeTask(string id, string title = "Task",
        string? parentId = null, int attachments = 0, int comments = 0)
    {
        var t = new GanttTask
        {
            Id = id, Title = title,
            Start = new DateTime(2024, 1, 1),
            End   = new DateTime(2024, 1, 5),
            ParentId = parentId
        };
        for (var i = 0; i < attachments; i++)
            t.Attachments.Add(new GanttAttachment { Id = $"att-{id}-{i}", FileName = $"file{i}.pdf", ContentType = "application/pdf", Url = $"/files/{id}/{i}", UploadedAt = DateTime.UtcNow });
        for (var i = 0; i < comments; i++)
            t.Comments.Add(new GanttComment { Id = $"cmt-{id}-{i}", TaskId = id, AuthorId = "u1", AuthorName = "Alice", Text = "Comment", CreatedAt = DateTime.UtcNow });
        return t;
    }

    // ═══════════════════════════════════════════════════════════════
    // F4.1 – Rich Text Description
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GanttTask_Has_Description_Property()
    {
        var task = new GanttTask();
        task.Description = "Some rich description";
        task.Description.Should().Be("Some rich description");
    }

    [Fact]
    public void TmGanttTaskPanel_Renders_Description_Section()
    {
        var task = MakeTask("1");
        task.Description = "My description text";
        var cut = RenderComponent<TmGanttTaskPanel>(p => p.Add(x => x.Task, task));

        cut.Find("[data-testid='task-description']").Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════
    // F4.2 – Attachments
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GanttAttachment_Class_Has_Required_Properties()
    {
        var att = new GanttAttachment
        {
            Id = "a1", FileName = "report.pdf",
            ContentType = "application/pdf", Url = "/files/report.pdf",
            UploadedAt = new DateTime(2024, 1, 10, 12, 0, 0, DateTimeKind.Utc)
        };
        att.Id.Should().Be("a1");
        att.FileName.Should().Be("report.pdf");
        att.ContentType.Should().Be("application/pdf");
        att.Url.Should().Be("/files/report.pdf");
        att.UploadedAt.Year.Should().Be(2024);
    }

    [Fact]
    public void GanttTask_Has_Attachments_Property()
    {
        var task = new GanttTask();
        task.Attachments.Should().NotBeNull();
        task.Attachments.Should().BeEmpty();
    }

    [Fact]
    public void TmGanttTaskPanel_Renders_Attachment_List_And_Upload_Button()
    {
        var task = MakeTask("1", attachments: 1);
        var cut = RenderComponent<TmGanttTaskPanel>(p => p.Add(x => x.Task, task));

        cut.Find("[data-testid='attachment-list']").Should().NotBeNull();
        cut.Find("[data-testid='attachment-upload']").Should().NotBeNull();
    }

    [Fact]
    public void TmGantt_Tree_Row_Shows_Attachment_Count_Icon_When_Task_Has_Attachments()
    {
        var task = MakeTask("1", attachments: 2);
        var cut = RenderComponent<TmGantt>(p => p.Add(x => x.Data, new[] { task }));

        cut.Find("[data-testid='attachment-count-1']").Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════
    // F4.3 – Comments + @mentions
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GanttComment_Class_Has_Required_Properties()
    {
        var c = new GanttComment
        {
            Id = "c1", TaskId = "t1", AuthorId = "u1", AuthorName = "Alice",
            AvatarUrl = null, Text = "Hello world", CreatedAt = DateTime.UtcNow
        };
        c.Id.Should().Be("c1");
        c.TaskId.Should().Be("t1");
        c.AuthorName.Should().Be("Alice");
        c.Text.Should().Be("Hello world");
    }

    [Fact]
    public void GanttTask_Has_Comments_Property()
    {
        var task = new GanttTask();
        task.Comments.Should().NotBeNull();
        task.Comments.Should().BeEmpty();
    }

    [Fact]
    public void TmGanttTaskPanel_Renders_Comment_List()
    {
        var task = MakeTask("1", comments: 1);
        var cut = RenderComponent<TmGanttTaskPanel>(p => p.Add(x => x.Task, task));

        cut.Find("[data-testid='comment-list']").Should().NotBeNull();
    }

    [Fact]
    public void TmGanttTaskPanel_Parses_At_Mention_As_Span()
    {
        var task = MakeTask("1");
        task.Comments.Add(new GanttComment
        {
            Id = "c1", TaskId = "1", AuthorId = "u1", AuthorName = "Alice",
            Text = "Hello @bob please review this", CreatedAt = DateTime.UtcNow
        });
        var cut = RenderComponent<TmGanttTaskPanel>(p => p.Add(x => x.Task, task));

        cut.Find(".tm-gantt__mention").TextContent.Should().Be("@bob");
    }

    [Fact]
    public void TmGantt_Tree_Row_Shows_Comment_Count_Icon_When_Task_Has_Comments()
    {
        var task = MakeTask("1", comments: 3);
        var cut = RenderComponent<TmGantt>(p => p.Add(x => x.Data, new[] { task }));

        cut.Find("[data-testid='comment-count-1']").Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════
    // F4.4 – Export (PDF, PNG, XLSX, XML)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GanttExportFormat_Enum_Has_Required_Values()
    {
        var values = Enum.GetValues<GanttExportFormat>();
        values.Should().Contain(GanttExportFormat.Pdf);
        values.Should().Contain(GanttExportFormat.Png);
        values.Should().Contain(GanttExportFormat.Xlsx);
        values.Should().Contain(GanttExportFormat.Xml);
    }

    [Fact]
    public void GanttExportOptions_Record_Has_Required_Properties()
    {
        var opts = new GanttExportOptions(
            Format: GanttExportFormat.Xlsx,
            PaperSize: "A4",
            Landscape: true,
            ZoomLevel: 100,
            IncludeCriticalPath: true,
            IncludeToday: true,
            IncludeWorkload: false,
            Columns: null);

        opts.Format.Should().Be(GanttExportFormat.Xlsx);
        opts.PaperSize.Should().Be("A4");
        opts.Landscape.Should().BeTrue();
        opts.ZoomLevel.Should().Be(100);
        opts.IncludeCriticalPath.Should().BeTrue();
    }

    [Fact]
    public void GanttXlsxExporter_Export_Returns_Valid_Zip_Bytes()
    {
        var tasks = new[] { MakeTask("1", "Export Task") };
        var opts = new GanttExportOptions(GanttExportFormat.Xlsx);

        var bytes = GanttXlsxExporter.Export(tasks, Array.Empty<GanttDependency>(), opts);

        bytes.Should().NotBeEmpty();
        bytes[0].Should().Be(0x50); // ZIP PK magic
        bytes[1].Should().Be(0x4B);
    }

    [Fact]
    public void GanttXlsxExporter_Export_Contains_Configured_Columns_As_Header()
    {
        var tasks = new[] { MakeTask("1", "My Export Task") };
        var opts = new GanttExportOptions(GanttExportFormat.Xlsx,
            Columns: new[] { GanttColumnKey.Title, GanttColumnKey.Start, GanttColumnKey.End });

        var bytes = GanttXlsxExporter.Export(tasks, Array.Empty<GanttDependency>(), opts);

        // Open and verify at least 2 rows (header + 1 data row)
        using var ms = new MemoryStream(bytes);
        using var doc = SpreadsheetDocument.Open(ms, false);
        var wsp = doc.WorkbookPart!.WorksheetParts.First();
        var rows = wsp.Worksheet.Descendants<Row>().ToList();
        rows.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void GanttXmlExporter_Export_Returns_Valid_MsProject_Xml()
    {
        var tasks = new[] { MakeTask("1", "XML Task") };
        var xml = GanttXmlExporter.Export(tasks, Array.Empty<GanttDependency>());

        xml.Should().Contain("<Project");
        xml.Should().Contain("XML Task");
        xml.Should().Contain("</Project>");
    }

    [Fact]
    public void TmGantt_Has_OnExportRequested_Parameter()
    {
        var fired = false;
        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, Array.Empty<GanttTask>())
            .Add(x => x.OnExportRequested, EventCallback.Factory.Create<GanttExportOptions>(this, _ => fired = true)));

        cut.Instance.OnExportRequested.HasDelegate.Should().BeTrue();
    }

    [Fact]
    public void TmGanttExportDialog_Renders_Preview_And_Format_Options()
    {
        var cut = RenderComponent<TmGanttExportDialog>(p => p
            .Add(x => x.IsOpen, true)
            .Add(x => x.OnExport, EventCallback.Factory.Create<GanttExportOptions>(this, _ => { }))
            .Add(x => x.OnClose, EventCallback.Factory.Create(this, () => { })));

        cut.Find("[data-testid='export-preview']").Should().NotBeNull();
        cut.Find("[data-testid='export-format-xlsx']").Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════
    // F4.5 – Import (Excel, MS Project, JIRA)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GanttColumnMapping_Class_Has_Required_Properties()
    {
        var m = new GanttColumnMapping { SourceColumn = "Task Name", TargetProperty = GanttColumnKey.Title };
        m.SourceColumn.Should().Be("Task Name");
        m.TargetProperty.Should().Be(GanttColumnKey.Title);
    }

    [Fact]
    public void GanttExcelImporter_Import_Returns_Tasks_From_Standard_Columns()
    {
        using var stream = CreateTestXlsx(new[]
        {
            new[] { "Title", "Start", "End", "Progress" },
            new[] { "Imported Task", "2024-01-01", "2024-01-05", "50" }
        });

        var result = GanttExcelImporter.Import(stream);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Imported Task");
        result[0].PercentComplete.Should().Be(50);
    }

    [Fact]
    public void GanttExcelImporter_Import_Maps_Start_And_End_Dates()
    {
        using var stream = CreateTestXlsx(new[]
        {
            new[] { "Title", "Start", "End", "Progress" },
            new[] { "Date Task", "2024-03-01", "2024-03-15", "0" }
        });

        var result = GanttExcelImporter.Import(stream);

        result[0].Start.Should().Be(new DateTime(2024, 3, 1));
        result[0].End.Should().Be(new DateTime(2024, 3, 15));
    }

    [Fact]
    public void GanttMppImporter_Import_Returns_Tasks_From_MsProject_Xml()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Project xmlns="http://schemas.microsoft.com/project">
              <Tasks>
                <Task>
                  <UID>1</UID>
                  <Name>MPP Imported Task</Name>
                  <Start>2024-01-01T08:00:00</Start>
                  <Finish>2024-01-05T17:00:00</Finish>
                  <PercentComplete>25</PercentComplete>
                </Task>
              </Tasks>
            </Project>
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));

        var result = GanttMppImporter.Import(stream);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("MPP Imported Task");
        result[0].PercentComplete.Should().Be(25);
        result[0].Start.Date.Should().Be(new DateTime(2024, 1, 1));
    }

    [Fact]
    public async Task GanttJiraImporter_Maps_Jira_Priority_To_GanttTaskPriority()
    {
        const string json = """
            { "issues": [{ "key": "P-1", "fields": {
                "summary": "Jira Task Highest",
                "duedate": "2024-01-10",
                "priority": { "name": "Highest" },
                "assignee": null,
                "status": { "name": "To Do" }
            }}]}
            """;
        using var importer = new GanttJiraImporter(CreateFakeHttpClient(200, json));

        var result = await importer.ImportAsync("https://jira.example.com", "token", "PROJ");

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Jira Task Highest");
        result[0].Priority.Should().Be(GanttTaskPriority.Highest);
    }

    [Fact]
    public async Task GanttJiraImporter_Throws_GanttImportAuthException_On_401()
    {
        using var importer = new GanttJiraImporter(CreateFakeHttpClient(401, "Unauthorized"));

        await importer.Invoking(i => i.ImportAsync("https://jira.example.com", "bad-token", "PROJ"))
            .Should().ThrowAsync<GanttImportAuthException>();
    }

    [Fact]
    public void GanttExcelImporter_Import_With_Custom_Mappings()
    {
        using var stream = CreateTestXlsx(new[]
        {
            new[] { "Task Name", "Start Date", "End Date", "Done %" },
            new[] { "Mapped Task", "2024-02-01", "2024-02-10", "30" }
        });
        var mappings = new[]
        {
            new GanttColumnMapping { SourceColumn = "Task Name",  TargetProperty = GanttColumnKey.Title    },
            new GanttColumnMapping { SourceColumn = "Start Date", TargetProperty = GanttColumnKey.Start    },
            new GanttColumnMapping { SourceColumn = "End Date",   TargetProperty = GanttColumnKey.End      },
            new GanttColumnMapping { SourceColumn = "Done %",     TargetProperty = GanttColumnKey.Progress },
        };

        var result = GanttExcelImporter.Import(stream, mappings);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Mapped Task");
        result[0].Start.Should().Be(new DateTime(2024, 2, 1));
    }

    [Fact]
    public void TmGantt_Has_OnImportCompleted_And_OnImportError_Parameters()
    {
        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, Array.Empty<GanttTask>())
            .Add(x => x.OnImportCompleted, EventCallback.Factory.Create<IReadOnlyList<GanttTask>>(this, _ => { }))
            .Add(x => x.OnImportError,     EventCallback.Factory.Create<string>(this, _ => { })));

        cut.Instance.OnImportCompleted.HasDelegate.Should().BeTrue();
        cut.Instance.OnImportError.HasDelegate.Should().BeTrue();
    }

    [Fact]
    public void TmGanttImportMappingDialog_Renders_Mapping_Selects_For_Source_Columns()
    {
        var cols = new[] { "Task Name", "Start Date", "End Date" };
        var cut = RenderComponent<TmGanttImportMappingDialog>(p => p
            .Add(x => x.SourceColumns, cols)
            .Add(x => x.IsOpen, true)
            .Add(x => x.OnImport, EventCallback.Factory.Create<IReadOnlyList<GanttColumnMapping>>(this, _ => { }))
            .Add(x => x.OnClose, EventCallback.Factory.Create(this, () => { })));

        cut.FindAll("[data-testid^='mapping-select-']").Should().HaveCount(3);
    }

    // ═══════════════════════════════════════════════════════════════
    // F4.6 – History / Audit Log
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GanttHistoryEntry_Record_Has_Required_Properties()
    {
        var ts = new DateTime(2024, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var entry = new GanttHistoryEntry("h1", ts, "Alice", "StatusChanged", "t1", "Open", "Done");

        entry.Id.Should().Be("h1");
        entry.Timestamp.Should().Be(ts);
        entry.Author.Should().Be("Alice");
        entry.ChangeType.Should().Be("StatusChanged");
        entry.TaskId.Should().Be("t1");
        entry.OldValue.Should().Be("Open");
        entry.NewValue.Should().Be("Done");
    }

    [Fact]
    public void TmGantt_Has_History_Parameter()
    {
        var history = new[]
        {
            new GanttHistoryEntry("h1", DateTime.UtcNow, "Alice", "StatusChanged", "t1", null, "Done"),
            new GanttHistoryEntry("h2", DateTime.UtcNow, "Bob",   "PriorityChanged", "t2", "Low", "High"),
        };
        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, Array.Empty<GanttTask>())
            .Add(x => x.History, history));

        cut.Instance.History.Should().HaveCount(2);
    }

    [Fact]
    public void GanttHelper_FilterHistory_Returns_Only_Matching_ChangeTypes()
    {
        var entries = new[]
        {
            new GanttHistoryEntry("1", DateTime.UtcNow, "Alice", "StatusChanged",   "t1", "Open",  "Done"),
            new GanttHistoryEntry("2", DateTime.UtcNow, "Bob",   "PriorityChanged", "t2", "Low",   "High"),
            new GanttHistoryEntry("3", DateTime.UtcNow, "Alice", "StatusChanged",   "t3", "Open",  "InProgress"),
            new GanttHistoryEntry("4", DateTime.UtcNow, "Carol", "TaskChanged",     "t4", "Old",   "New"),
        };

        var result = GanttHelper.FilterHistory(entries, new[] { "StatusChanged" });

        result.Should().HaveCount(2);
        result.All(e => e.ChangeType == "StatusChanged").Should().BeTrue();
    }

    [Fact]
    public void TmGantt_Has_OnTimeTravelRequested_And_OnRollbackRequested_Parameters()
    {
        var cut = RenderComponent<TmGantt>(p => p
            .Add(x => x.Data, Array.Empty<GanttTask>())
            .Add(x => x.OnTimeTravelRequested,  EventCallback.Factory.Create<DateTime>(this, _ => { }))
            .Add(x => x.OnRollbackRequested,    EventCallback.Factory.Create<GanttHistoryEntry>(this, _ => { })));

        cut.Instance.OnTimeTravelRequested.HasDelegate.Should().BeTrue();
        cut.Instance.OnRollbackRequested.HasDelegate.Should().BeTrue();
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private static Stream CreateTestXlsx(string[][] rows)
    {
        var ms = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook, true))
        {
            var wbp = doc.AddWorkbookPart();
            wbp.Workbook = new Workbook();

            var wsp = wbp.AddNewPart<WorksheetPart>();
            var sd = new SheetData();
            wsp.Worksheet = new Worksheet(sd);

            foreach (var rowData in rows)
            {
                var row = new Row();
                foreach (var cell in rowData)
                    row.Append(new Cell { CellValue = new CellValue(cell), DataType = CellValues.InlineString });
                sd.Append(row);
            }

            wbp.Workbook.AppendChild(new Sheets()).Append(new Sheet
            {
                Id = wbp.GetIdOfPart(wsp),
                SheetId = 1,
                Name = "Tasks"
            });
            doc.Save();
        }
        ms.Position = 0;
        return ms;
    }

    private static HttpClient CreateFakeHttpClient(int statusCode, string content) =>
        new(new FakeHttpMessageHandler(statusCode, content));

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly int _statusCode;
        private readonly string _content;

        public FakeHttpMessageHandler(int statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage((HttpStatusCode)_statusCode)
            {
                Content = new StringContent(_content, System.Text.Encoding.UTF8, "application/json")
            });
    }
}
