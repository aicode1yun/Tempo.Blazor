using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Model;

/// <summary>
/// Traversal helpers over the nested document structure (sections → columns → blocks, recursing into
/// hero, group and wrapper containers) plus identifier reassignment used by clone-with-new-ids.
/// </summary>
public static class DocumentTree
{
    /// <summary>Enumerates every section, including those nested inside wrapper blocks.</summary>
    public static IEnumerable<EmailSection> AllSections(EmailTemplateDocument document)
    {
        foreach (var section in document.Sections)
            foreach (var s in SectionAndDescendants(section))
                yield return s;
    }

    /// <summary>Enumerates every column anywhere in the document, recursing into all containers.</summary>
    public static IEnumerable<EmailColumn> AllColumns(EmailTemplateDocument document)
    {
        foreach (var section in document.Sections)
            foreach (var c in ColumnsInSection(section))
                yield return c;
    }

    /// <summary>Enumerates every block list (a column's or a hero's <c>Blocks</c>) in the document.</summary>
    public static IEnumerable<IList<EmailBlockBase>> AllBlockLists(EmailTemplateDocument document)
    {
        var acc = new List<IList<EmailBlockBase>>();
        foreach (var section in document.Sections)
            foreach (var column in section.Columns)
                CollectBlockLists(column.Blocks, acc);
        return acc;
    }

    /// <summary>Enumerates every block anywhere in the document.</summary>
    public static IEnumerable<EmailBlockBase> AllBlocks(EmailTemplateDocument document)
        => AllBlockLists(document).SelectMany(list => list);

    /// <summary>Assigns a fresh identifier to a block and to every node nested beneath it.</summary>
    public static void ReassignIds(EmailBlockBase block)
    {
        block.Id = Guid.NewGuid();
        switch (block)
        {
            case EmailHeroBlock hero:
                foreach (var b in hero.Blocks) ReassignIds(b);
                break;
            case EmailGroupBlock group:
                foreach (var column in group.Columns) ReassignIds(column);
                break;
            case EmailWrapperBlock wrapper:
                foreach (var section in wrapper.Sections) ReassignIds(section);
                break;
        }
    }

    /// <summary>Assigns a fresh identifier to a section and everything beneath it.</summary>
    public static void ReassignIds(EmailSection section)
    {
        section.Id = Guid.NewGuid();
        foreach (var column in section.Columns) ReassignIds(column);
    }

    /// <summary>Assigns a fresh identifier to a column and every block beneath it.</summary>
    public static void ReassignIds(EmailColumn column)
    {
        column.Id = Guid.NewGuid();
        foreach (var block in column.Blocks) ReassignIds(block);
    }

    private static IEnumerable<EmailSection> SectionAndDescendants(EmailSection section)
    {
        yield return section;
        foreach (var column in section.Columns)
            foreach (var block in column.Blocks)
                if (block is EmailWrapperBlock wrapper)
                    foreach (var nested in wrapper.Sections)
                        foreach (var s in SectionAndDescendants(nested))
                            yield return s;
    }

    private static IEnumerable<EmailColumn> ColumnsInSection(EmailSection section)
    {
        foreach (var column in section.Columns)
        {
            yield return column;
            foreach (var c in ColumnsInBlocks(column.Blocks))
                yield return c;
        }
    }

    private static IEnumerable<EmailColumn> ColumnsInBlocks(IEnumerable<EmailBlockBase> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case EmailGroupBlock group:
                    foreach (var column in group.Columns)
                    {
                        yield return column;
                        foreach (var c in ColumnsInBlocks(column.Blocks)) yield return c;
                    }
                    break;
                case EmailWrapperBlock wrapper:
                    foreach (var section in wrapper.Sections)
                        foreach (var c in ColumnsInSection(section)) yield return c;
                    break;
                case EmailHeroBlock hero:
                    foreach (var c in ColumnsInBlocks(hero.Blocks)) yield return c;
                    break;
            }
        }
    }

    private static void CollectBlockLists(IList<EmailBlockBase> list, IList<IList<EmailBlockBase>> acc)
    {
        acc.Add(list);
        foreach (var block in list)
        {
            switch (block)
            {
                case EmailHeroBlock hero:
                    CollectBlockLists(hero.Blocks, acc);
                    break;
                case EmailGroupBlock group:
                    foreach (var column in group.Columns) CollectBlockLists(column.Blocks, acc);
                    break;
                case EmailWrapperBlock wrapper:
                    foreach (var section in wrapper.Sections)
                        foreach (var column in section.Columns) CollectBlockLists(column.Blocks, acc);
                    break;
            }
        }
    }
}
