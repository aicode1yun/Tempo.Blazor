using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>Event arguments for the spreadsheet open/import event.</summary>
public sealed class SpreadsheetOpenEventArgs : EventArgs
{
    /// <summary>The name of the imported file.</summary>
    public string FileName { get; }

    /// <summary>The raw XLSX file bytes.</summary>
    public byte[] Data { get; }

    /// <summary>The imported workbook.</summary>
    public SpreadsheetWorkbook Workbook { get; }

    public SpreadsheetOpenEventArgs(string fileName, byte[] data, SpreadsheetWorkbook workbook)
    {
        FileName = fileName;
        Data = data;
        Workbook = workbook;
    }
}
