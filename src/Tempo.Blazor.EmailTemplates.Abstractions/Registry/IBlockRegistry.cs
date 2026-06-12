using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Registry;

/// <summary>Catalogue of block kinds available to the editor, with factories to create them.</summary>
public interface IBlockRegistry
{
    /// <summary>Gets all registered block descriptors (built-in and custom).</summary>
    IReadOnlyList<BlockDescriptor> GetAll();

    /// <summary>Creates a new instance of the given built-in block type with default attributes.</summary>
    EmailBlockBase CreateInstance(BlockType type);

    /// <summary>Creates a new block from the descriptor with the given id.</summary>
    /// <exception cref="ArgumentException">No descriptor with that id is registered.</exception>
    EmailBlockBase CreateById(string id);

    /// <summary>Registers an additional, externally defined block descriptor.</summary>
    void RegisterCustom(BlockDescriptor descriptor);
}
