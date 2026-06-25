namespace Tempo.Blazor.Components.DocumentEditor;

/// <summary>Stable string identifiers for document editor pending operations tracked in <see cref="DocumentPendingActionService"/>.</summary>
internal static class PendingActionId
{
    public const string Save = "save";
    public const string AutosaveWaiting = "autosave-waiting";
    public const string ExportPdf = "export-pdf";
    public const string ImportDocx = "import-docx";
    public const string ExportDocx = "export-docx";
    public const string ImageUpload = "image-upload";
    public const string CollaborationSync = "collab-sync";
    public const string OfflineSync = "offline-sync";
}
