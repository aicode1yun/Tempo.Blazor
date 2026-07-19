using System.Reflection;

namespace Tempo.Blazor.DocumentFormats.HeadlessLayout;

/// <summary>
/// Access to the embedded headless document layout bundle — the canvas editor's layout chain
/// (layout snapshot export → display list → pagination → line breaker/paragraph engine/font
/// metrics) compiled into a single ESM artifact by <c>scripts/build-document-editor.mjs</c>
/// (<c>npm run build:document-editor</c>). Server-side layout hosts evaluate this script to
/// produce the exact layout snapshot the browser editor exports (WYSIWYG parity by construction).
/// </summary>
public static class TempoDocumentHeadlessLayoutBundle
{
    /// <summary>Manifest resource name of the embedded ESM bundle.</summary>
    public const string ResourceName = "Tempo.Blazor.DocumentFormats.HeadlessLayout.tempo-document-headless-layout.bundle.mjs";

    /// <summary>Opens a fresh read-only stream over the embedded bundle.</summary>
    public static Stream OpenStream()
        => typeof(TempoDocumentHeadlessLayoutBundle).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' is missing from {typeof(TempoDocumentHeadlessLayoutBundle).Assembly.GetName().Name}. " +
                "Rebuild the bundle via `npm run build:document-editor`.");

    /// <summary>Reads the embedded bundle as UTF-8 JavaScript text.</summary>
    public static string ReadJavaScript()
    {
        using var stream = OpenStream();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
