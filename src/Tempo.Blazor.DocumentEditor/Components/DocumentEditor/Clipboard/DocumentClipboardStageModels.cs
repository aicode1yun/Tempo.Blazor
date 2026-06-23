using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Components.DocumentEditor.Clipboard;

/// <summary>Raw clipboard payload captured before source detection and normalization.</summary>
public sealed class DocumentClipboardRawInput
{
    /// <summary>HTML payload, if provided by the browser.</summary>
    public string? Html { get; init; }

    /// <summary>Plain text payload, if provided by the browser.</summary>
    public string? PlainText { get; init; }

    /// <summary>Clipboard file metadata, usually image file names or MIME summaries.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>Browser-reported MIME types.</summary>
    public IReadOnlyList<string> MimeTypes { get; init; } = [];

    /// <summary>Optional custom metadata copied from an internal Tempo clipboard payload.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

/// <summary>Detected clipboard source and confidence score.</summary>
public sealed class DocumentClipboardSourceDetectionResult
{
    /// <summary>Detected source application or fallback kind.</summary>
    public DocumentClipboardSource Source { get; init; } = DocumentClipboardSource.Unknown;

    /// <summary>Confidence score from 0.0 to 1.0.</summary>
    public double Confidence { get; init; }

    /// <summary>Machine-readable detector reason.</summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>Sanitized or source-normalized HTML ready for block conversion.</summary>
public sealed class DocumentClipboardNormalizedHtml
{
    /// <summary>Normalized HTML fragment.</summary>
    public string Html { get; init; } = string.Empty;

    /// <summary>Detected source for the HTML.</summary>
    public DocumentClipboardSource Source { get; init; } = DocumentClipboardSource.Unknown;

    /// <summary>Non-fatal normalization warnings.</summary>
    public IReadOnlyList<DocumentClipboardWarning> Warnings { get; init; } = [];
}

/// <summary>Intermediate clipboard fragment after conversion to Tempo document blocks.</summary>
public sealed class DocumentClipboardFragment
{
    /// <summary>Converted blocks.</summary>
    public IReadOnlyList<DocumentBlock> Blocks { get; init; } = [];

    /// <summary>Detected source for the fragment.</summary>
    public DocumentClipboardSource Source { get; init; } = DocumentClipboardSource.Unknown;

    /// <summary>Accumulated conversion warnings.</summary>
    public IReadOnlyList<DocumentClipboardWarning> Warnings { get; init; } = [];
}

/// <summary>Final paste pipeline result after schema policy normalization.</summary>
public sealed class DocumentClipboardInsertionResult
{
    /// <summary>Blocks that can be inserted into the runtime editor.</summary>
    public IReadOnlyList<DocumentBlock> Blocks { get; init; } = [];

    /// <summary>Detected source for the paste operation.</summary>
    public DocumentClipboardSource Source { get; init; } = DocumentClipboardSource.Unknown;

    /// <summary>Warnings from detection, normalization, conversion, and insertion policy.</summary>
    public IReadOnlyList<DocumentClipboardWarning> Warnings { get; init; } = [];

    /// <summary>Schema region where the blocks were prepared for insertion.</summary>
    public DocumentEditorRegion Region { get; init; } = DocumentEditorRegion.Body;

    /// <summary>Creates an insertion result from a clipboard output and policy result.</summary>
    public static DocumentClipboardInsertionResult FromPolicy(
        DocumentClipboardOutput output,
        DocumentInsertionPolicyResult policy,
        DocumentEditorRegion region)
    {
        var warnings = output.Warnings
            .Concat(policy.Warnings.Select(w => new DocumentClipboardWarning
            {
                Code = w.Code,
                Message = w.Message
            }))
            .ToList();

        return new DocumentClipboardInsertionResult
        {
            Blocks = policy.Blocks,
            Source = output.Source,
            Warnings = warnings,
            Region = region
        };
    }
}
