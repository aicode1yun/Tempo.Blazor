using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Registry;

/// <summary>
/// Describes a block kind available in the editor toolbox: how to present it and how to create one.
/// </summary>
/// <param name="Id">Stable identifier (the JSON discriminator token for built-ins, or a custom id).</param>
/// <param name="Type">The underlying block type the factory produces.</param>
/// <param name="NameKey">Localization key for the toolbox display name.</param>
/// <param name="Icon">Icon token used by the toolbox.</param>
/// <param name="Category">Localization-friendly category id used to group the toolbox.</param>
/// <param name="Factory">Creates a new block instance with sensible defaults.</param>
public sealed record BlockDescriptor(
    string Id,
    BlockType Type,
    string NameKey,
    string Icon,
    string Category,
    Func<EmailBlockBase> Factory);
