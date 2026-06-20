namespace Tempo.Blazor.EmailTemplates.Abstractions.Templating;

/// <summary>
/// Sandbox limits applied when rendering user-authored templates, bounding loops, recursion, output
/// size and wall-clock time so a malicious or buggy template cannot hang or exhaust memory.
/// </summary>
public sealed class TemplateSecurityOptions
{
    /// <summary>Maximum total loop iterations before rendering aborts (default 5000).</summary>
    public int LoopLimit { get; set; } = 5000;

    /// <summary>Maximum recursion depth before rendering aborts (default 100).</summary>
    public int RecursiveLimit { get; set; } = 100;

    /// <summary>Maximum length of the rendered output in characters (default 5,000,000).</summary>
    public int MaxOutputLength { get; set; } = 5_000_000;

    /// <summary>Maximum wall-clock time for a single render (default 5 seconds, cooperative).</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// When <see langword="true"/>, referencing an undefined variable is an error; otherwise it
    /// renders as empty (the default, lenient behaviour).
    /// </summary>
    public bool StrictVariables { get; set; }
}
