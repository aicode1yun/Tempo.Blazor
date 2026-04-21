namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public interface IInlineMention
{
    InlineMentionType Type { get; }
    int TextOffset { get; }
}
