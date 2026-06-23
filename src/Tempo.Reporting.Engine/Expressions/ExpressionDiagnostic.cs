#pragma warning disable MA0048

using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Tempo.Reporting.Engine.Expressions;

/// <summary>Structured expression diagnostic with stable code and source position.</summary>
public sealed record ExpressionDiagnostic(
    string Code,
    string Message,
    int Line,
    int Column);

internal static class ExpressionDiagnostics
{
    private static readonly ResourceManager ResourceManager = new(
        "Tempo.Reporting.Engine.Resources.ExpressionResources",
        Assembly.GetExecutingAssembly());

    public static ExpressionDiagnostic Create(string code, int line, int column, params object?[] args)
    {
        var key = code.Replace('.', '_');
        var format = ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? code;
        var message = args.Length == 0
            ? format
            : string.Format(CultureInfo.CurrentCulture, format, args);
        return new ExpressionDiagnostic(code, message, line, column);
    }
}

#pragma warning restore MA0048
