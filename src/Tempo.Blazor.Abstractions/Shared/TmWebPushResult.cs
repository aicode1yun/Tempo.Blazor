namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Outcome of a single Web Push send attempt.</summary>
public sealed class TmWebPushResult
{
    /// <summary>Whether the push service accepted the message.</summary>
    public bool Success { get; set; }

    /// <summary>HTTP status code returned by the push service, when applicable.</summary>
    public int StatusCode { get; set; }

    /// <summary>Error detail when <see cref="Success"/> is false.</summary>
    public string? Error { get; set; }

    /// <summary>
    /// True when the subscription is gone (404/410) and the caller should remove it
    /// from the <see cref="Interfaces.IPushSubscriptionStore"/>.
    /// </summary>
    public bool IsExpired => StatusCode is 404 or 410;

    /// <summary>Convenience factory for a successful result.</summary>
    public static TmWebPushResult Ok(int statusCode = 201) => new() { Success = true, StatusCode = statusCode };

    /// <summary>Convenience factory for a failed result.</summary>
    public static TmWebPushResult Failed(int statusCode, string? error = null)
        => new() { Success = false, StatusCode = statusCode, Error = error };
}
