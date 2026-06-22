namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Result returned by an <see cref="ITmAuthorizationProvider"/>.</summary>
public sealed class TmAuthorizationResult
{
    /// <summary>True when the requested action is allowed.</summary>
    public bool Allowed { get; set; }

    /// <summary>Optional machine-readable reason or policy code.</summary>
    public string? Reason { get; set; }

    /// <summary>Optional provider-specific metadata.</summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>Creates an allowed authorization result.</summary>
    /// <param name="reason">Optional reason or policy code.</param>
    public static TmAuthorizationResult Allow(string? reason = null)
        => new() { Allowed = true, Reason = reason };

    /// <summary>Creates a denied authorization result.</summary>
    /// <param name="reason">Optional reason or policy code.</param>
    public static TmAuthorizationResult Deny(string? reason = null)
        => new() { Allowed = false, Reason = reason };
}
