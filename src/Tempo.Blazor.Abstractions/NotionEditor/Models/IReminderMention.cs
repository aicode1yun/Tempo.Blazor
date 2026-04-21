namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Enums;

public interface IReminderMention : IDateMention
{
    ReminderTiming Timing { get; }
}
