namespace Tempo.Blazor.Components.Spreadsheet.Formula;

/// <summary>
/// Represents a spreadsheet error value such as #DIV/0!, #VALUE!, #NAME?, #REF!, or #N/A.
/// </summary>
public sealed class FormulaError
{
    public string Code { get; }

    public FormulaError(string code)
    {
        Code = code;
    }

    public override string ToString() => Code;
}
