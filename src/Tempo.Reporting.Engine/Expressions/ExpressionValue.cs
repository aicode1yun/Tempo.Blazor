#pragma warning disable MA0048

using System.Globalization;

namespace Tempo.Reporting.Engine.Expressions;

/// <summary>Expression runtime value kind.</summary>
public enum ExpressionValueKind
{
    /// <summary>Null value.</summary>
    Null,

    /// <summary>Numeric decimal value.</summary>
    Number,

    /// <summary>String value.</summary>
    String,

    /// <summary>Boolean value.</summary>
    Boolean,

    /// <summary>Date-time value.</summary>
    Date,

    /// <summary>Deferred placeholder resolved by a later pipeline phase.</summary>
    Deferred,
}

/// <summary>Deferred expression placeholder kind.</summary>
public enum ExpressionDeferredKind
{
    /// <summary>No deferred placeholder.</summary>
    None,

    /// <summary>Page number placeholder.</summary>
    PageNumber,

    /// <summary>Total pages placeholder.</summary>
    TotalPages,

    /// <summary>Page-scoped aggregate placeholder resolved by layout after pagination.</summary>
    PageAggregate,
}

/// <summary>Typed expression value.</summary>
public sealed record ExpressionValue
{
    private ExpressionValue(ExpressionValueKind kind, object? value, ExpressionDeferredKind deferredKind = ExpressionDeferredKind.None)
    {
        Kind = kind;
        RawValue = value;
        DeferredKind = deferredKind;
    }

    /// <summary>Null expression value.</summary>
    public static ExpressionValue Null { get; } = new(ExpressionValueKind.Null, null);

    /// <summary>Value kind.</summary>
    public ExpressionValueKind Kind { get; }

    /// <summary>Raw CLR value.</summary>
    public object? RawValue { get; }

    /// <summary>Deferred placeholder kind.</summary>
    public ExpressionDeferredKind DeferredKind { get; }

    /// <summary>Creates a number value.</summary>
    public static ExpressionValue Number(decimal value) => new(ExpressionValueKind.Number, value);

    /// <summary>Creates a string value.</summary>
    public static ExpressionValue String(string value) => new(ExpressionValueKind.String, value);

    /// <summary>Creates a boolean value.</summary>
    public static ExpressionValue Boolean(bool value) => new(ExpressionValueKind.Boolean, value);

    /// <summary>Creates a date value.</summary>
    public static ExpressionValue Date(DateTimeOffset value) => new(ExpressionValueKind.Date, value);

    /// <summary>Creates a deferred placeholder value.</summary>
    public static ExpressionValue Deferred(ExpressionDeferredKind kind) => new(ExpressionValueKind.Deferred, null, kind);

    /// <summary>Wraps a CLR value as an expression value.</summary>
    public static ExpressionValue FromObject(object? value)
    {
        if (value is null)
        {
            return Null;
        }

        if (value is ExpressionValue expressionValue)
        {
            return expressionValue;
        }

        return value switch
        {
            decimal d => Number(d),
            int i => Number(i),
            long l => Number(l),
            short s => Number(s),
            byte b => Number(b),
            double d => Number((decimal)d),
            float f => Number((decimal)f),
            bool b => Boolean(b),
            string s => String(s),
            DateTime dateTime => Date(new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified), TimeSpan.Zero)),
            DateTimeOffset dateTimeOffset => Date(dateTimeOffset),
            _ => String(Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty),
        };
    }

    /// <summary>Coerces the value to a number.</summary>
    public decimal AsNumber()
    {
        return Kind switch
        {
            ExpressionValueKind.Number => (decimal)RawValue!,
            ExpressionValueKind.String => ParseDecimal((string)RawValue!),
            ExpressionValueKind.Boolean => (bool)RawValue! ? 1m : 0m,
            ExpressionValueKind.Null => 0m,
            _ => throw new InvalidOperationException($"Cannot convert {Kind} to number."),
        };
    }

    /// <summary>Coerces the value to a string.</summary>
    public string AsString()
    {
        return Kind switch
        {
            ExpressionValueKind.Null => string.Empty,
            ExpressionValueKind.String => (string)RawValue!,
            ExpressionValueKind.Number => ((decimal)RawValue!).ToString(CultureInfo.InvariantCulture),
            ExpressionValueKind.Boolean => ((bool)RawValue!).ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
            ExpressionValueKind.Date => ((DateTimeOffset)RawValue!).ToString("O", CultureInfo.InvariantCulture),
            ExpressionValueKind.Deferred => DeferredKind.ToString(),
            _ => string.Empty,
        };
    }

    /// <summary>Coerces the value to a boolean.</summary>
    public bool AsBoolean()
    {
        return Kind switch
        {
            ExpressionValueKind.Boolean => (bool)RawValue!,
            ExpressionValueKind.Number => AsNumber() != 0m,
            ExpressionValueKind.String => ParseBoolean((string)RawValue!),
            ExpressionValueKind.Null => false,
            _ => throw new InvalidOperationException($"Cannot convert {Kind} to boolean."),
        };
    }

    /// <summary>Coerces the value to a date.</summary>
    public DateTimeOffset AsDate()
    {
        if (Kind == ExpressionValueKind.Date)
        {
            return (DateTimeOffset)RawValue!;
        }

        if (Kind == ExpressionValueKind.String &&
            DateTimeOffset.TryParse((string)RawValue!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
        {
            return date;
        }

        throw new InvalidOperationException($"Cannot convert {Kind} to date.");
    }

    private static decimal ParseDecimal(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariant))
        {
            return invariant;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var current))
        {
            return current;
        }

        throw new InvalidOperationException($"Cannot convert '{value}' to number.");
    }

    private static bool ParseBoolean(string value)
    {
        if (bool.TryParse(value, out var boolean))
        {
            return boolean;
        }

        return ParseDecimal(value) != 0m;
    }
}

#pragma warning restore MA0048
