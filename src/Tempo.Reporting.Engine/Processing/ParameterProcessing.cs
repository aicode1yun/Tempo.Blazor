#pragma warning disable MA0048

using System.Globalization;
using Tempo.Reporting.Abstractions;
using Tempo.Reporting.Abstractions.Data;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Expressions;

namespace Tempo.Reporting.Engine.Processing;

/// <summary>Resolved report parameter option.</summary>
public sealed record ReportResolvedParameterAvailableValue(object? Value, string? Label);

/// <summary>Resolved parameter values plus available-value lists.</summary>
public sealed record ReportParameterResolution
{
    /// <summary>Creates a parameter resolution result.</summary>
    public ReportParameterResolution(
        IReadOnlyDictionary<string, ReportParameterValue> values,
        IReadOnlyDictionary<string, IReadOnlyList<ReportResolvedParameterAvailableValue>> availableValues)
    {
        Values = new Dictionary<string, ReportParameterValue>(values, StringComparer.Ordinal);
        AvailableValues = new Dictionary<string, IReadOnlyList<ReportResolvedParameterAvailableValue>>(
            availableValues,
            StringComparer.Ordinal);
    }

    /// <summary>Resolved parameter values.</summary>
    public IReadOnlyDictionary<string, ReportParameterValue> Values { get; }

    /// <summary>Resolved available values keyed by parameter name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ReportResolvedParameterAvailableValue>> AvailableValues { get; }
}

/// <summary>Resolves and validates report parameters before data processing.</summary>
public static class ReportParameterProcessor
{
    /// <summary>Resolves supplied/default values and available values for all report parameters.</summary>
    public static async Task<ReportParameterResolution> ResolveAsync(
        ReportDefinition definition,
        IReportDataProvider provider,
        IReadOnlyDictionary<string, ReportParameterValue> suppliedValues,
        ReportExecutionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(suppliedValues);
        ArgumentNullException.ThrowIfNull(executionContext);

        var culture = CreateCulture(executionContext.CultureName);
        var values = new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal);
        var availableValues = new Dictionary<string, IReadOnlyList<ReportResolvedParameterAvailableValue>>(StringComparer.Ordinal);

        foreach (var parameter in definition.Parameters)
        {
            var available = await ResolveAvailableValuesAsync(
                parameter,
                definition,
                provider,
                values,
                executionContext,
                culture).ConfigureAwait(false);
            if (available.Count > 0)
            {
                availableValues[parameter.Name] = available;
            }

            var resolved = ResolveParameterValue(parameter, suppliedValues, values, culture);
            if (resolved is null)
            {
                if (parameter.Required)
                {
                    throw new ReportProcessingException(
                        "Parameters.Required",
                        $"Parameter '{parameter.Name}' is required.");
                }

                continue;
            }

            ValidateAvailableValue(parameter, resolved, available);
            values[parameter.Name] = resolved;
        }

        return new ReportParameterResolution(values, availableValues);
    }

    private static ReportParameterValue? ResolveParameterValue(
        ReportParameterDefinition parameter,
        IReadOnlyDictionary<string, ReportParameterValue> suppliedValues,
        IReadOnlyDictionary<string, ReportParameterValue> resolvedValues,
        CultureInfo culture)
    {
        if (suppliedValues.TryGetValue(parameter.Name, out var supplied))
        {
            return CoerceParameterValue(parameter, supplied, culture);
        }

        if (string.IsNullOrWhiteSpace(parameter.DefaultExpression))
        {
            return null;
        }

        using var _ = new CultureScope(culture);
        var context = new ExpressionContext(
            new Dictionary<string, object?>(StringComparer.Ordinal),
            ParameterScalars(resolvedValues));
        var value = ExpressionEvaluator.Evaluate(parameter.DefaultExpression, context);
        return CoerceParameterValue(parameter, ReportParameterValue.Scalar(value.RawValue), culture);
    }

    private static ReportParameterValue CoerceParameterValue(
        ReportParameterDefinition parameter,
        ReportParameterValue value,
        CultureInfo culture)
    {
        if (!parameter.AllowMultipleValues && value.Values.Count > 1)
        {
            throw new ReportProcessingException(
                "Parameters.MultiValueNotAllowed",
                $"Parameter '{parameter.Name}' does not allow multiple values.");
        }

        try
        {
            var coerced = value.Values.Select(item => CoerceScalar(parameter, item, culture)).ToArray();
            return value.Values.Count > 1
                ? ReportParameterValue.Multiple(coerced)
                : ReportParameterValue.Scalar(coerced.FirstOrDefault());
        }
        catch (FormatException exception)
        {
            throw new ReportProcessingException(
                "Parameters.InvalidValue",
                $"Parameter '{parameter.Name}' has an invalid value.",
                exception);
        }
        catch (InvalidCastException exception)
        {
            throw new ReportProcessingException(
                "Parameters.InvalidValue",
                $"Parameter '{parameter.Name}' has an invalid value.",
                exception);
        }
    }

    private static object? CoerceScalar(ReportParameterDefinition parameter, object? value, CultureInfo culture)
    {
        if (value is null)
        {
            return null;
        }

        return parameter.DataType switch
        {
            ReportParameterType.Number => CoerceNumber(value, culture),
            ReportParameterType.Date => CoerceDate(value, culture),
            ReportParameterType.Boolean => CoerceBoolean(value, culture),
            ReportParameterType.String or ReportParameterType.List => Convert.ToString(value, culture),
            _ => value,
        };
    }

    private static decimal CoerceNumber(object value, CultureInfo culture)
    {
        if (value is decimal d)
        {
            return d;
        }

        if (value is byte or short or int or long or float or double)
        {
            return Convert.ToDecimal(value, culture);
        }

        if (value is string text &&
            decimal.TryParse(text, NumberStyles.Number, culture, out var number))
        {
            return number;
        }

        throw new FormatException("Invalid number parameter value.");
    }

    private static DateTimeOffset CoerceDate(object value, CultureInfo culture)
    {
        if (value is DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset;
        }

        if (value is DateTime dateTime)
        {
            return new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified), TimeSpan.Zero);
        }

        if (value is string text &&
            DateTimeOffset.TryParse(text, culture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed;
        }

        throw new FormatException("Invalid date parameter value.");
    }

    private static bool CoerceBoolean(object value, CultureInfo culture)
    {
        if (value is bool boolean)
        {
            return boolean;
        }

        if (value is string text && bool.TryParse(text, out var parsed))
        {
            return parsed;
        }

        if (value is string numberText &&
            decimal.TryParse(numberText, NumberStyles.Number, culture, out var number))
        {
            return number != 0m;
        }

        if (value is byte or short or int or long or float or double or decimal)
        {
            return Convert.ToDecimal(value, culture) != 0m;
        }

        throw new FormatException("Invalid boolean parameter value.");
    }

    private static async Task<IReadOnlyList<ReportResolvedParameterAvailableValue>> ResolveAvailableValuesAsync(
        ReportParameterDefinition parameter,
        ReportDefinition definition,
        IReportDataProvider provider,
        IReadOnlyDictionary<string, ReportParameterValue> resolvedValues,
        ReportExecutionContext executionContext,
        CultureInfo culture)
    {
        if (parameter.AvailableValues is null)
        {
            return [];
        }

        if (parameter.AvailableValues.Kind == ReportParameterAvailableValuesKind.Static)
        {
            return parameter.AvailableValues.StaticValues
                .Select(value => new ReportResolvedParameterAvailableValue(value.Value, value.Label))
                .ToArray();
        }

        if (string.IsNullOrWhiteSpace(parameter.AvailableValues.DataSetName))
        {
            return [];
        }

        var dataSetDefinition = definition.DataSets
            .FirstOrDefault(dataSet => string.Equals(
                dataSet.Name,
                parameter.AvailableValues.DataSetName,
                StringComparison.Ordinal));
        var query = new ReportDataQuery
        {
            SourceName = dataSetDefinition?.Source?.Name,
            Text = dataSetDefinition?.Query,
        };
        var providerParameters = BindDataSetParameters(dataSetDefinition, resolvedValues, culture);
        var result = await provider.GetDataAsync(
            parameter.AvailableValues.DataSetName,
            query,
            providerParameters,
            executionContext).ConfigureAwait(false);
        var dataSet = await ReportDataSetRuntime.LoadAsync(
            parameter.AvailableValues.DataSetName,
            result,
            executionContext.CancellationToken).ConfigureAwait(false);

        return dataSet.Rows
            .Select(row => new ReportResolvedParameterAvailableValue(
                ResolveFieldOrExpression(parameter.AvailableValues.ValueField, row, resolvedValues, culture),
                Convert.ToString(
                    ResolveFieldOrExpression(parameter.AvailableValues.LabelField ?? parameter.AvailableValues.ValueField, row, resolvedValues, culture),
                    culture)))
            .ToArray();
    }

    private static IReadOnlyDictionary<string, ReportParameterValue> BindDataSetParameters(
        ReportDataSetDefinition? dataSetDefinition,
        IReadOnlyDictionary<string, ReportParameterValue> resolvedValues,
        CultureInfo culture)
    {
        if (dataSetDefinition is null || dataSetDefinition.Parameters.Count == 0)
        {
            return new Dictionary<string, ReportParameterValue>(resolvedValues, StringComparer.Ordinal);
        }

        using var _ = new CultureScope(culture);
        var bound = new Dictionary<string, ReportParameterValue>(StringComparer.Ordinal);
        var context = new ExpressionContext(
            new Dictionary<string, object?>(StringComparer.Ordinal),
            ParameterScalars(resolvedValues));

        foreach (var binding in dataSetDefinition.Parameters)
        {
            var value = ExpressionEvaluator.Evaluate(binding.Expression, context);
            bound[binding.Name] = ReportParameterValue.Scalar(value.RawValue);
        }

        return bound;
    }

    private static object? ResolveFieldOrExpression(
        string? fieldOrExpression,
        ProcessedDataRow row,
        IReadOnlyDictionary<string, ReportParameterValue> parameters,
        CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(fieldOrExpression))
        {
            return null;
        }

        if (!fieldOrExpression.TrimStart().StartsWith('='))
        {
            return row[fieldOrExpression];
        }

        using var _ = new CultureScope(culture);
        var value = ExpressionEvaluator.Evaluate(
            fieldOrExpression,
            new ExpressionContext(row.Values, ParameterScalars(parameters)));
        return value.RawValue;
    }

    private static void ValidateAvailableValue(
        ReportParameterDefinition parameter,
        ReportParameterValue value,
        IReadOnlyList<ReportResolvedParameterAvailableValue> availableValues)
    {
        if (availableValues.Count == 0 || parameter.DataType != ReportParameterType.List)
        {
            return;
        }

        var allowed = new HashSet<string>(
            availableValues.Select(item => Convert.ToString(item.Value, CultureInfo.InvariantCulture) ?? string.Empty),
            StringComparer.Ordinal);
        var invalid = value.Values.Any(item => !allowed.Contains(Convert.ToString(item, CultureInfo.InvariantCulture) ?? string.Empty));
        if (invalid)
        {
            throw new ReportProcessingException(
                "Parameters.InvalidValue",
                $"Parameter '{parameter.Name}' has a value outside its available values.");
        }
    }

    private static IReadOnlyDictionary<string, object?> ParameterScalars(
        IReadOnlyDictionary<string, ReportParameterValue> parameters)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in parameters)
        {
            values[pair.Key] = pair.Value.Values.Count > 1 ? pair.Value.Values.ToArray() : pair.Value.ScalarValue;
        }

        return values;
    }

    private static CultureInfo CreateCulture(string cultureName)
    {
        try
        {
            return CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(cultureName) ? "en-US" : cultureName);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo("en-US");
        }
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture;
        private readonly CultureInfo _uiCulture;

        public CultureScope(CultureInfo culture)
        {
            _culture = CultureInfo.CurrentCulture;
            _uiCulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }
}
