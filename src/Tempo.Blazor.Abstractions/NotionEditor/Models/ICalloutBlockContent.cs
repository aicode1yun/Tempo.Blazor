namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public interface ICalloutBlockContent : ITextBlockContent
{
    string? IconEmoji { get; }
    string? IconImageUrl { get; }
    CalloutVariant Variant { get; }
}
