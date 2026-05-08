namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Role or signer definition used by signing templates and submissions.</summary>
public class SigningSubmitterRole
{
    /// <summary>Stable role identifier used by fields.</summary>
    public string Uuid { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Display name of the signer role.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional signer email for submission recipient editing.</summary>
    public string? Email { get; set; }

    /// <summary>Optional signer phone number for submission recipient editing.</summary>
    public string? Phone { get; set; }

    /// <summary>Optional signer full name for submission recipient editing.</summary>
    public string? FullName { get; set; }

    /// <summary>Color used to identify the role in field overlays.</summary>
    public string? Color { get; set; }

    /// <summary>Zero-based signing order.</summary>
    public int Order { get; set; }

    /// <summary>Whether this role represents the requester.</summary>
    public bool IsRequester { get; set; }

    /// <summary>Whether this invite is optional.</summary>
    public bool IsOptional { get; set; }

    /// <summary>Role UUID that must invite this signer.</summary>
    public string? InviteByRoleUuid { get; set; }

    /// <summary>Role UUID that may optionally invite this signer.</summary>
    public string? OptionalInviteByRoleUuid { get; set; }

    /// <summary>Field UUID whose value can invite this signer.</summary>
    public string? InviteViaFieldUuid { get; set; }
}
