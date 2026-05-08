using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Models;

/// <summary>Represents a node in a nested comment thread tree.</summary>
public class CommentThreadNode
{
    public INotionCommentEntry Entry { get; set; } = default!;
    public List<CommentThreadNode> Children { get; set; } = new();
    public int Level { get; set; }
}

/// <summary>Builds a tree from a flat list of comment entries using <see cref="INotionCommentEntry.ParentEntryId"/>.</summary>
public static class CommentThreadHelper
{
    public static List<CommentThreadNode> BuildTree(IReadOnlyList<INotionCommentEntry> entries, int maxDepth = 5)
    {
        var lookup = entries.ToLookup(e => e.ParentEntryId);

        List<CommentThreadNode> Build(Guid? parentId, int level)
        {
            if (level > maxDepth) return new List<CommentThreadNode>();
            return lookup[parentId]
                .Select(e => new CommentThreadNode
                {
                    Entry = e,
                    Level = level,
                    Children = Build(e.Id, level + 1)
                })
                .ToList();
        }

        return Build(null, 0);
    }
}
