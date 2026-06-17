using Tempo.Blazor.EmailTemplates.Abstractions.Layout;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

/// <summary>A single document validation finding (reuses <see cref="LayoutSeverity"/>).</summary>
/// <param name="Severity">How serious the finding is.</param>
/// <param name="Key">The localization key describing the finding.</param>
/// <param name="Path">A locator for the offending node (block/section identifier).</param>
public sealed record DocumentValidationMessage(LayoutSeverity Severity, string Key, string Path);
