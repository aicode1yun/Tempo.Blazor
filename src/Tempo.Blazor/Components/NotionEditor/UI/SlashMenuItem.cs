using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.Components.NotionEditor.UI;

public enum SlashMenuCategory
{
    Recent   = 0,
    Basic    = 1,
    Media    = 2,
    Embeds   = 3,
    Page     = 4,
    Advanced = 5
}

public enum SlashMenuAction
{
    ConvertBlock = 0,
    InsertStatus = 1
}

public sealed record SlashMenuItem(
    BlockType         Type,
    string            Name,
    string            Description,
    string            SvgIcon,
    SlashMenuCategory Category,
    string[]          Keywords,
    SlashMenuAction   Action = SlashMenuAction.ConvertBlock,
    CalloutVariant?   CalloutVariant = null
);
