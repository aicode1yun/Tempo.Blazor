using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet.Formula;

/// <summary>
/// High-level facade for the formula engine: lex, parse and evaluate.
/// </summary>
public sealed class FormulaEngine
{
    private readonly FunctionRegistry _registry;
    private readonly FormulaEvaluator _evaluator;

    public FormulaEngine()
    {
        _registry = new FunctionRegistry();
        SpreadsheetFunctions.RegisterMathFunctions(_registry);
        SpreadsheetFunctions.RegisterTextFunctions(_registry);
        SpreadsheetFunctions.RegisterLogicalFunctions(_registry);
        SpreadsheetFunctions.RegisterDateTimeFunctions(_registry);
        SpreadsheetFunctions.RegisterLookupFunctions(_registry);
        _evaluator = new FormulaEvaluator(_registry);
    }

    /// <summary>Evaluates a formula string against the given sheet.</summary>
    public object? Evaluate(string formula, SpreadsheetSheet sheet)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return null;

        var tokens = FormulaLexer.Tokenize(formula);
        var parser = new FormulaParser(tokens);
        var ast = parser.Parse();
        var context = new FormulaContext(sheet);
        return _evaluator.Evaluate(ast, context);
    }

    /// <summary>Registers a custom function.</summary>
    public void RegisterFunction(string name, Func<FormulaContext, List<object?>, object?> implementation)
    {
        _registry.Register(name, implementation);
    }
}
