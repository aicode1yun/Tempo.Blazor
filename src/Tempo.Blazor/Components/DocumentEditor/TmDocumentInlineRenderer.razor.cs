using Microsoft.AspNetCore.Components;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor;

/// <summary>Renders a single inline content run from the document editor model.</summary>
public partial class TmDocumentInlineRenderer : ComponentBase
{
    /// <summary>Inline content to render.</summary>
    [Parameter] public InlineContent? Inline { get; set; }

    /// <summary>Raised when a comment anchor inline is selected.</summary>
    [Parameter] public EventCallback<string> OnCommentSelected { get; set; }

    private string? SafeHref => GetSafeHref();

    private string? LinkTitle => Inline?.Marks.FirstOrDefault(mark => mark.Type == InlineMarkType.Link)?.Link?.Title;

    private string? TokenKey => Inline is TokenRun token ? token.Key : null;

    private string? TokenType => Inline is TokenRun token ? token.TokenType : null;

    private string? CommentId => Inline?.Marks.FirstOrDefault(mark => mark.Type == InlineMarkType.CommentAnchor)
        ?.CommentAnchor?.CommentId;

    private string? InlineTestId => Inline is TokenRun
        ? "document-token-chip"
        : string.IsNullOrWhiteSpace(CommentId) ? null : "document-comment-highlight";

    private string? InlineTitle => Inline is TokenRun token
        ? string.IsNullOrWhiteSpace(token.Description) ? token.Key : token.Description
        : LinkTitle;

    private string InlineClass
    {
        get
        {
            var classes = new List<string> { "tm-document-inline" };
            if (Inline is TokenRun)
            {
                classes.Add("tm-document-inline--token");
                if (Inline is TokenRun token && !string.IsNullOrWhiteSpace(token.ColorClass))
                {
                    classes.Add(token.ColorClass!);
                }
            }

            if (Inline is DocumentNoteReferenceRun)
            {
                classes.Add("tm-document-inline--note-reference");
            }

            foreach (var mark in Inline?.Marks ?? [])
            {
                classes.Add(mark.Type switch
                {
                    InlineMarkType.Bold => "tm-document-inline--bold",
                    InlineMarkType.Italic => "tm-document-inline--italic",
                    InlineMarkType.Underline => "tm-document-inline--underline",
                    InlineMarkType.Strikethrough => "tm-document-inline--strikethrough",
                    InlineMarkType.Superscript => "tm-document-inline--superscript",
                    InlineMarkType.Subscript => "tm-document-inline--subscript",
                    InlineMarkType.Link => "tm-document-inline--link",
                    InlineMarkType.CommentAnchor => "tm-document-inline--comment-anchor",
                    InlineMarkType.Revision => "tm-document-inline--revision",
                    InlineMarkType.Highlight => "tm-document-inline--highlight",
                    InlineMarkType.TextColor => "tm-document-inline--text-color",
                    _ => string.Empty
                });
            }

            return string.Join(" ", classes.Where(c => !string.IsNullOrWhiteSpace(c)));
        }
    }

    private string? InlineStyle
    {
        get
        {
            var styles = new List<string>();
            foreach (var mark in Inline?.Marks ?? [])
            {
                if (string.IsNullOrWhiteSpace(mark.Value) || !IsSafeCssColor(mark.Value))
                {
                    continue;
                }

                if (mark.Type == InlineMarkType.Highlight)
                {
                    styles.Add($"background-color: {mark.Value}");
                }
                else if (mark.Type == InlineMarkType.TextColor)
                {
                    styles.Add($"color: {mark.Value}");
                }
            }

            return styles.Count == 0 ? null : string.Join("; ", styles);
        }
    }

    private string GetText()
    {
        return Inline switch
        {
            TextRun text => text.Text,
            TokenRun token => string.IsNullOrWhiteSpace(token.DisplayName) ? token.Key : token.DisplayName,
            DocumentNoteReferenceRun note => string.IsNullOrWhiteSpace(note.DisplayMarker) ? note.NoteId : note.DisplayMarker!,
            _ => string.Empty
        };
    }

    private string? GetSafeHref()
    {
        var href = Inline?.Marks.FirstOrDefault(mark => mark.Type == InlineMarkType.Link)?.Link?.Href;
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        return IsSafeLinkUrl(href) ? href : null;
    }

    internal static bool IsSafeLinkUrl(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        if (href.StartsWith("/", StringComparison.Ordinal) || href.StartsWith("#", StringComparison.Ordinal))
        {
            return true;
        }

        return Uri.TryCreate(href, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps
                || uri.Scheme == Uri.UriSchemeHttp
                || uri.Scheme == Uri.UriSchemeMailto
                || uri.Scheme == "tel");
    }

    private static bool IsSafeCssColor(string value)
    {
        if (value.StartsWith("#", StringComparison.Ordinal)
            && (value.Length == 4 || value.Length == 7)
            && value[1..].All(c => Uri.IsHexDigit(c)))
        {
            return true;
        }

        return value.All(char.IsLetter);
    }

    private Task SelectCommentAsync()
    {
        return string.IsNullOrWhiteSpace(CommentId)
            ? Task.CompletedTask
            : OnCommentSelected.InvokeAsync(CommentId);
    }
}
