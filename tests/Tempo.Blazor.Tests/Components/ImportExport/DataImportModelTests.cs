using FluentAssertions;
using global::FluentValidation;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.FluentValidation.Import;
using Tempo.Blazor.Interfaces;
using Xunit;

namespace Tempo.Blazor.Tests.Components.ImportExport;

/// <summary>
/// Model tests for the data-import stack: InMemoryDataImportTarget (validate batch,
/// continue-on-error import, per-session rollback, clone-on-read) and the
/// FluentValidation row-validator adapter with row numbers and field keys.
/// </summary>
public class DataImportModelTests
{
    private static DataImportRow Row(int number, string name = "Alice", string email = "a@x.com", string age = "30")
        => new()
        {
            RowNumber = number,
            Values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = name,
                ["email"] = email,
                ["age"] = age
            }
        };

    private static InMemoryDataImportTarget Target()
        => new(
            [
                new ImportTargetField("name", "Name", Required: true),
                new ImportTargetField("email", "Email", Required: true),
                new ImportTargetField("age", "Age")
            ],
            row =>
            {
                var errors = new List<DataImportRowError>();
                if (string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("name")))
                {
                    errors.Add(new DataImportRowError { RowNumber = row.RowNumber, FieldKey = "name", Message = "Name is required" });
                }

                if (!(row.Values.GetValueOrDefault("email") ?? string.Empty).Contains('@'))
                {
                    errors.Add(new DataImportRowError { RowNumber = row.RowNumber, FieldKey = "email", Message = "Invalid e-mail" });
                }

                return errors;
            });

    // ── InMemoryDataImportTarget ─────────────────────────────────────────────

    [Fact]
    public async Task ValidateBatch_ReportsOnlyInvalidRows()
    {
        var target = Target();

        var errors = await target.ValidateBatchAsync([Row(1), Row(2, email: "broken"), Row(3, name: "")]);

        errors.Should().HaveCount(2);
        errors.Should().Contain(e => e.RowNumber == 2 && e.FieldKey == "email");
        errors.Should().Contain(e => e.RowNumber == 3 && e.FieldKey == "name");
    }

    [Fact]
    public async Task ImportBatch_ContinuesPastInvalidRows_AndImportsTheValidOnes()
    {
        var target = Target();

        var result = await target.ImportBatchAsync("session-1", [Row(1), Row(2, email: "broken"), Row(3)]);

        result.ImportedCount.Should().Be(2);
        result.Errors.Should().ContainSingle(e => e.RowNumber == 2);
        target.ImportedRows.Should().HaveCount(2);
        target.ImportedRows.Select(r => r.RowNumber).Should().Equal(1, 3);
    }

    [Fact]
    public async Task Rollback_RemovesOnlyTheSessionsRows()
    {
        var target = Target();
        await target.ImportBatchAsync("session-1", [Row(1)]);
        await target.ImportBatchAsync("session-2", [Row(2)]);

        await target.RollbackAsync("session-1");

        target.ImportedRows.Should().ContainSingle(r => r.RowNumber == 2);
    }

    [Fact]
    public async Task ImportedRows_AreClones_NotLiveReferences()
    {
        var target = Target();
        await target.ImportBatchAsync("session-1", [Row(1)]);

        target.ImportedRows[0].Values["name"] = "Mutated";

        target.ImportedRows[0].Values["name"].Should().Be("Alice");
    }

    // ── FluentValidation adapter ─────────────────────────────────────────────

    private sealed class ContactRow
    {
        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public int? Age { get; set; }
    }

    private sealed class ContactRowValidator : AbstractValidator<ContactRow>
    {
        public ContactRowValidator()
        {
            RuleFor(c => c.Name).NotEmpty().WithMessage("Name is required");
            RuleFor(c => c.Email).Must(e => e.Contains('@')).WithMessage("Invalid e-mail");
            RuleFor(c => c.Age).Must(a => a is null or (>= 0 and <= 130)).WithMessage("Age out of range");
        }
    }

    private static FluentValidationDataImportValidator<ContactRow> Adapter()
        => new(
            new ContactRowValidator(),
            row => new ContactRow
            {
                Name = row.Values.GetValueOrDefault("name") ?? string.Empty,
                Email = row.Values.GetValueOrDefault("email") ?? string.Empty,
                Age = int.TryParse(row.Values.GetValueOrDefault("age"), out var age) ? age : null
            });

    [Fact]
    public async Task Adapter_MapsFailures_ToRowNumbersAndFieldKeys()
    {
        var errors = await Adapter().ValidateAsync([Row(5, name: ""), Row(9, age: "999")]);

        errors.Should().Contain(e => e.RowNumber == 5 && e.FieldKey == "name" && e.Message == "Name is required");
        errors.Should().Contain(e => e.RowNumber == 9 && e.FieldKey == "age" && e.Message == "Age out of range");
    }

    [Fact]
    public async Task Adapter_ValidRows_ProduceNoErrors()
    {
        (await Adapter().ValidateAsync([Row(1), Row(2)])).Should().BeEmpty();
    }

    [Fact]
    public async Task Adapter_BinderException_BecomesARowError()
    {
        var adapter = new FluentValidationDataImportValidator<ContactRow>(
            new ContactRowValidator(),
            _ => throw new FormatException("bad row shape"));

        var errors = await adapter.ValidateAsync([Row(7)]);

        errors.Should().ContainSingle(e => e.RowNumber == 7 && e.Message.Contains("bad row shape"));
    }

    [Fact]
    public async Task Adapter_CustomFieldKeyMap_IsApplied()
    {
        var adapter = new FluentValidationDataImportValidator<ContactRow>(
            new ContactRowValidator(),
            row => new ContactRow { Email = row.Values.GetValueOrDefault("email") ?? string.Empty, Name = "x" },
            property => $"col_{property.ToLowerInvariant()}");

        var errors = await adapter.ValidateAsync([Row(1, email: "broken")]);

        errors.Should().ContainSingle(e => e.FieldKey == "col_email");
    }
}
