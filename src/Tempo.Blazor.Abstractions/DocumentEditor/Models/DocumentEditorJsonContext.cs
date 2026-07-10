using System.Text.Json.Serialization;

namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>
/// Source-generated serializer metadata for the document editor persistence model and its provider
/// contracts (perf plan N10). Wired into <see cref="DocumentEditorJson.Options"/> as the first link
/// of the resolver chain so document clones, saves, and interop marshals avoid reflection-based
/// metadata construction; a reflection resolver stays chained behind it as a safe fallback for
/// payloads not rooted here (generic helper clones, anonymous debug payloads).
/// Polymorphic <c>$type</c> hierarchies (<see cref="DocumentBlockContent"/>, <see cref="InlineContent"/>)
/// are supported by the source generator since .NET 8; the generated metadata preserves the exact
/// wire format of the reflection serializer.
/// </summary>
// GenerationMode = Metadata (no fast-path): fast-path serialize handlers bind to the context's own
// options and bypass the runtime resolver chain, which breaks `object`-typed members whose runtime
// values are JsonElement (style format bags such as DocumentStyleDefinition.ParagraphFormat).
// Metadata mode always routes nested lookups through the runtime options, so the chained
// reflection fallback keeps handling them.
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(DocumentEditorDocument))]
[JsonSerializable(typeof(DocumentEditorLoadResult))]
[JsonSerializable(typeof(DocumentEditorSaveRequest))]
[JsonSerializable(typeof(DocumentEditorSaveResult))]
[JsonSerializable(typeof(DocumentOfflineDraft))]
[JsonSerializable(typeof(DocumentComment))]
[JsonSerializable(typeof(List<DocumentComment>))]
[JsonSerializable(typeof(DocumentRevision))]
[JsonSerializable(typeof(List<DocumentRevision>))]
[JsonSerializable(typeof(DocumentVersion))]
[JsonSerializable(typeof(List<DocumentVersion>))]
[JsonSerializable(typeof(WysiwygPatch))]
[JsonSerializable(typeof(WysiwygSelectionSnapshot))]
[JsonSerializable(typeof(DocumentOperation))]
[JsonSerializable(typeof(DocumentOperationBatch))]
[JsonSerializable(typeof(List<DocumentOperationBatch>))]
[JsonSerializable(typeof(DocumentEditorAuthor))]
[JsonSerializable(typeof(DocumentSigningFieldDescriptor))]
[JsonSerializable(typeof(List<DocumentSigningFieldDescriptor>))]
[JsonSerializable(typeof(List<DocumentBlock>))]
[JsonSerializable(typeof(DocumentParagraphProperties))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
public partial class DocumentEditorJsonContext : JsonSerializerContext
{
}
