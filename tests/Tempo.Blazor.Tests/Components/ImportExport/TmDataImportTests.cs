using System.Text;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.ImportExport;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.ImportExport;

/// <summary>
/// bUnit tests for TmDataImport: the four-step flow (upload+preview → mapping →
/// dry-run validation → batched import) over the TmImportWizard shell, gating of
/// step transitions, error report download, partial import with skip-invalid,
/// progress, rollback, and reset.
/// </summary>
public class TmDataImportTests : LocalizationTestBase
{
    private const string CleanCsv = "Name,Email,Age\nAlice,a@x.com,30\nBob,b@x.com,40\nCara,c@x.com,50";
    private const string DirtyCsv = "Name,Email,Age\nAlice,a@x.com,30\nBob,broken,40\nCara,c@x.com,50";

    private static InMemoryDataImportTarget Target()
        => new(
            [
                new ImportTargetField("name", "Name", Required: true),
                new ImportTargetField("email", "Email", Required: true),
                new ImportTargetField("age", "Age")
            ],
            row => (row.Values.GetValueOrDefault("email") ?? string.Empty).Contains('@')
                ? []
                : [new DataImportRowError { RowNumber = row.RowNumber, FieldKey = "email", Message = "Invalid e-mail" }]);

    private IRenderedComponent<TmDataImport> Render(
        IDataImportTarget target,
        Action<Bunit.ComponentParameterCollectionBuilder<TmDataImport>>? configure = null)
        => Render<TmDataImport>(p =>
        {
            p.Add(x => x.Target, target);
            configure?.Invoke(p);
        });

    private static void Upload(IRenderedComponent<TmDataImport> cut, string csv, string fileName = "contacts.csv")
        => cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(csv, fileName));

    private static void ClickNext(IRenderedComponent<TmDataImport> cut)
        => cut.Find("[data-testid='wizard-next'] button").Click();

    private static void WalkToValidation(IRenderedComponent<TmDataImport> cut, string csv)
    {
        Upload(cut, csv);
        cut.WaitForAssertion(() => cut.Find("[data-testid='di-preview']"));
        ClickNext(cut);                         // → mapping (auto-mapped by header)
        cut.WaitForElement("[data-testid='di-step-mapping']");
        ClickNext(cut);                         // → validation (dry-run runs)
        cut.WaitForAssertion(() => cut.Find("[data-testid='di-validation-summary']"));
    }

    // ── Upload + preview ─────────────────────────────────────────────────────

    [Fact]
    public void RendersUploadStepFirst_WithFourSteps()
    {
        var cut = Render(Target());

        cut.Find("[data-testid='data-import']");
        cut.Find("[data-testid='di-step-upload']");
        cut.FindAll(".tm-stepper-item").Should().HaveCount(4);
    }

    [Fact]
    public void UploadCsv_ShowsPreviewWithCounts()
    {
        var cut = Render(Target());

        Upload(cut, CleanCsv);

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='di-parse-summary']").TextContent.Should().Contain("3");
            cut.FindAll("[data-testid='di-preview-row']").Should().HaveCount(3);
        });
    }

    [Fact]
    public void Next_WithoutFile_StaysOnUpload_WithMessage()
    {
        var cut = Render(Target());

        ClickNext(cut);

        cut.WaitForElement("[data-testid='di-gate-message']");
        cut.Find("[data-testid='di-step-upload']");
    }

    [Fact]
    public void UnsupportedExtension_ShowsGateMessage()
    {
        var cut = Render(Target());

        Upload(cut, "whatever", "data.pdf");

        cut.WaitForAssertion(() => cut.Find("[data-testid='di-gate-message']"));
        cut.FindAll("[data-testid='di-preview']").Should().BeEmpty();
    }

    [Fact]
    public void CustomParserByExtension_IsUsed()
    {
        var stub = new StubParser(new ImportParseResult(
            [new ImportColumn(0, "Name")],
            [new[] { "FromXlsx" }]));
        var cut = Render(Target(), p => p.Add(x => x.Parsers,
            new Dictionary<string, IImportFileParser> { [".xlsx"] = stub }));

        Upload(cut, "binary-not-used", "book.xlsx");

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='di-preview']").TextContent.Should().Contain("FromXlsx"));
        stub.Calls.Should().Be(1);
    }

    // ── Mapping ──────────────────────────────────────────────────────────────

    [Fact]
    public void Mapping_AutoMapsColumnsMatchingFieldLabels()
    {
        var cut = Render(Target());
        Upload(cut, CleanCsv);
        cut.WaitForAssertion(() => cut.Find("[data-testid='di-preview']"));

        ClickNext(cut);

        cut.WaitForElement("[data-testid='di-step-mapping']");
        var selects = cut.FindAll("[data-testid^='di-map-select-']");
        selects.Should().HaveCount(3);
        selects.Select(s => s.GetAttribute("value")).Should().Equal("name", "email", "age");
    }

    [Fact]
    public void Mapping_UnmappedRequiredField_BlocksNext()
    {
        var cut = Render(Target());
        Upload(cut, CleanCsv);
        cut.WaitForAssertion(() => cut.Find("[data-testid='di-preview']"));
        ClickNext(cut);

        cut.WaitForElement("[data-testid='di-map-select-1']").Change("");   // unmap Email (required)
        ClickNext(cut);

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='di-gate-message']").TextContent.Should().Contain("Email"));
        cut.Find("[data-testid='di-step-mapping']");
    }

    // ── Dry-run validation ───────────────────────────────────────────────────

    [Fact]
    public void DryRun_ListsRowErrors_WithRowNumbersAndFields()
    {
        var cut = Render(Target());

        WalkToValidation(cut, DirtyCsv);

        cut.Find("[data-testid='di-validation-summary']").TextContent.Should().Contain("1");
        var error = cut.Find("[data-testid='di-error-row']");
        error.TextContent.Should().Contain("Invalid e-mail");
        error.TextContent.Should().Contain("2");   // source row number
    }

    [Fact]
    public void DryRun_WithErrors_BlocksNext_UntilSkipInvalidIsChecked()
    {
        var cut = Render(Target());
        WalkToValidation(cut, DirtyCsv);

        ClickNext(cut);
        cut.Find("[data-testid='di-step-validation']");

        cut.Find("[data-testid='di-skip-invalid']").Change(true);
        ClickNext(cut);
        cut.WaitForElement("[data-testid='di-step-import']");
    }

    [Fact]
    public void DryRun_CleanFile_AdvancesWithoutSkip()
    {
        var cut = Render(Target());
        WalkToValidation(cut, CleanCsv);

        cut.Find("[data-testid='di-validation-summary']").TextContent.Should().NotContain("skip");
        ClickNext(cut);

        cut.WaitForElement("[data-testid='di-step-import']");
    }

    [Fact]
    public void ErrorReport_Download_SendsCsvWithRowsAndReasons()
    {
        var cut = Render(Target());
        WalkToValidation(cut, DirtyCsv);

        cut.Find("[data-testid='di-download-errors']").Click();

        cut.WaitForAssertion(() =>
        {
            var invocation = JSInterop.Invocations
                .LastOrDefault(i => i.Identifier == "tmDataTable.downloadFile");
            invocation.Arguments.Should().NotBeNull();
            var csv = Encoding.UTF8.GetString(Convert.FromBase64String((string)invocation.Arguments[2]!));
            csv.Should().Contain("Invalid e-mail");
            csv.Should().Contain("2");
        });
    }

    // ── Import, progress, partial import, rollback ───────────────────────────

    [Fact]
    public void Import_CleanFile_ImportsAllRows_AndFiresOnCompleted()
    {
        var target = Target();
        DataImportResult? completed = null;
        var cut = Render(target, p => p.Add(x => x.OnCompleted, (DataImportResult r) => completed = r));
        WalkToValidation(cut, CleanCsv);
        ClickNext(cut);

        cut.WaitForElement("[data-testid='di-start-import']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='di-import-result']");
            completed.Should().NotBeNull();
            completed!.ImportedCount.Should().Be(3);
            completed.SkippedCount.Should().Be(0);
        });
        target.ImportedRows.Should().HaveCount(3);
        cut.Find("[data-testid='di-progress']").GetAttribute("data-percent").Should().Be("100");
    }

    [Fact]
    public void PartialImport_SkipsInvalidRows_AndOffersFailedRowsDownload()
    {
        var target = Target();
        var cut = Render(target);
        WalkToValidation(cut, DirtyCsv);
        cut.Find("[data-testid='di-skip-invalid']").Change(true);
        ClickNext(cut);

        cut.WaitForElement("[data-testid='di-start-import']").Click();

        cut.WaitForAssertion(() =>
        {
            var result = cut.Find("[data-testid='di-import-result']").TextContent;
            result.Should().Contain("2");
            target.ImportedRows.Should().HaveCount(2);
        });

        // The skipped-rows CSV re-uses the target field keys so it can be re-imported (continuation).
        cut.Find("[data-testid='di-download-failed']").Click();
        cut.WaitForAssertion(() =>
        {
            var invocation = JSInterop.Invocations
                .LastOrDefault(i => i.Identifier == "tmDataTable.downloadFile");
            var csv = Encoding.UTF8.GetString(Convert.FromBase64String((string)invocation.Arguments[2]!));
            csv.Should().Contain("Name,Email,Age");
            csv.Should().Contain("broken");
        });
    }

    [Fact]
    public void Import_UsesBatchSize_ForBatchedTargetCalls()
    {
        var counting = new CountingTarget(Target());
        var cut = Render(counting, p => p.Add(x => x.BatchSize, 1));
        WalkToValidation(cut, CleanCsv);
        ClickNext(cut);

        cut.WaitForElement("[data-testid='di-start-import']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='di-import-result']");
            counting.ImportBatchCalls.Should().Be(3);
        });
    }

    [Fact]
    public void Rollback_AfterImport_RemovesTheSessionsRows()
    {
        var target = Target();
        var cut = Render(target);
        WalkToValidation(cut, CleanCsv);
        ClickNext(cut);
        cut.WaitForElement("[data-testid='di-start-import']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='di-import-result']"));

        cut.Find("[data-testid='di-rollback']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='di-rolled-back']");
            target.ImportedRows.Should().BeEmpty();
        });
    }

    [Fact]
    public void Cancel_ResetsBackToUpload()
    {
        var cut = Render(Target());
        WalkToValidation(cut, CleanCsv);

        cut.Find("[data-testid='wizard-cancel'] button").Click();

        cut.WaitForElement("[data-testid='di-step-upload']");
        cut.FindAll("[data-testid='di-preview']").Should().BeEmpty();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private sealed class StubParser(ImportParseResult result) : IImportFileParser
    {
        public int Calls { get; private set; }

        public Task<ImportParseResult> ParseAsync(Stream stream, ImportParseOptions options, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class CountingTarget(IDataImportTarget inner) : IDataImportTarget
    {
        public int ImportBatchCalls { get; private set; }

        public IReadOnlyList<ImportTargetField> Fields => inner.Fields;

        public Task<IReadOnlyList<DataImportRowError>> ValidateBatchAsync(
            IReadOnlyList<DataImportRow> rows, CancellationToken cancellationToken = default)
            => inner.ValidateBatchAsync(rows, cancellationToken);

        public Task<DataImportBatchResult> ImportBatchAsync(
            string sessionId, IReadOnlyList<DataImportRow> rows, CancellationToken cancellationToken = default)
        {
            ImportBatchCalls++;
            return inner.ImportBatchAsync(sessionId, rows, cancellationToken);
        }

        public Task RollbackAsync(string sessionId, CancellationToken cancellationToken = default)
            => inner.RollbackAsync(sessionId, cancellationToken);
    }
}
