namespace Tempo.Blazor.EmailTemplates.Abstractions.Layout;

/// <summary>Describes a layout preset for presentation in the toolbox.</summary>
/// <param name="Preset">The preset value.</param>
/// <param name="NameKey">The localization key for the preset's display name.</param>
/// <param name="Widths">The column widths the preset produces.</param>
public sealed record LayoutPresetDescriptor(LayoutPreset Preset, string NameKey, IReadOnlyList<string> Widths);
