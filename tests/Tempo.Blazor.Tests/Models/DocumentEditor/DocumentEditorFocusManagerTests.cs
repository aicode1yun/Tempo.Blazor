using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.Models.DocumentEditor;

public sealed class DocumentEditorFocusManagerTests
{
    [Fact]
    public void Register_StoresSurfaceToolbarAndFloatingLayerTargets()
    {
        var manager = new DocumentEditorFocusManager();

        manager.Register(new DocumentEditorFocusTarget
        {
            Id = "surface",
            Kind = DocumentEditorFocusTargetKind.Surface,
            Selector = "[data-testid='document-canvas-engine-host']"
        });
        manager.Register(new DocumentEditorFocusTarget
        {
            Id = "toolbar",
            Kind = DocumentEditorFocusTargetKind.Toolbar,
            Selector = "[data-testid='document-toolbar']"
        });
        manager.Register(new DocumentEditorFocusTarget
        {
            Id = "mini-toolbar",
            Kind = DocumentEditorFocusTargetKind.FloatingLayer,
            Selector = "[data-testid='document-mini-toolbar']"
        });

        manager.Targets.Select(target => target.Id).Should().Equal("mini-toolbar", "surface", "toolbar");
        manager.Targets.Single(target => target.Id == "surface").Kind.Should().Be(DocumentEditorFocusTargetKind.Surface);
        manager.Targets.Single(target => target.Id == "toolbar").Selector.Should().Be("[data-testid='document-toolbar']");
    }

    [Fact]
    public void PopRestoreTarget_ReturnsLastRegisteredRestoreTarget()
    {
        var manager = new DocumentEditorFocusManager();
        manager.Register(new DocumentEditorFocusTarget { Id = "surface", Kind = DocumentEditorFocusTargetKind.Surface });
        manager.Register(new DocumentEditorFocusTarget { Id = "toolbar", Kind = DocumentEditorFocusTargetKind.Toolbar });

        manager.PushRestoreTarget("surface");
        manager.PushRestoreTarget("toolbar");

        manager.PopRestoreTarget()!.Id.Should().Be("toolbar");
        manager.PopRestoreTarget()!.Id.Should().Be("surface");
        manager.PopRestoreTarget().Should().BeNull();
    }

    [Fact]
    public void ShouldTrapFocus_ReturnsTrueOnlyForTrapTargets()
    {
        var manager = new DocumentEditorFocusManager();
        manager.Register(new DocumentEditorFocusTarget
        {
            Id = "image-dialog",
            Kind = DocumentEditorFocusTargetKind.Modal,
            TrapsFocus = true
        });
        manager.Register(new DocumentEditorFocusTarget
        {
            Id = "mini-toolbar",
            Kind = DocumentEditorFocusTargetKind.FloatingLayer,
            TrapsFocus = false
        });

        manager.ShouldTrapFocus("image-dialog").Should().BeTrue();
        manager.ShouldTrapFocus("mini-toolbar").Should().BeFalse();
        manager.ShouldTrapFocus("missing").Should().BeFalse();
    }
}
