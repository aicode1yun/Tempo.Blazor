using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Abstractions.WorkItems;

/// <summary>
/// A person (or virtual resource) assigned to a <see cref="TmWorkItem"/>.
/// Keeps scheduling-specific metadata while exposing conversion to the shared
/// <see cref="TmUserRef"/> snapshot used by people-aware components.
/// </summary>
public sealed class TmWorkItemAssignee
{
    /// <summary>Stable user identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional username or mention handle.</summary>
    public string? UserName { get; set; }

    /// <summary>Optional avatar URL.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>Optional e-mail address.</summary>
    public string? Email { get; set; }

    /// <summary>Hourly billing rate for cost tracking. Null = not specified.</summary>
    public decimal? HourlyRate { get; set; }

    /// <summary>When true, this is a generic placeholder resource (not a real user account).</summary>
    public bool IsVirtual { get; set; }

    /// <summary>Optional CSS color used to tint the person in timelines/avatars.</summary>
    public string? Color { get; set; }

    /// <summary>Optional provider/source discriminator for applications with multiple people sources.</summary>
    public string? SourceKey { get; set; }

    /// <summary>Optional tenant, workspace, or application scope identifier.</summary>
    public string? TenantId { get; set; }

    /// <summary>Creates a shared user reference from this assignment snapshot.</summary>
    public TmUserRef ToUserRef()
        => new()
        {
            Id = Id,
            DisplayName = Name,
            UserName = UserName,
            Email = Email,
            AvatarUrl = AvatarUrl,
            Color = Color,
            IsVirtual = IsVirtual,
            SourceKey = SourceKey,
            TenantId = TenantId
        };

    /// <summary>Creates an assignment snapshot from a shared user reference.</summary>
    /// <param name="user">Shared user reference to copy.</param>
    /// <param name="hourlyRate">Optional hourly billing rate for the assignment.</param>
    public static TmWorkItemAssignee FromUserRef(TmUserRef user, decimal? hourlyRate = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new TmWorkItemAssignee
        {
            Id = user.Id,
            Name = user.DisplayName,
            UserName = user.UserName,
            AvatarUrl = user.AvatarUrl,
            Email = user.Email,
            HourlyRate = hourlyRate,
            IsVirtual = user.IsVirtual,
            Color = user.Color,
            SourceKey = user.SourceKey,
            TenantId = user.TenantId
        };
    }
}
