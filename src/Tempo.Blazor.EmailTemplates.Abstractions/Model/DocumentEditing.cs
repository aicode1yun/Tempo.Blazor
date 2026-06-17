using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;
using Tempo.Blazor.EmailTemplates.Abstractions.Serialization;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Model;

/// <summary>
/// Structural edit operations over an <see cref="EmailTemplateDocument"/>: finding, adding, moving,
/// removing and duplicating blocks, sections and columns. All operations work across nested
/// hero/group/wrapper containers via <see cref="DocumentTree"/>.
/// </summary>
public static class DocumentEditing
{
    // ── Blocks ────────────────────────────────────────────────────────────────────────────────

    /// <summary>Finds a block anywhere in the document by id, or returns <see langword="null"/>.</summary>
    public static EmailBlockBase? FindBlock(this EmailTemplateDocument document, Guid blockId)
        => DocumentTree.AllBlocks(document).FirstOrDefault(b => b.Id == blockId);

    /// <summary>
    /// Returns the column that directly contains the given block, or <see langword="null"/> when the
    /// block is held by a non-column container (e.g. a hero) or does not exist.
    /// </summary>
    public static EmailColumn? FindParentColumn(this EmailTemplateDocument document, Guid blockId)
        => DocumentTree.AllColumns(document).FirstOrDefault(c => c.Blocks.Any(b => b.Id == blockId));

    /// <summary>Removes a block from wherever it lives in the tree. Returns whether it was found.</summary>
    public static bool RemoveBlock(this EmailTemplateDocument document, Guid blockId)
    {
        foreach (var list in DocumentTree.AllBlockLists(document))
            if (RemoveFirst(list, b => b.Id == blockId))
                return true;
        return false;
    }

    /// <summary>
    /// Inserts a block into the column with the given id at <paramref name="index"/> (clamped).
    /// Returns <see langword="false"/> if no such column exists.
    /// </summary>
    public static bool AddBlock(this EmailTemplateDocument document, Guid columnId, EmailBlockBase block, int index)
    {
        var column = DocumentTree.AllColumns(document).FirstOrDefault(c => c.Id == columnId);
        if (column is null) return false;
        column.Blocks.Insert(Clamp(index, column.Blocks.Count), block);
        return true;
    }

    /// <summary>
    /// Moves a block to the target column at <paramref name="targetIndex"/> (clamped, evaluated after
    /// removal). Returns <see langword="false"/> if the block or target column is missing.
    /// </summary>
    public static bool MoveBlock(this EmailTemplateDocument document, Guid blockId, Guid targetColumnId, int targetIndex)
    {
        var target = DocumentTree.AllColumns(document).FirstOrDefault(c => c.Id == targetColumnId);
        var block = document.FindBlock(blockId);
        if (target is null || block is null) return false;

        document.RemoveBlock(blockId);
        target.Blocks.Insert(Clamp(targetIndex, target.Blocks.Count), block);
        return true;
    }

    /// <summary>
    /// Inserts a deep copy (with fresh ids) of the block immediately after the original, in the same
    /// container. Returns the copy, or <see langword="null"/> if the block was not found.
    /// </summary>
    public static EmailBlockBase? DuplicateBlock(this EmailTemplateDocument document, Guid blockId)
    {
        foreach (var list in DocumentTree.AllBlockLists(document))
        {
            var index = IndexOf(list, b => b.Id == blockId);
            if (index >= 0)
            {
                var copy = list[index].CloneWithNewIds();
                list.Insert(index + 1, copy);
                return copy;
            }
        }
        return null;
    }

    // ── Sections ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Inserts a top-level section at <paramref name="index"/> (clamped).</summary>
    public static void AddSection(this EmailTemplateDocument document, EmailSection section, int index)
        => document.Sections.Insert(Clamp(index, document.Sections.Count), section);

    /// <summary>Removes a top-level section by id. Returns whether it was found.</summary>
    public static bool RemoveSection(this EmailTemplateDocument document, Guid sectionId)
        => RemoveFirst(document.Sections, s => s.Id == sectionId);

    /// <summary>Moves a top-level section to <paramref name="index"/> (clamped after removal).</summary>
    public static bool MoveSection(this EmailTemplateDocument document, Guid sectionId, int index)
    {
        var section = document.Sections.FirstOrDefault(s => s.Id == sectionId);
        if (section is null) return false;
        document.Sections.Remove(section);
        document.Sections.Insert(Clamp(index, document.Sections.Count), section);
        return true;
    }

    /// <summary>
    /// Inserts a deep copy (with fresh ids) of a top-level section immediately after the original.
    /// Returns the copy, or <see langword="null"/> if the section was not found.
    /// </summary>
    public static EmailSection? DuplicateSection(this EmailTemplateDocument document, Guid sectionId)
    {
        var index = IndexOf(document.Sections, s => s.Id == sectionId);
        if (index < 0) return null;

        var copy = EmailTemplateSerializer.Clone(document.Sections[index]);
        DocumentTree.ReassignIds(copy);
        document.Sections.Insert(index + 1, copy);
        return copy;
    }

    // ── Columns ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Adds a column to the section and rebalances all columns to equal widths.</summary>
    public static void AddColumn(this EmailSection section, EmailColumn column)
    {
        section.Columns.Add(column);
        RebalanceColumns(section);
    }

    /// <summary>Removes a column by id and rebalances the remaining columns. Returns whether found.</summary>
    public static bool RemoveColumn(this EmailSection section, Guid columnId)
    {
        if (!RemoveFirst(section.Columns, c => c.Id == columnId)) return false;
        RebalanceColumns(section);
        return true;
    }

    /// <summary>Sets every column in the section to an equal share of the width (summing to 100%).</summary>
    public static void RebalanceColumns(EmailSection section)
    {
        var widths = LayoutMath.EqualWidths(section.Columns.Count);
        for (int i = 0; i < section.Columns.Count; i++)
            section.Columns[i].Width = widths[i];
    }

    private static int Clamp(int index, int count) => index < 0 ? 0 : index > count ? count : index;

    private static int IndexOf<T>(IList<T> items, Predicate<T> predicate)
    {
        for (var i = 0; i < items.Count; i++)
            if (predicate(items[i]))
                return i;

        return -1;
    }

    private static bool RemoveFirst<T>(IList<T> items, Predicate<T> predicate)
    {
        var index = IndexOf(items, predicate);
        if (index < 0) return false;

        items.RemoveAt(index);
        return true;
    }
}
