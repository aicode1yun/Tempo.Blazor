namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Event arguments sent by the JS engine when it finishes initialization.</summary>
public sealed class WysiwygEngineReadyEventArgs
{
    /// <summary>Instance identifier assigned by the JS engine.</summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>Protocol version supported by the JS engine.</summary>
    public int ProtocolVersion { get; set; } = 1;
}
