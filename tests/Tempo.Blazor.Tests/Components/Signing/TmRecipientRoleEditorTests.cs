using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class TmRecipientRoleEditorTests : LocalizationTestBase
{
    [Fact]
    public void Render_RendersRoleList()
    {
        var cut = RenderComponent<TmRecipientRoleEditor>(parameters =>
            parameters.Add(p => p.Roles, CreateRoles()));

        cut.FindAll(".tm-recipient-role-editor__row").Should().HaveCount(2);
        cut.Markup.Should().Contain("Approver");
        cut.Markup.Should().Contain("Reviewer");
    }

    [Fact]
    public void Render_EmptyRolesShowsDefaultRole()
    {
        var cut = RenderComponent<TmRecipientRoleEditor>(parameters =>
            parameters.Add(p => p.Roles, Array.Empty<SigningSubmitterRole>()));

        cut.FindAll(".tm-recipient-role-editor__row").Should().HaveCount(1);
        cut.Find(".tm-recipient-role-editor__name").GetAttribute("value").Should().Be("Signer 1");
    }

    [Fact]
    public void Render_RoleHasNameAndColor()
    {
        var cut = RenderComponent<TmRecipientRoleEditor>(parameters =>
            parameters.Add(p => p.Roles, CreateRoles()));

        var row = cut.Find("[data-role-uuid='role-a']");
        row.QuerySelector(".tm-recipient-role-editor__name")!.GetAttribute("value").Should().Be("Approver");
        row.QuerySelector(".tm-recipient-role-editor__color")!.GetAttribute("value").Should().Be("#2563eb");
    }

    [Fact]
    public void AddRole_CreatesNewRole()
    {
        IReadOnlyList<SigningSubmitterRole>? captured = null;
        var cut = RenderComponent<TmRecipientRoleEditor>(parameters =>
            parameters.Add(p => p.Roles, CreateRoles())
                      .Add(p => p.RolesChanged, EventCallback.Factory.Create<IReadOnlyList<SigningSubmitterRole>>(this, roles => captured = roles)));

        cut.Find(".tm-recipient-role-editor__add").Click();

        captured.Should().NotBeNull();
        captured!.Should().HaveCount(3);
        captured.Last().Name.Should().Be("Signer 3");
        captured.Last().Order.Should().Be(2);
    }

    [Fact]
    public void RemoveRole_RemovesRole()
    {
        IReadOnlyList<SigningSubmitterRole>? captured = null;
        var cut = RenderComponent<TmRecipientRoleEditor>(parameters =>
            parameters.Add(p => p.Roles, CreateRoles())
                      .Add(p => p.RolesChanged, EventCallback.Factory.Create<IReadOnlyList<SigningSubmitterRole>>(this, roles => captured = roles)));

        cut.Find("[data-role-uuid='role-b'] .tm-recipient-role-editor__remove").Click();

        captured.Should().NotBeNull();
        captured!.Select(role => role.Uuid).Should().NotContain("role-b");
    }

    [Fact]
    public void RenameRole_InvokesRolesChanged()
    {
        IReadOnlyList<SigningSubmitterRole>? captured = null;
        var cut = RenderComponent<TmRecipientRoleEditor>(parameters =>
            parameters.Add(p => p.Roles, CreateRoles())
                      .Add(p => p.RolesChanged, EventCallback.Factory.Create<IReadOnlyList<SigningSubmitterRole>>(this, roles => captured = roles)));

        cut.Find("[data-role-uuid='role-a'] .tm-recipient-role-editor__name").Change("Signer A");

        captured.Should().NotBeNull();
        captured!.First(role => role.Uuid == "role-a").Name.Should().Be("Signer A");
    }

    [Fact]
    public void TemplateRoles_DoesNotRenderRecipientInputs()
    {
        var cut = RenderComponent<TmRecipientRoleEditor>(parameters =>
            parameters.Add(p => p.Roles, CreateRoles())
                      .Add(p => p.Mode, TmRecipientRoleEditorMode.TemplateRoles));

        cut.FindAll(".tm-recipient-role-editor__email").Should().BeEmpty();
        cut.FindAll(".tm-recipient-role-editor__full-name").Should().BeEmpty();
        cut.FindAll(".tm-recipient-role-editor__phone").Should().BeEmpty();
    }

    [Fact]
    public void SubmissionRecipients_RendersRecipientInputs()
    {
        var cut = RenderComponent<TmRecipientRoleEditor>(parameters =>
            parameters.Add(p => p.Roles, CreateRoles())
                      .Add(p => p.Mode, TmRecipientRoleEditorMode.SubmissionRecipients));

        cut.FindAll(".tm-recipient-role-editor__email").Should().HaveCount(2);
        cut.FindAll(".tm-recipient-role-editor__full-name").Should().HaveCount(2);
        cut.FindAll(".tm-recipient-role-editor__phone").Should().HaveCount(2);
    }

    [Fact]
    public void SubmissionRecipients_EmailInputHasEmailType()
    {
        var cut = RenderComponent<TmRecipientRoleEditor>(parameters =>
            parameters.Add(p => p.Roles, CreateRoles())
                      .Add(p => p.Mode, TmRecipientRoleEditorMode.SubmissionRecipients));

        cut.Find(".tm-recipient-role-editor__email").GetAttribute("type").Should().Be("email");
    }

    [Fact]
    public void SubmissionRecipients_PhoneInputHasTelType()
    {
        var cut = RenderComponent<TmRecipientRoleEditor>(parameters =>
            parameters.Add(p => p.Roles, CreateRoles())
                      .Add(p => p.Mode, TmRecipientRoleEditorMode.SubmissionRecipients));

        cut.Find(".tm-recipient-role-editor__phone").GetAttribute("type").Should().Be("tel");
    }

    [Fact]
    public void SubmissionRecipients_RequiredEmailMissing_ShowsValidation()
    {
        var roles = CreateRoles();
        roles[0].Email = null;
        roles[0].IsOptional = false;

        var cut = RenderComponent<TmRecipientRoleEditor>(parameters =>
            parameters.Add(p => p.Roles, roles)
                      .Add(p => p.Mode, TmRecipientRoleEditorMode.SubmissionRecipients));

        cut.Find("[data-role-uuid='role-a'] .tm-recipient-role-editor__validation")
            .TextContent.Should().Contain("Email is required");
    }

    [Fact]
    public void MoveDown_ReordersRolesAndUpdatesOrderNumbers()
    {
        IReadOnlyList<SigningSubmitterRole>? captured = null;
        var cut = RenderComponent<TmRecipientRoleEditor>(parameters =>
            parameters.Add(p => p.Roles, CreateRoles())
                      .Add(p => p.RolesChanged, EventCallback.Factory.Create<IReadOnlyList<SigningSubmitterRole>>(this, roles => captured = roles)));

        cut.Find("[data-role-uuid='role-a'] .tm-recipient-role-editor__move-down").Click();

        captured.Should().NotBeNull();
        captured![0].Uuid.Should().Be("role-b");
        captured[0].Order.Should().Be(0);
        captured[1].Uuid.Should().Be("role-a");
        captured[1].Order.Should().Be(1);
    }

    [Fact]
    public void DragDrop_ReordersRoles()
    {
        IReadOnlyList<SigningSubmitterRole>? captured = null;
        var cut = RenderComponent<TmRecipientRoleEditor>(parameters =>
            parameters.Add(p => p.Roles, CreateRoles())
                      .Add(p => p.RolesChanged, EventCallback.Factory.Create<IReadOnlyList<SigningSubmitterRole>>(this, roles => captured = roles)));

        cut.Find("[data-role-uuid='role-b']").DragStart();
        cut.Find("[data-role-uuid='role-a']").Drop();

        captured.Should().NotBeNull();
        captured![0].Uuid.Should().Be("role-b");
        captured[0].Order.Should().Be(0);
        captured[1].Uuid.Should().Be("role-a");
        captured[1].Order.Should().Be(1);
    }

    [Fact]
    public void InviteByRole_UpdatesRole()
    {
        IReadOnlyList<SigningSubmitterRole>? captured = null;
        var cut = RenderComponent<TmRecipientRoleEditor>(parameters =>
            parameters.Add(p => p.Roles, CreateRoles())
                      .Add(p => p.RolesChanged, EventCallback.Factory.Create<IReadOnlyList<SigningSubmitterRole>>(this, roles => captured = roles)));

        cut.Find("[data-role-uuid='role-b'] .tm-recipient-role-editor__invite-by-role").Change("role-a");

        captured.Should().NotBeNull();
        captured!.First(role => role.Uuid == "role-b").InviteByRoleUuid.Should().Be("role-a");
    }

    [Fact]
    public void OptionalInviteByRole_UpdatesRole()
    {
        IReadOnlyList<SigningSubmitterRole>? captured = null;
        var cut = RenderComponent<TmRecipientRoleEditor>(parameters =>
            parameters.Add(p => p.Roles, CreateRoles())
                      .Add(p => p.RolesChanged, EventCallback.Factory.Create<IReadOnlyList<SigningSubmitterRole>>(this, roles => captured = roles)));

        cut.Find("[data-role-uuid='role-b'] .tm-recipient-role-editor__optional-invite-by-role").Change("role-a");

        captured.Should().NotBeNull();
        captured!.First(role => role.Uuid == "role-b").OptionalInviteByRoleUuid.Should().Be("role-a");
    }

    [Fact]
    public void InviteViaField_UpdatesRole()
    {
        IReadOnlyList<SigningSubmitterRole>? captured = null;
        var cut = RenderComponent<TmRecipientRoleEditor>(parameters =>
            parameters.Add(p => p.Roles, CreateRoles())
                      .Add(p => p.Fields, CreateFields())
                      .Add(p => p.RolesChanged, EventCallback.Factory.Create<IReadOnlyList<SigningSubmitterRole>>(this, roles => captured = roles)));

        cut.Find("[data-role-uuid='role-b'] .tm-recipient-role-editor__invite-via-field").Change("email-field");

        captured.Should().NotBeNull();
        captured!.First(role => role.Uuid == "role-b").InviteViaFieldUuid.Should().Be("email-field");
    }

    [Fact]
    public void InviteSelectors_DoNotOfferSelfReference()
    {
        var roles = CreateRoles();
        roles[0].InviteByRoleUuid = "role-a";

        var cut = RenderComponent<TmRecipientRoleEditor>(parameters =>
            parameters.Add(p => p.Roles, roles));

        var row = cut.Find("[data-role-uuid='role-a']");
        row.QuerySelector(".tm-recipient-role-editor__invite-by-role")!
            .GetAttribute("value").Should().Be(string.Empty);
        row.QuerySelectorAll(".tm-recipient-role-editor__invite-by-role option")
            .Select(option => option.GetAttribute("value"))
            .Should()
            .NotContain("role-a");
    }

    private static List<SigningSubmitterRole> CreateRoles()
    {
        return
        [
            new SigningSubmitterRole
            {
                Uuid = "role-a",
                Name = "Approver",
                Color = "#2563eb",
                Email = "approver@example.test",
                FullName = "Ada Approver",
                Phone = "+420555010101",
                Order = 0
            },
            new SigningSubmitterRole
            {
                Uuid = "role-b",
                Name = "Reviewer",
                Color = "#16a34a",
                Email = "reviewer@example.test",
                FullName = "Rene Reviewer",
                Phone = "+420555010102",
                Order = 1,
                IsOptional = true
            }
        ];
    }

    private static IReadOnlyList<SigningField> CreateFields()
    {
        return
        [
            new SigningField
            {
                Uuid = "email-field",
                Name = "Manager email",
                Type = SigningFieldType.Text
            }
        ];
    }
}
