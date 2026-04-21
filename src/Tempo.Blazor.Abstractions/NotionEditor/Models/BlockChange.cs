namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;

public record BlockChange(Guid BlockId, BlockChangeType ChangeType, IPageBlock? Block, string UserId);
