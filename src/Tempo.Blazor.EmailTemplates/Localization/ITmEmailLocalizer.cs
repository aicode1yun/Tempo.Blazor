namespace Tempo.Blazor.EmailTemplates.Localization;

/// <summary>
/// Localization accessor for the email template editor UI. Backed by
/// <c>IStringLocalizer&lt;TmEmailResources&gt;</c>; hosts may replace it to fully control strings.
/// </summary>
public interface ITmEmailLocalizer
{
    /// <summary>Gets the localized string for the given resource key.</summary>
    string this[string key] { get; }

    /// <summary>Gets the localized, formatted string for the given resource key and arguments.</summary>
    string this[string key, params object[] arguments] { get; }
}
