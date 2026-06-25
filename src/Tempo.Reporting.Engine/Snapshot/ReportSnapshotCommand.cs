using System.Text.Json.Serialization;

namespace Tempo.Reporting.Engine.Snapshot;

/// <summary>Single absolute drawing primitive in a report snapshot page.</summary>
public sealed class ReportSnapshotCommand
{
    /// <summary>Stable command identifier.</summary>
    [JsonPropertyOrder(0)]
    public string Id { get; init; } = string.Empty;

    /// <summary>Primitive command kind.</summary>
    [JsonPropertyOrder(1)]
    public ReportSnapshotCommandType Type { get; init; }

    /// <summary>X coordinate in CSS pixels.</summary>
    [JsonPropertyOrder(2)]
    public double X { get; init; }

    /// <summary>Y coordinate in CSS pixels.</summary>
    [JsonPropertyOrder(3)]
    public double Y { get; init; }

    /// <summary>Command width in CSS pixels.</summary>
    [JsonPropertyOrder(4)]
    public double Width { get; init; }

    /// <summary>Command height in CSS pixels.</summary>
    [JsonPropertyOrder(5)]
    public double Height { get; init; }

    /// <summary>Optional text content.</summary>
    [JsonPropertyOrder(6)]
    public string? Text { get; init; }

    /// <summary>Optional text baseline in CSS pixels.</summary>
    [JsonPropertyOrder(7)]
    public double? Baseline { get; init; }

    /// <summary>Optional font family.</summary>
    [JsonPropertyOrder(8)]
    public string? FontFamily { get; init; }

    /// <summary>Optional font size in CSS pixels.</summary>
    [JsonPropertyOrder(9)]
    public double? FontSize { get; init; }

    /// <summary>Optional CSS font weight.</summary>
    [JsonPropertyOrder(10)]
    public string? FontWeight { get; init; }

    /// <summary>Optional CSS font style.</summary>
    [JsonPropertyOrder(11)]
    public string? FontStyle { get; init; }

    /// <summary>Optional letter spacing in CSS pixels.</summary>
    [JsonPropertyOrder(12)]
    public double LetterSpacing { get; init; }

    /// <summary>Optional fill color.</summary>
    [JsonPropertyOrder(13)]
    public string? Fill { get; init; }

    /// <summary>Optional stroke color.</summary>
    [JsonPropertyOrder(14)]
    public string? Stroke { get; init; }

    /// <summary>Optional stroke width.</summary>
    [JsonPropertyOrder(15)]
    public double StrokeWidth { get; init; }

    /// <summary>Optional SVG-like path data for vector commands.</summary>
    [JsonPropertyOrder(16)]
    public string? PathData { get; init; }

    /// <summary>Optional image source URL or data URI.</summary>
    [JsonPropertyOrder(17)]
    public string? Source { get; init; }

    /// <summary>Whether this text command is underlined.</summary>
    [JsonPropertyOrder(18)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Underline { get; init; }

    /// <summary>Whether this text command has strike-through decoration.</summary>
    [JsonPropertyOrder(19)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool StrikeThrough { get; init; }

    /// <summary>Optional text highlight color.</summary>
    [JsonPropertyOrder(20)]
    public string? Highlight { get; init; }

    /// <summary>Optional clockwise text rotation in degrees around the text origin.</summary>
    [JsonPropertyOrder(21)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Rotation { get; init; }

    /// <summary>Creates a text command.</summary>
    public static ReportSnapshotCommand TextRun(
        string id,
        string text,
        double x,
        double baseline,
        double width,
        double height,
        string fontFamily,
        double fontSize,
        string fill,
        string fontWeight = "400",
        string fontStyle = "normal",
        double letterSpacing = 0,
        bool underline = false,
        bool strikeThrough = false,
        string? highlight = null,
        double rotation = 0)
        => new()
        {
            Id = id,
            Type = ReportSnapshotCommandType.TextRun,
            X = x,
            Y = baseline - height,
            Width = width,
            Height = height,
            Text = text,
            Baseline = baseline,
            FontFamily = fontFamily,
            FontSize = fontSize,
            FontWeight = fontWeight,
            FontStyle = fontStyle,
            LetterSpacing = letterSpacing,
            Fill = fill,
            Underline = underline,
            StrikeThrough = strikeThrough,
            Highlight = highlight,
            Rotation = rotation
        };

    /// <summary>Creates a rectangle command.</summary>
    public static ReportSnapshotCommand Rectangle(
        string id,
        double x,
        double y,
        double width,
        double height,
        string fill,
        string? stroke = null,
        double strokeWidth = 0)
        => new()
        {
            Id = id,
            Type = ReportSnapshotCommandType.Rectangle,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Fill = fill,
            Stroke = stroke,
            StrokeWidth = strokeWidth
        };

    /// <summary>Creates a line command.</summary>
    public static ReportSnapshotCommand Line(string id, double x, double y, double width, double height, string stroke, double strokeWidth)
        => new()
        {
            Id = id,
            Type = ReportSnapshotCommandType.Line,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Stroke = stroke,
            StrokeWidth = strokeWidth
        };

    /// <summary>Creates a vector path command.</summary>
    public static ReportSnapshotCommand Path(
        string id,
        string pathData,
        double x,
        double y,
        double width,
        double height,
        string? fill = null,
        string? stroke = null,
        double strokeWidth = 0)
        => new()
        {
            Id = id,
            Type = ReportSnapshotCommandType.Path,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Fill = fill,
            Stroke = stroke,
            StrokeWidth = strokeWidth,
            PathData = pathData
        };

    /// <summary>Creates an image command.</summary>
    public static ReportSnapshotCommand Image(string id, double x, double y, double width, double height, string source)
        => new()
        {
            Id = id,
            Type = ReportSnapshotCommandType.Image,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Source = source
        };

    /// <summary>Creates a rectangular clip-push command.</summary>
    public static ReportSnapshotCommand ClipPush(string id, double x, double y, double width, double height)
        => new()
        {
            Id = id,
            Type = ReportSnapshotCommandType.ClipPush,
            X = x,
            Y = y,
            Width = width,
            Height = height
        };

    /// <summary>Creates a clip-pop command.</summary>
    public static ReportSnapshotCommand ClipPop(string id)
        => new()
        {
            Id = id,
            Type = ReportSnapshotCommandType.ClipPop
        };
}
