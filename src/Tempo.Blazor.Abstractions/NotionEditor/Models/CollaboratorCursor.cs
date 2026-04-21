namespace Tempo.Blazor.NotionEditor.Models;

public record CollaboratorCursor(string UserId, string DisplayName, string? AvatarUrl, string Color, Guid BlockId, int Offset);
