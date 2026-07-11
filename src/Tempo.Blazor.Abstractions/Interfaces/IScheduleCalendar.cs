using Tempo.Blazor.Models;

namespace Tempo.Blazor.Interfaces;

/// <summary>Serializes scheduler events into a calendar document (for example iCalendar / ICS).</summary>
public interface IScheduleExporter
{
    /// <summary>MIME content type of the produced document.</summary>
    string ContentType { get; }

    /// <summary>File extension (without the dot), for example <c>ics</c>.</summary>
    string FileExtension { get; }

    /// <summary>Serializes events into a calendar document.</summary>
    /// <param name="events">Events to export.</param>
    /// <param name="calendarName">Optional calendar display name.</param>
    string Export(IEnumerable<TmScheduleEvent> events, string? calendarName = null);
}

/// <summary>Parses a calendar document (for example iCalendar / ICS) into scheduler events.</summary>
public interface IScheduleImporter
{
    /// <summary>Parses a calendar document into events.</summary>
    /// <param name="content">Calendar document text.</param>
    IReadOnlyList<TmScheduleEvent> Import(string content);
}
