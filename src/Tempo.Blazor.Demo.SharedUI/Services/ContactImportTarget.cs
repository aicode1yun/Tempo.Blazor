using FluentValidation;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.FluentValidation.Import;
using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Demo.SharedUI.Services;

/// <summary>
/// Demo <see cref="IDataImportTarget"/>: a contact "database" with FluentValidation row
/// rules (via <see cref="FluentValidationDataImportValidator{TModel}"/>), per-session
/// storage with rollback, and a change event so the demo database panel refreshes live.
/// </summary>
public sealed class ContactImportTarget : IDataImportTarget
{
    private readonly object _gate = new();
    private readonly List<(string SessionId, ContactRow Contact)> _contacts = [];
    private readonly FluentValidationDataImportValidator<ContactRow> _validator = new(
        new ContactRowValidator(),
        row => new ContactRow
        {
            RowNumber = row.RowNumber,
            Name = row.Values.GetValueOrDefault("name") ?? string.Empty,
            Email = row.Values.GetValueOrDefault("email") ?? string.Empty,
            AgeText = row.Values.GetValueOrDefault("age") ?? string.Empty,
            City = row.Values.GetValueOrDefault("city") ?? string.Empty
        });

    /// <summary>Raised whenever the stored contacts change (import or rollback).</summary>
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<ImportTargetField> Fields { get; } =
    [
        new ImportTargetField("name", "Name", Required: true),
        new ImportTargetField("email", "Email", Required: true),
        new ImportTargetField("age", "Age"),
        new ImportTargetField("city", "City")
    ];

    /// <summary>Number of stored contacts.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _contacts.Count;
            }
        }
    }

    /// <summary>Snapshot of the most recently imported contacts, newest first.</summary>
    public IReadOnlyList<ContactRow> Latest(int take)
    {
        lock (_gate)
        {
            return _contacts.Select(entry => entry.Contact)
                .Reverse()
                .Take(take)
                .ToList();
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DataImportRowError>> ValidateBatchAsync(
        IReadOnlyList<DataImportRow> rows, CancellationToken cancellationToken = default)
        => _validator.ValidateAsync(rows, cancellationToken);

    /// <inheritdoc />
    public async Task<DataImportBatchResult> ImportBatchAsync(
        string sessionId, IReadOnlyList<DataImportRow> rows, CancellationToken cancellationToken = default)
    {
        var errors = await _validator.ValidateAsync(rows, cancellationToken);
        var invalid = errors.Select(e => e.RowNumber).ToHashSet();
        var imported = 0;

        lock (_gate)
        {
            foreach (var row in rows.Where(r => !invalid.Contains(r.RowNumber)))
            {
                _contacts.Add((sessionId, new ContactRow
                {
                    RowNumber = row.RowNumber,
                    Name = row.Values.GetValueOrDefault("name") ?? string.Empty,
                    Email = row.Values.GetValueOrDefault("email") ?? string.Empty,
                    AgeText = row.Values.GetValueOrDefault("age") ?? string.Empty,
                    City = row.Values.GetValueOrDefault("city") ?? string.Empty
                }));
                imported++;
            }
        }

        Changed?.Invoke();
        return new DataImportBatchResult { ImportedCount = imported, Errors = errors };
    }

    /// <inheritdoc />
    public Task RollbackAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _contacts.RemoveAll(entry => string.Equals(entry.SessionId, sessionId, StringComparison.Ordinal));
        }

        Changed?.Invoke();
        return Task.CompletedTask;
    }

    /// <summary>One imported contact.</summary>
    public sealed class ContactRow
    {
        public int RowNumber { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;

        public string AgeText { get; init; } = string.Empty;

        public string City { get; init; } = string.Empty;
    }

    private sealed class ContactRowValidator : AbstractValidator<ContactRow>
    {
        public ContactRowValidator()
        {
            RuleFor(c => c.Name)
                .NotEmpty().WithMessage("Name is required");
            RuleFor(c => c.Email)
                .Must(email => email.Contains('@') && email.Contains('.'))
                .WithMessage("Invalid e-mail address");
            RuleFor(c => c.AgeText)
                .Must(age => string.IsNullOrWhiteSpace(age) || (int.TryParse(age, out var value) && value is >= 0 and <= 130))
                .WithMessage("Age must be a number between 0 and 130")
                .OverridePropertyName("Age");
        }
    }
}
