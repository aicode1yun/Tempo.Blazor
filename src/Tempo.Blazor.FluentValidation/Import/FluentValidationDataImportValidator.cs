using global::FluentValidation;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.FluentValidation.Import;

/// <summary>
/// Adapts a FluentValidation <see cref="IValidator{T}"/> to data-import row validation:
/// each <see cref="DataImportRow"/> is bound to <typeparamref name="TModel"/> and validated,
/// and every failure becomes a <see cref="DataImportRowError"/> carrying the source row
/// number and the target field key. Plug the <see cref="ValidateAsync"/> method into an
/// <see cref="IDataImportTarget"/> (or call it directly from one).
/// </summary>
/// <typeparam name="TModel">Strongly-typed shape one import row binds to.</typeparam>
public sealed class FluentValidationDataImportValidator<TModel>
{
    private readonly IValidator<TModel> _validator;
    private readonly Func<DataImportRow, TModel> _binder;
    private readonly Func<string, string> _propertyToFieldKey;

    /// <summary>
    /// Creates the adapter. <paramref name="propertyToFieldKey"/> maps a failed property name
    /// to the import field key; the default lower-cases the first letter (Name → name).
    /// </summary>
    public FluentValidationDataImportValidator(
        IValidator<TModel> validator,
        Func<DataImportRow, TModel> binder,
        Func<string, string>? propertyToFieldKey = null)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _binder = binder ?? throw new ArgumentNullException(nameof(binder));
        _propertyToFieldKey = propertyToFieldKey ?? DefaultFieldKey;
    }

    /// <summary>Validates the rows and returns one error per failed rule (empty when clean).</summary>
    public async Task<IReadOnlyList<DataImportRowError>> ValidateAsync(
        IReadOnlyList<DataImportRow> rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var errors = new List<DataImportRowError>();

        foreach (var row in rows)
        {
            TModel model;
            try
            {
                model = _binder(row);
            }
            catch (Exception exception)
            {
                // A row the binder cannot even shape is a whole-row error, not a crash.
                errors.Add(new DataImportRowError { RowNumber = row.RowNumber, Message = exception.Message });
                continue;
            }

            var result = await _validator.ValidateAsync(model, cancellationToken).ConfigureAwait(false);
            foreach (var failure in result.Errors)
            {
                errors.Add(new DataImportRowError
                {
                    RowNumber = row.RowNumber,
                    FieldKey = string.IsNullOrEmpty(failure.PropertyName) ? null : _propertyToFieldKey(failure.PropertyName),
                    Message = failure.ErrorMessage
                });
            }
        }

        return errors;
    }

    private static string DefaultFieldKey(string propertyName)
        => propertyName.Length == 0
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
