using Tempo.Blazor.Components.DocumentEditor.Registry;

namespace Tempo.Blazor.Components.DocumentEditor.Features;

/// <summary>Factory for the default document editor feature set.</summary>
public static class DocumentEditorBuiltInFeatures
{
    /// <summary>Creates the default feature registry used by the editor.</summary>
    public static DocumentEditorFeatureRegistry CreateDefaultRegistry()
    {
        var registry = new DocumentEditorFeatureRegistry();
        registry.Register(new TextFormattingFeature());
        registry.Register(new ParagraphFeature());
        registry.Register(new ClipboardFeature());
        registry.Register(new FindReplaceFeature());
        registry.Register(new ImageFeature());
        registry.Register(new TableFeature());
        registry.Register(new CommentsFeature());
        registry.Register(new TrackChangesFeature());
        registry.Register(new HeadersFootersFeature());
        registry.Register(new ImportExportFeature());
        registry.Register(new RestrictedEditingFeature());
        registry.Register(new OfflineCollaborationFeature());
        return registry;
    }
}

/// <summary>Base implementation for built-in feature skeletons.</summary>
public abstract class DocumentEditorFeatureBase : IDocumentEditorFeature
{
    /// <summary>Initializes a new feature skeleton.</summary>
    protected DocumentEditorFeatureBase(string name, IReadOnlyList<string>? requires = null)
    {
        Name = name;
        Requires = requires ?? [];
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Requires { get; }

    /// <inheritdoc />
    public virtual void RegisterCommands(DocumentEditorCommandRegistry commands) { }

    /// <inheritdoc />
    public virtual void RegisterToolbar(DocumentEditorToolbarRegistry toolbar) { }

    /// <inheritdoc />
    public virtual void RegisterShortcuts(DocumentEditorShortcutRegistry shortcuts) { }

    /// <inheritdoc />
    public virtual void RegisterFloatingUi(DocumentFloatingUiRegistry floatingUi) { }

    /// <inheritdoc />
    public virtual void ConfigureSchema(DocumentEditorSchemaBuilder schema) { }
}

/// <summary>Text formatting feature skeleton.</summary>
public sealed class TextFormattingFeature : DocumentEditorFeatureBase
{
    /// <summary>Initializes a new text formatting feature.</summary>
    public TextFormattingFeature() : base(DocumentEditorFeatureNames.TextFormatting) { }
}

/// <summary>Paragraph feature skeleton.</summary>
public sealed class ParagraphFeature : DocumentEditorFeatureBase
{
    /// <summary>Initializes a new paragraph feature.</summary>
    public ParagraphFeature() : base(DocumentEditorFeatureNames.Paragraph) { }
}

/// <summary>Clipboard feature skeleton.</summary>
public sealed class ClipboardFeature : DocumentEditorFeatureBase
{
    /// <summary>Initializes a new clipboard feature.</summary>
    public ClipboardFeature() : base(DocumentEditorFeatureNames.Clipboard) { }
}

/// <summary>Find and replace feature skeleton.</summary>
public sealed class FindReplaceFeature : DocumentEditorFeatureBase
{
    /// <summary>Initializes a new find and replace feature.</summary>
    public FindReplaceFeature() : base(DocumentEditorFeatureNames.FindReplace) { }
}

/// <summary>Image feature skeleton.</summary>
public sealed class ImageFeature : DocumentEditorFeatureBase
{
    /// <summary>Initializes a new image feature.</summary>
    public ImageFeature() : base(DocumentEditorFeatureNames.Image) { }
}

/// <summary>Table feature skeleton.</summary>
public sealed class TableFeature : DocumentEditorFeatureBase
{
    /// <summary>Initializes a new table feature.</summary>
    public TableFeature() : base(DocumentEditorFeatureNames.Table) { }
}

/// <summary>Comments feature skeleton.</summary>
public sealed class CommentsFeature : DocumentEditorFeatureBase
{
    /// <summary>Initializes a new comments feature.</summary>
    public CommentsFeature() : base(DocumentEditorFeatureNames.Comments) { }
}

/// <summary>Track changes feature skeleton.</summary>
public sealed class TrackChangesFeature : DocumentEditorFeatureBase
{
    /// <summary>Initializes a new track changes feature.</summary>
    public TrackChangesFeature() : base(DocumentEditorFeatureNames.TrackChanges) { }
}

/// <summary>Headers and footers feature skeleton.</summary>
public sealed class HeadersFootersFeature : DocumentEditorFeatureBase
{
    /// <summary>Initializes a new headers and footers feature.</summary>
    public HeadersFootersFeature() : base(DocumentEditorFeatureNames.HeadersFooters) { }
}

/// <summary>Import/export feature skeleton.</summary>
public sealed class ImportExportFeature : DocumentEditorFeatureBase
{
    /// <summary>Initializes a new import/export feature.</summary>
    public ImportExportFeature() : base(DocumentEditorFeatureNames.ImportExport) { }
}

/// <summary>Restricted editing feature skeleton.</summary>
public sealed class RestrictedEditingFeature : DocumentEditorFeatureBase
{
    /// <summary>Initializes a new restricted editing feature.</summary>
    public RestrictedEditingFeature() : base(DocumentEditorFeatureNames.RestrictedEditing) { }
}

/// <summary>Offline collaboration feature skeleton.</summary>
public sealed class OfflineCollaborationFeature : DocumentEditorFeatureBase
{
    /// <summary>Initializes a new offline collaboration feature.</summary>
    public OfflineCollaborationFeature() : base(DocumentEditorFeatureNames.OfflineCollaboration) { }
}
