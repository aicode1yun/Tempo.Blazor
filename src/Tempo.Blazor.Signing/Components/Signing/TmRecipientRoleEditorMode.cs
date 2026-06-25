namespace Tempo.Blazor.Components.Signing;

/// <summary>Editing mode for <see cref="TmRecipientRoleEditor"/>.</summary>
public enum TmRecipientRoleEditorMode
{
    /// <summary>Edit template signer roles without concrete recipient contact details.</summary>
    TemplateRoles,

    /// <summary>Edit concrete submission recipients with email, name, and phone details.</summary>
    SubmissionRecipients
}
