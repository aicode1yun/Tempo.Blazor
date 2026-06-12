namespace Tempo.Blazor.EmailTemplates.Abstractions;

/// <summary>
/// Marker type identifying the <c>Tempo.Blazor.EmailTemplates.Abstractions</c> assembly.
/// Used as the assembly anchor for FluentValidation validator scanning and for
/// <see cref="System.Resources.ResourceManager"/> / localization resource resolution.
/// </summary>
public static class EmailTemplatesAbstractions
{
    /// <summary>
    /// Gets the assembly that contains the email template engine, model and contracts.
    /// </summary>
    public static System.Reflection.Assembly Assembly => typeof(EmailTemplatesAbstractions).Assembly;
}
