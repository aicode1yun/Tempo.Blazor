namespace Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

/// <summary>
/// Implemented by anything that directly holds a list of content blocks (a column or a hero).
/// Used by the document tree operations to find, add, move and remove blocks uniformly.
/// </summary>
public interface IBlockContainer
{
    /// <summary>Gets the identifier of the container.</summary>
    Guid Id { get; }

    /// <summary>Gets the blocks held directly by this container.</summary>
    IList<EmailBlockBase> Blocks { get; }
}
