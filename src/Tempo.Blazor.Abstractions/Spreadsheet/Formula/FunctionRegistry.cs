namespace Tempo.Blazor.Components.Spreadsheet.Formula;

/// <summary>
/// Registry of built-in spreadsheet functions.
/// </summary>
public sealed class FunctionRegistry
{
    private readonly Dictionary<string, Func<FormulaContext, List<object?>, object?>> _functions = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string name, Func<FormulaContext, List<object?>, object?> implementation)
    {
        _functions[name.ToUpperInvariant()] = implementation;
    }

    public bool TryGet(string name, out Func<FormulaContext, List<object?>, object?>? implementation)
    {
        return _functions.TryGetValue(name.ToUpperInvariant(), out implementation);
    }

    public object? Invoke(string name, FormulaContext context, List<object?> args)
    {
        if (_functions.TryGetValue(name.ToUpperInvariant(), out var fn))
            return fn(context, args);
        throw new InvalidOperationException($"Unknown function: {name}");
    }
}
