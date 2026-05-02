namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>Event arguments for the spreadsheet download/export event.</summary>
public sealed class SpreadsheetDownloadEventArgs : EventArgs
{
    /// <summary>The name of the exported file.</summary>
    public string FileName { get; }

    /// <summary>The raw XLSX file bytes.</summary>
    public byte[] Data { get; }

    public SpreadsheetDownloadEventArgs(string fileName, byte[] data)
    {
        FileName = fileName;
        Data = data;
    }
}
