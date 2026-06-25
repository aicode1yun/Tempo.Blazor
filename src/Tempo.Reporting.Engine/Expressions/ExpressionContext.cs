#pragma warning disable MA0048

namespace Tempo.Reporting.Engine.Expressions;

/// <summary>Explicit runtime context available to expression evaluation.</summary>
public sealed record ExpressionContext
{
    /// <summary>Creates an expression context.</summary>
    public ExpressionContext(
        IReadOnlyDictionary<string, object?> fields,
        IReadOnlyDictionary<string, object?> parameters,
        ExpressionGlobals? globals = null)
    {
        Fields = new Dictionary<string, object?>(fields, StringComparer.Ordinal);
        Parameters = new Dictionary<string, object?>(parameters, StringComparer.Ordinal);
        Globals = globals ?? new ExpressionGlobals();
    }

    /// <summary>Current row fields.</summary>
    public IReadOnlyDictionary<string, object?> Fields { get; }

    /// <summary>Report parameter values.</summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; }

    /// <summary>Report global values.</summary>
    public ExpressionGlobals Globals { get; }
}

/// <summary>Global values exposed to expressions.</summary>
public sealed record ExpressionGlobals
{
    /// <summary>Execution timestamp.</summary>
    public DateTimeOffset ExecutionTime { get; init; } = DateTimeOffset.MinValue;

    /// <summary>User name.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Tenant name.</summary>
    public string TenantName { get; init; } = string.Empty;
}

#pragma warning restore MA0048
