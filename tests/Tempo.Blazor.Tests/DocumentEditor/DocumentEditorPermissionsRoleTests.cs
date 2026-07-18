using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor;

/// <summary>
/// Phase 8: role matrix over the coarse permission booleans. DocumentEditorPermissions.ForRole
/// materializes a role (Viewer/Commenter/SuggestOnly/Editor/Owner) into the existing boolean
/// capability set — additive, so 2.0.x consumers configuring booleans directly keep working.
/// SuggestOnly is the "can propose, cannot directly edit" role: editing is allowed ONLY as
/// tracked suggestions (the editor forces track changes on and locks the toggle) and the role
/// cannot accept/reject suggestions.
/// </summary>
public sealed class DocumentEditorPermissionsRoleTests
{
    [Fact]
    public void ForRole_Viewer_IsReadOnly()
    {
        var permissions = DocumentEditorPermissions.ForRole(DocumentEditorRole.Viewer);

        permissions.Role.Should().Be(DocumentEditorRole.Viewer);
        permissions.CanRead.Should().BeTrue();
        permissions.CanEdit.Should().BeFalse();
        permissions.CanComment.Should().BeFalse();
        permissions.CanSuggest.Should().BeFalse();
        permissions.CanReviewSuggestions.Should().BeFalse();
        permissions.CanCreateVersion.Should().BeFalse();
        permissions.CanImport.Should().BeFalse();
        permissions.CanExport.Should().BeFalse();
        permissions.CanViewAudit.Should().BeFalse();
    }

    [Fact]
    public void ForRole_Commenter_CanOnlyReadAndComment()
    {
        var permissions = DocumentEditorPermissions.ForRole(DocumentEditorRole.Commenter);

        permissions.Role.Should().Be(DocumentEditorRole.Commenter);
        permissions.CanRead.Should().BeTrue();
        permissions.CanComment.Should().BeTrue();
        permissions.CanEdit.Should().BeFalse();
        permissions.CanSuggest.Should().BeFalse();
        permissions.CanReviewSuggestions.Should().BeFalse();
        permissions.CanCreateVersion.Should().BeFalse();
        permissions.CanImport.Should().BeFalse();
        permissions.CanExport.Should().BeFalse();
    }

    [Fact]
    public void ForRole_SuggestOnly_CanProposeButMustTrackAndCannotDecide()
    {
        var permissions = DocumentEditorPermissions.ForRole(DocumentEditorRole.SuggestOnly);

        permissions.Role.Should().Be(DocumentEditorRole.SuggestOnly);
        permissions.CanRead.Should().BeTrue();
        permissions.CanComment.Should().BeTrue();
        permissions.CanSuggest.Should().BeTrue();
        // Editing is allowed ONLY as tracked suggestions: CanEdit stays true so typing works,
        // RequiresTrackedEditing marks that the editor must force track changes on.
        permissions.CanEdit.Should().BeTrue();
        permissions.RequiresTrackedEditing.Should().BeTrue();
        permissions.CanReviewSuggestions.Should().BeFalse("suggest-only must not decide their own suggestions");
        permissions.CanCreateVersion.Should().BeFalse();
        permissions.CanImport.Should().BeFalse();
        permissions.CanExport.Should().BeFalse();
    }

    [Fact]
    public void ForRole_Editor_HasFullEditingWithoutAudit()
    {
        var permissions = DocumentEditorPermissions.ForRole(DocumentEditorRole.Editor);

        permissions.Role.Should().Be(DocumentEditorRole.Editor);
        permissions.CanRead.Should().BeTrue();
        permissions.CanEdit.Should().BeTrue();
        permissions.RequiresTrackedEditing.Should().BeFalse();
        permissions.CanComment.Should().BeTrue();
        permissions.CanSuggest.Should().BeTrue();
        permissions.CanReviewSuggestions.Should().BeTrue();
        permissions.CanCreateVersion.Should().BeTrue();
        permissions.CanImport.Should().BeTrue();
        permissions.CanExport.Should().BeTrue();
        permissions.CanViewAudit.Should().BeFalse();
    }

    [Fact]
    public void ForRole_Owner_HasEverything()
    {
        var permissions = DocumentEditorPermissions.ForRole(DocumentEditorRole.Owner);

        permissions.Role.Should().Be(DocumentEditorRole.Owner);
        permissions.CanRead.Should().BeTrue();
        permissions.CanEdit.Should().BeTrue();
        permissions.CanComment.Should().BeTrue();
        permissions.CanSuggest.Should().BeTrue();
        permissions.CanReviewSuggestions.Should().BeTrue();
        permissions.CanCreateVersion.Should().BeTrue();
        permissions.CanImport.Should().BeTrue();
        permissions.CanExport.Should().BeTrue();
        permissions.CanViewAudit.Should().BeTrue();
    }

    [Fact]
    public void DefaultConstructor_KeepsLegacyPermissiveBooleans()
    {
        var permissions = new DocumentEditorPermissions();

        permissions.Role.Should().BeNull("legacy consumers configure booleans directly");
        permissions.RequiresTrackedEditing.Should().BeFalse();
        permissions.CanRead.Should().BeTrue();
        permissions.CanEdit.Should().BeTrue();
        permissions.CanComment.Should().BeTrue();
    }

    [Fact]
    public void TryParseRole_AcceptsCaseInsensitiveNames()
    {
        DocumentEditorPermissions.TryParseRole("suggestonly", out var role).Should().BeTrue();
        role.Should().Be(DocumentEditorRole.SuggestOnly);
        DocumentEditorPermissions.TryParseRole("VIEWER", out role).Should().BeTrue();
        role.Should().Be(DocumentEditorRole.Viewer);
        DocumentEditorPermissions.TryParseRole("nonsense", out _).Should().BeFalse();
    }
}
