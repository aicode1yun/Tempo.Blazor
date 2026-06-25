using System.Text.Json;
using System.Text.Json.Serialization;
using Tempo.Reporting.Engine.Snapshot;

namespace Tempo.Blazor.Reporting.Interop;

/// <summary>JSON options shared by report viewer interop and HTTP sources.</summary>
public static class ReportViewerJson
{
    /// <summary>Viewer JSON options.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>Serializes a snapshot for the JavaScript painter.</summary>
    public static string SerializeSnapshot(ReportSnapshot snapshot)
        => JsonSerializer.Serialize(snapshot, Options);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter<ReportSnapshotCommandType>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new ReportParameterValueJsonConverter());
        return options;
    }
}
