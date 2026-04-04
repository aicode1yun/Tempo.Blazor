namespace Tempo.Blazor.Interfaces;

/// <summary>
/// Represents a token/variable that can be inserted into TmRichEditor via {{ trigger.
/// </summary>
public interface IToken
{
    /// <summary>Unique key for the token (e.g. "user.email", "company.name").</summary>
    string Key { get; }

    /// <summary>Display name shown in the autocomplete list and in the editor chip.</summary>
    string DisplayName { get; }

    /// <summary>Optional description shown in the autocomplete list.</summary>
    string? Description { get; }

    /// <summary>Optional category for grouping tokens (e.g. "User", "System").</summary>
    string? Category { get; }

    /// <summary>Optional icon shown next to the token in the dropdown and hover preview. Can be an emoji (e.g. "🔒") or a CSS icon class.</summary>
    string? Icon { get; }

    /// <summary>Optional CSS class applied to the token chip in the editor for visual distinction (e.g. "token-secret", "token-url").</summary>
    string? ColorClass { get; }

    /// <summary>Optional short type label shown in the dropdown and hover preview (e.g. "Secret", "URL", "Number").</summary>
    string? TypeLabel { get; }
}
