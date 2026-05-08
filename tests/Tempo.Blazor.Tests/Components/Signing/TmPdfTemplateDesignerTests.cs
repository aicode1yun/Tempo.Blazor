using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class TmPdfTemplateDesignerTests : LocalizationTestBase
{
    [Fact]
    public void Render_RootAndEmptyState()
    {
        var cut = RenderComponent<TmPdfTemplateDesigner>();

        cut.Find(".tm-pdf-template-designer").Should().NotBeNull();
        cut.Find(".tm-empty-state").TextContent.Should().Contain("No documents");
    }

    [Fact]
    public void Render_DocumentsAndFields_UsesViewerAndOverlay()
    {
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages())
                      .Add(p => p.Fields, CreateFields()));

        cut.FindAll(".tm-document-page-viewer").Should().HaveCount(2);
        cut.FindAll(".tm-signing-field").Should().HaveCount(2);
    }

    [Fact]
    public void Render_AcceptsFieldsChangedAndDocuments()
    {
        IReadOnlyList<SigningField>? captured = null;
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages())
                      .Add(p => p.Fields, CreateFields())
                      .Add(p => p.FieldsChanged, EventCallback.Factory.Create<IReadOnlyList<SigningField>>(this, value => captured = value)));

        cut.Find("[data-field-uuid='field-1']").Click();
        cut.Find(".tm-signing-field-editor-panel__name").Change("Updated name");

        captured.Should().NotBeNull();
        captured!.Single(field => field.Uuid == "field-1").Name.Should().Be("Updated name");
    }

    [Fact]
    public void Palette_RendersAllowedFieldTypesAndCanBeFiltered()
    {
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages())
                      .Add(p => p.AllowedFieldTypes, new[] { SigningFieldType.Text, SigningFieldType.Signature }));

        cut.FindAll(".tm-pdf-template-designer__palette-item").Should().HaveCount(2);
        cut.Find("[data-field-type='Text']").TextContent.Should().Contain("Text");
        cut.Find("[data-field-type='Signature']").TextContent.Should().Contain("Signature");
    }

    [Fact]
    public void Palette_ClickFieldType_EntersDrawMode()
    {
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages()));

        cut.Find("[data-field-type='Text']").Click();

        cut.Find(".tm-pdf-template-designer").ClassList.Should().Contain("tm-pdf-template-designer--drawing");
        cut.Find("[data-field-type='Text']").ClassList.Should().Contain("tm-pdf-template-designer__palette-item--active");
    }

    [Fact]
    public void Palette_WhenDisabled_IsHidden()
    {
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages())
                      .Add(p => p.Disabled, true));

        cut.FindAll(".tm-pdf-template-designer__palette").Should().BeEmpty();
    }

    [Fact]
    public void DrawField_CreatesFieldAndArea()
    {
        IReadOnlyList<SigningField>? captured = null;
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages())
                      .Add(p => p.SubmitterRoles, CreateRoles())
                      .Add(p => p.SelectedSubmitterUuid, "role-2")
                      .Add(p => p.FieldsChanged, EventCallback.Factory.Create<IReadOnlyList<SigningField>>(this, value => captured = value)));

        cut.Find("[data-field-type='Text']").Click();
        var surface = cut.Find("[data-page-key='attachment-1:0'] .tm-pdf-template-designer__page-surface");
        surface.MouseDown(new MouseEventArgs { OffsetX = 100, OffsetY = 100 });
        surface.MouseMove(new MouseEventArgs { OffsetX = 260, OffsetY = 180 });

        cut.Find(".tm-pdf-template-designer__draft").Should().NotBeNull();

        surface.MouseUp(new MouseEventArgs { OffsetX = 260, OffsetY = 180 });

        captured.Should().NotBeNull();
        var field = captured!.Single();
        field.Type.Should().Be(SigningFieldType.Text);
        field.SubmitterUuid.Should().Be("role-2");
        field.Areas.Single().AttachmentUuid.Should().Be("attachment-1");
        field.Areas.Single().Page.Should().Be(0);
    }

    [Fact]
    public void DrawField_SmallRectangle_IsIgnored()
    {
        IReadOnlyList<SigningField>? captured = null;
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages())
                      .Add(p => p.FieldsChanged, EventCallback.Factory.Create<IReadOnlyList<SigningField>>(this, value => captured = value)));

        cut.Find("[data-field-type='Text']").Click();
        var surface = cut.Find("[data-page-key='attachment-1:0'] .tm-pdf-template-designer__page-surface");
        surface.MouseDown(new MouseEventArgs { OffsetX = 100, OffsetY = 100 });
        surface.MouseUp(new MouseEventArgs { OffsetX = 104, OffsetY = 104 });

        captured.Should().BeNull();
    }

    [Fact]
    public void SelectMoveAndResize_UpdateFieldArea()
    {
        IReadOnlyList<SigningField>? captured = null;
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages())
                      .Add(p => p.Fields, CreateFields())
                      .Add(p => p.FieldsChanged, EventCallback.Factory.Create<IReadOnlyList<SigningField>>(this, value => captured = value)));

        cut.Find("[data-field-uuid='field-1']").Click();
        cut.Find("[data-field-uuid='field-1']").MouseDown(new MouseEventArgs { ClientX = 10, ClientY = 10 });
        cut.Find(".tm-pdf-template-designer").MouseMove(new MouseEventArgs { ClientX = 110, ClientY = 60 });
        cut.Find(".tm-pdf-template-designer").MouseUp();

        captured!.Single(field => field.Uuid == "field-1").Areas.Single().X.Should().BeApproximately(0.2, 0.001);
        cut.Find("[data-handle='SouthEast']").MouseDown(new MouseEventArgs { ClientX = 100, ClientY = 100 });
        cut.Find(".tm-pdf-template-designer").MouseMove(new MouseEventArgs { ClientX = 200, ClientY = 200 });
        cut.Find(".tm-pdf-template-designer").MouseUp();

        captured!.Single(field => field.Uuid == "field-1").Areas.Single().Width.Should().BeGreaterThan(0.2);
    }

    [Fact]
    public void Select_WithControlKey_AddsToMultiSelect()
    {
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages())
                      .Add(p => p.Fields, CreateFields()));

        cut.Find("[data-field-uuid='field-1']").Click();
        cut.Find("[data-field-uuid='field-2']").Click(new MouseEventArgs { CtrlKey = true });

        cut.FindAll(".tm-signing-field--selected").Should().HaveCount(2);
        cut.Find(".tm-pdf-template-designer__selection-bounds").Should().NotBeNull();
    }

    [Fact]
    public void Select_WithControlMouseDown_DoesNotReplaceExistingSelection()
    {
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages())
                      .Add(p => p.Fields, CreateFields()));

        cut.Find("[data-field-uuid='field-1']").Click();
        cut.Find("[data-field-uuid='field-2']").MouseDown(new MouseEventArgs { CtrlKey = true });
        cut.Find("[data-field-uuid='field-2']").Click(new MouseEventArgs { CtrlKey = true });

        cut.FindAll(".tm-signing-field--selected").Should().HaveCount(2);
    }

    [Fact]
    public void DragSelectionBox_SelectsMultipleFields()
    {
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages())
                      .Add(p => p.Fields, CreateFields()));

        var surface = cut.Find("[data-page-key='attachment-1:0'] .tm-pdf-template-designer__page-surface");
        surface.MouseDown(new MouseEventArgs { OffsetX = 0, OffsetY = 0 });
        surface.MouseMove(new MouseEventArgs { OffsetX = 500, OffsetY = 500 });
        surface.MouseUp(new MouseEventArgs { OffsetX = 500, OffsetY = 500 });

        cut.FindAll(".tm-signing-field--selected").Should().HaveCount(2);
    }

    [Fact]
    public void DeleteSelected_RemovesMultipleFields()
    {
        IReadOnlyList<SigningField>? captured = null;
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages())
                      .Add(p => p.Fields, CreateFields())
                      .Add(p => p.FieldsChanged, EventCallback.Factory.Create<IReadOnlyList<SigningField>>(this, value => captured = value)));

        cut.Find("[data-field-uuid='field-1']").Click();
        cut.Find("[data-field-uuid='field-2']").Click(new MouseEventArgs { CtrlKey = true });
        cut.Find(".tm-pdf-template-designer__delete-selected").Click();

        captured.Should().NotBeNull();
        captured.Should().BeEmpty();
    }

    [Fact]
    public void ContextMenus_RenderUsingContextMenuComponent()
    {
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages())
                      .Add(p => p.Fields, CreateFields()));

        cut.Find("[data-field-uuid='field-1']").ContextMenu();

        cut.Find(".tm-context-menu-wrapper").Should().NotBeNull();
        cut.Markup.Should().Contain("Copy field");
        cut.Markup.Should().Contain("Delete field");
        cut.Markup.Should().Contain("Settings");
    }

    [Fact]
    public void PageContextMenu_OffersPasteAndAutodetect()
    {
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages())
                      .Add(p => p.OnDetectFields, () => Task.FromResult<IReadOnlyList<SigningField>>([])));

        cut.Find("[data-page-key='attachment-1:0'] .tm-pdf-template-designer__page-surface").ContextMenu();

        cut.Markup.Should().Contain("Paste field");
        cut.Markup.Should().Contain("Autodetect fields");
    }

    [Fact]
    public void CopyPaste_CreatesFieldCopyOnCurrentPage()
    {
        IReadOnlyList<SigningField>? captured = null;
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages())
                      .Add(p => p.Fields, CreateFields().Take(1).ToArray())
                      .Add(p => p.FieldsChanged, EventCallback.Factory.Create<IReadOnlyList<SigningField>>(this, value => captured = value)));

        cut.Find("[data-field-uuid='field-1']").ContextMenu();
        cut.Find(".tm-pdf-template-designer__copy-field").Click();
        cut.Find("[data-page-key='attachment-1:1'] .tm-pdf-template-designer__page-surface").ContextMenu();
        cut.Find(".tm-pdf-template-designer__paste-field").Click();

        captured.Should().NotBeNull();
        captured.Should().HaveCount(2);
        captured!.Select(field => field.Uuid).Distinct().Should().HaveCount(2);
        captured!.Last().Areas.Single().Page.Should().Be(1);
    }

    [Fact]
    public void CopyToAllPages_CreatesAreaOnEachDocumentPage()
    {
        IReadOnlyList<SigningField>? captured = null;
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages())
                      .Add(p => p.Fields, CreateFields().Take(1).ToArray())
                      .Add(p => p.FieldsChanged, EventCallback.Factory.Create<IReadOnlyList<SigningField>>(this, value => captured = value)));

        cut.Find("[data-field-uuid='field-1']").Click();
        cut.Find(".tm-signing-field-editor-panel__copy-to-pages").Click();

        captured.Should().NotBeNull();
        captured!.Single().Areas.Should().HaveCount(2);
    }

    [Fact]
    public void DetectFields_AddsReturnedFieldsAndShowsLoadingState()
    {
        IReadOnlyList<SigningField>? captured = null;
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages())
                      .Add(p => p.OnDetectFields, () => Task.FromResult<IReadOnlyList<SigningField>>([CreateField("detected", "Detected")]))
                      .Add(p => p.FieldsChanged, EventCallback.Factory.Create<IReadOnlyList<SigningField>>(this, value => captured = value)));

        cut.Find(".tm-pdf-template-designer__detect").Click();

        captured.Should().NotBeNull();
        captured!.Single().Uuid.Should().Be("detected");
    }

    [Fact]
    public void DetectFields_Error_ShowsAlert()
    {
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages())
                      .Add(p => p.OnDetectFields, () => throw new InvalidOperationException("Detection failed")));

        cut.Find(".tm-pdf-template-designer__detect").Click();

        cut.Find(".tm-alert").TextContent.Should().Contain("Detection failed");
    }

    [Fact]
    public void MobileMode_UsesCompactPalette()
    {
        var cut = RenderComponent<TmPdfTemplateDesigner>(parameters =>
            parameters.Add(p => p.Documents, CreatePages())
                      .Add(p => p.MobileMode, true));

        cut.Find(".tm-pdf-template-designer").ClassList.Should().Contain("tm-pdf-template-designer--mobile");
        cut.Find(".tm-pdf-template-designer__palette").ClassList.Should().Contain("tm-pdf-template-designer__palette--compact");
        cut.Find(".tm-pdf-template-designer__mobile-draw").Click();
        cut.Find(".tm-pdf-template-designer").ClassList.Should().Contain("tm-pdf-template-designer--drawing");
    }

    private static IReadOnlyList<SigningDocumentPage> CreatePages()
    {
        return
        [
            new SigningDocumentPage
            {
                AttachmentUuid = "attachment-1",
                PageIndex = 0,
                ImageUrl = "/page-1.png",
                Width = 1000,
                Height = 1000,
                Label = "Page 1"
            },
            new SigningDocumentPage
            {
                AttachmentUuid = "attachment-1",
                PageIndex = 1,
                ImageUrl = "/page-2.png",
                Width = 1000,
                Height = 1000,
                Label = "Page 2"
            }
        ];
    }

    private static IReadOnlyList<SigningField> CreateFields()
    {
        return
        [
            CreateField("field-1", "Name", x: 0.1, y: 0.1),
            CreateField("field-2", "Signature", SigningFieldType.Signature, x: 0.35, y: 0.1)
        ];
    }

    private static SigningField CreateField(
        string uuid,
        string name,
        SigningFieldType type = SigningFieldType.Text,
        double x = 0.1,
        double y = 0.1)
    {
        return new SigningField
        {
            Uuid = uuid,
            Name = name,
            Type = type,
            Areas =
            [
                new SigningFieldArea
                {
                    Uuid = $"{uuid}-area",
                    AttachmentUuid = "attachment-1",
                    Page = 0,
                    X = x,
                    Y = y,
                    Width = 0.2,
                    Height = 0.08
                }
            ]
        };
    }

    private static IReadOnlyList<SigningSubmitterRole> CreateRoles()
    {
        return
        [
            new SigningSubmitterRole { Uuid = "role-1", Name = "Signer" },
            new SigningSubmitterRole { Uuid = "role-2", Name = "Approver" }
        ];
    }
}
