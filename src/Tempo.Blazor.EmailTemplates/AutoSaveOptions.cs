namespace Tempo.Blazor.EmailTemplates;

/// <summary>Configures the editor's debounced auto-save behaviour.</summary>
public sealed class AutoSaveOptions
{
    /// <summary>Gets or sets whether auto-save is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the debounce interval after the last change before auto-saving.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(2);
}
