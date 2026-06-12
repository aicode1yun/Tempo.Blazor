using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;
using Tempo.Blazor.EmailTemplates.Abstractions.Serialization;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Model;

/// <summary>Deep-copy helpers for documents and blocks (used by undo/redo and clipboard).</summary>
public static class CloneExtensions
{
    /// <summary>Creates an independent deep copy of the document. Identifiers are preserved.</summary>
    public static EmailTemplateDocument DeepClone(this EmailTemplateDocument document)
        => EmailTemplateSerializer.Deserialize(EmailTemplateSerializer.Serialize(document));

    /// <summary>Creates an independent deep copy of a block. Identifiers are preserved.</summary>
    public static EmailBlockBase Clone(this EmailBlockBase block)
        => EmailTemplateSerializer.CloneBlock(block);

    /// <summary>
    /// Creates an independent deep copy of a block with a fresh identifier on every node
    /// (used when pasting/duplicating so the copy does not collide with the original).
    /// </summary>
    public static EmailBlockBase CloneWithNewIds(this EmailBlockBase block)
    {
        var clone = block.Clone();
        DocumentTree.ReassignIds(clone);
        return clone;
    }
}
