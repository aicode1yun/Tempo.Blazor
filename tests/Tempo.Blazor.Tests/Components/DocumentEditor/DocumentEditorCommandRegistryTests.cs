using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor.Registry;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>Unit tests for the command registry infrastructure (Phases 2.1–2.3).</summary>
public class DocumentEditorCommandRegistryTests
{
    // ─── 2.1 DocumentEditorCommandState ──────────────────────────────────────

    [Fact]
    public void CommandState_ExposesAllRequiredProperties()
    {
        var state = new DocumentEditorCommandState
        {
            Name = "bold",
            IsEnabled = true,
            Value = "active",
            AffectsData = true,
            DisabledReason = null
        };

        state.Name.Should().Be("bold");
        state.IsEnabled.Should().BeTrue();
        state.Value.Should().Be("active");
        state.AffectsData.Should().BeTrue();
        state.DisabledReason.Should().BeNull();
    }

    [Fact]
    public void CommandState_DisabledState_HasDisabledReason()
    {
        var state = new DocumentEditorCommandState
        {
            Name = "save",
            IsEnabled = false,
            AffectsData = true,
            DisabledReason = "read-only"
        };

        state.IsEnabled.Should().BeFalse();
        state.DisabledReason.Should().Be("read-only");
    }

    // ─── 2.1 Forced-disable stack ────────────────────────────────────────────

    [Fact]
    public async Task ForcedDisable_CommandStaysDisabledUntilLastReasonRemoved()
    {
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(AlwaysEnabledCommand("bold", affectsData: true));

        registry.AddForceDisableReason("bold", "dialog-open");
        registry.AddForceDisableReason("bold", "upload-pending");
        await registry.RefreshAllAsync(EditableContext());

        registry.GetState("bold")!.IsEnabled.Should().BeFalse("both reasons still present");

        registry.RemoveForceDisableReason("bold", "dialog-open");
        await registry.RefreshAllAsync(EditableContext());

        registry.GetState("bold")!.IsEnabled.Should().BeFalse("upload-pending reason still present");

        registry.RemoveForceDisableReason("bold", "upload-pending");
        await registry.RefreshAllAsync(EditableContext());

        registry.GetState("bold")!.IsEnabled.Should().BeTrue("all reasons removed");
    }

    [Fact]
    public async Task ForcedDisable_DisabledReasonContainsAllActiveReasons()
    {
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(AlwaysEnabledCommand("save", affectsData: true));

        registry.AddForceDisableReason("save", "reason-a");
        registry.AddForceDisableReason("save", "reason-b");
        await registry.RefreshAllAsync(EditableContext());

        var state = registry.GetState("save")!;
        state.DisabledReason.Should().Contain("reason-a");
        state.DisabledReason.Should().Contain("reason-b");
    }

    // ─── 2.1 Read-only vs AffectsData ────────────────────────────────────────

    [Fact]
    public async Task ReadOnly_DisablesCommandsThatAffectData()
    {
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(AlwaysEnabledCommand("bold", affectsData: true));
        await registry.RefreshAllAsync(ReadOnlyContext());

        var state = registry.GetState("bold")!;
        state.IsEnabled.Should().BeFalse();
        state.DisabledReason.Should().Be("read-only");
    }

    [Fact]
    public async Task ReadOnly_DoesNotDisableCommandsThatDontAffectData()
    {
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(AlwaysEnabledCommand("zoom-in", affectsData: false));
        await registry.RefreshAllAsync(ReadOnlyContext());

        registry.GetState("zoom-in")!.IsEnabled.Should().BeTrue();
    }

    // ─── 2.2 DocumentEditorCommandContext ────────────────────────────────────

    [Fact]
    public void CommandContext_ExposesAllRequiredFields()
    {
        var permissions = new DocumentEditorPermissions { CanEdit = true };
        var selection = new WysiwygSelectionSnapshot { IsCollapsed = true };
        var formatting = new WysiwygFormattingState { Bold = WysiwygFormattingValue.Active };
        var undoState = new WysiwygUndoState { CanUndo = true, CanRedo = false };

        var context = new DocumentEditorCommandContext
        {
            IsReadOnly = false,
            Permissions = permissions,
            ActiveRegion = "Header",
            SelectionSnapshot = selection,
            FormattingState = formatting,
            UndoState = undoState,
            HasDocument = true,
            CanExportPdf = true,
            CanImportDocx = false,
            CanExportDocx = true,
            IsSaving = false
        };

        context.IsReadOnly.Should().BeFalse();
        context.Permissions.Should().BeSameAs(permissions);
        context.ActiveRegion.Should().Be("Header");
        context.SelectionSnapshot.Should().BeSameAs(selection);
        context.FormattingState.Should().BeSameAs(formatting);
        context.UndoState.Should().BeSameAs(undoState);
        context.HasDocument.Should().BeTrue();
        context.CanExportPdf.Should().BeTrue();
        context.CanImportDocx.Should().BeFalse();
        context.CanExportDocx.Should().BeTrue();
        context.IsSaving.Should().BeFalse();
    }

    // ─── 2.3 DocumentEditorCommandRegistry ───────────────────────────────────

    [Fact]
    public void Registry_Register_AcceptsUniqueCommandName()
    {
        var registry = new DocumentEditorCommandRegistry();
        var action = () => registry.Register(AlwaysEnabledCommand("bold", affectsData: true));
        action.Should().NotThrow();
    }

    [Fact]
    public void Registry_Register_ThrowsOnDuplicateName()
    {
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(AlwaysEnabledCommand("bold", affectsData: true));

        var action = () => registry.Register(AlwaysEnabledCommand("bold", affectsData: false));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*bold*already registered*");
    }

    [Fact]
    public void Registry_TryGet_ReturnsTrueForRegisteredCommand()
    {
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(AlwaysEnabledCommand("save", affectsData: true));

        var found = registry.TryGet("save", out var command);

        found.Should().BeTrue();
        command.Should().NotBeNull();
        command!.Name.Should().Be("save");
    }

    [Fact]
    public void Registry_TryGet_ReturnsFalseForUnknownCommand()
    {
        var registry = new DocumentEditorCommandRegistry();

        var found = registry.TryGet("unknown", out var command);

        found.Should().BeFalse();
        command.Should().BeNull();
    }

    [Fact]
    public void Registry_GetRequired_ReturnsCommandWhenRegistered()
    {
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(AlwaysEnabledCommand("undo", affectsData: true));

        var command = registry.GetRequired("undo");

        command.Name.Should().Be("undo");
    }

    [Fact]
    public void Registry_GetRequired_ThrowsForUnknownCommand()
    {
        var registry = new DocumentEditorCommandRegistry();

        var action = () => registry.GetRequired("undo");

        action.Should().Throw<InvalidOperationException>().WithMessage("*undo*not registered*");
    }

    [Fact]
    public async Task Registry_RefreshAllAsync_UpdatesCurrentStateForAllCommands()
    {
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(AlwaysEnabledCommand("bold", affectsData: true));
        registry.Register(AlwaysEnabledCommand("zoom-in", affectsData: false));

        registry.GetState("bold").Should().BeNull("not refreshed yet");

        await registry.RefreshAllAsync(EditableContext());

        registry.GetState("bold")!.IsEnabled.Should().BeTrue();
        registry.GetState("zoom-in")!.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Registry_CurrentState_ContainsAllRefreshedCommands()
    {
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(AlwaysEnabledCommand("bold", affectsData: true));
        registry.Register(AlwaysEnabledCommand("italic", affectsData: true));
        await registry.RefreshAllAsync(EditableContext());

        registry.CurrentState.Should().ContainKeys("bold", "italic");
    }

    [Fact]
    public async Task Registry_CommandValue_IsComputedFromEntry()
    {
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(new FuncDocumentEditorCommandEntry(
            "bold",
            affectsData: true,
            computeEnabled: _ => true,
            execute: (_, _) => Task.CompletedTask,
            computeValue: ctx => ctx.FormattingState.Bold == WysiwygFormattingValue.Active ? "active" : "inactive"));

        var context = EditableContext() with { FormattingState = new WysiwygFormattingState { Bold = WysiwygFormattingValue.Active } };
        await registry.RefreshAllAsync(context);

        registry.GetState("bold")!.Value.Should().Be("active");
    }

    [Fact]
    public async Task Registry_CommandMetadata_FlowsIntoCurrentState()
    {
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(new FuncDocumentEditorCommandEntry(
            "bold",
            affectsData: true,
            computeEnabled: _ => true,
            execute: (_, _) => Task.CompletedTask,
            descriptionKey: "TmDocumentEditor_Bold",
            tooltipKey: "TmDocumentEditor_Bold",
            category: "Formatting",
            defaultShortcut: "Ctrl+B",
            icon: "bold",
            disabledReasonKey: "TmDocumentEditor_CommandDisabledUnavailable"));

        await registry.RefreshAllAsync(EditableContext());

        var state = registry.GetState("bold")!;
        state.DescriptionKey.Should().Be("TmDocumentEditor_Bold");
        state.TooltipKey.Should().Be("TmDocumentEditor_Bold");
        state.Category.Should().Be("Formatting");
        state.DefaultShortcut.Should().Be("Ctrl+B");
        state.Icon.Should().Be("bold");
        state.DisabledReasonKey.Should().BeNull();
        state.IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task Registry_InvisibleCommand_IsNotEnabled()
    {
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(new FuncDocumentEditorCommandEntry(
            "insertFootnote",
            affectsData: true,
            computeEnabled: _ => true,
            execute: (_, _) => Task.CompletedTask,
            computeVisible: _ => false));

        await registry.RefreshAllAsync(EditableContext());

        var state = registry.GetState("insertFootnote")!;
        state.IsVisible.Should().BeFalse();
        state.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Registry_DisabledCommand_UsesDisabledReasonKey()
    {
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(new FuncDocumentEditorCommandEntry(
            "save",
            affectsData: true,
            computeEnabled: _ => false,
            execute: (_, _) => Task.CompletedTask,
            disabledReasonKey: "TmDocumentEditor_CommandDisabledBusy"));

        await registry.RefreshAllAsync(EditableContext());

        registry.GetState("save")!.DisabledReasonKey.Should().Be("TmDocumentEditor_CommandDisabledBusy");
    }

    [Fact]
    public async Task Registry_ExecuteAsync_DoesNotRunDisabledCommand()
    {
        var executed = false;
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(new FuncDocumentEditorCommandEntry(
            "bold",
            affectsData: true,
            computeEnabled: _ => false,
            execute: (_, _) =>
            {
                executed = true;
                return Task.CompletedTask;
            }));

        var result = await registry.ExecuteAsync("bold", EditableContext());

        result.Should().BeFalse();
        executed.Should().BeFalse();
    }

    [Fact]
    public async Task Registry_ExecuteAsync_RunsEnabledCommand()
    {
        var executed = false;
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(new FuncDocumentEditorCommandEntry(
            "bold",
            affectsData: true,
            computeEnabled: _ => true,
            execute: (_, _) =>
            {
                executed = true;
                return Task.CompletedTask;
            }));

        var result = await registry.ExecuteAsync("bold", EditableContext());

        result.Should().BeTrue();
        executed.Should().BeTrue();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    // ─── 13.2 Restricted editing / IsProtected gating ────────────────────────

    [Fact]
    public async Task Protected_DisablesDataCommandWhenCaretOutsideEditableRegion()
    {
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(AlwaysEnabledCommand("bold", affectsData: true));

        var ctx = ProtectedOutsideContext();
        await registry.RefreshAllAsync(ctx);

        var state = registry.GetState("bold")!;
        state.IsEnabled.Should().BeFalse();
        state.DisabledReason.Should().Be("protected");
    }

    [Fact]
    public async Task Protected_EnablesDataCommandWhenCaretInsideEditableRegion()
    {
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(AlwaysEnabledCommand("bold", affectsData: true));

        var ctx = ProtectedInsideContext();
        await registry.RefreshAllAsync(ctx);

        var state = registry.GetState("bold")!;
        state.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Protected_DoesNotDisableViewCommandWhenCaretOutsideEditableRegion()
    {
        var registry = new DocumentEditorCommandRegistry();
        registry.Register(AlwaysEnabledCommand("findReplace", affectsData: false));

        var ctx = ProtectedOutsideContext();
        await registry.RefreshAllAsync(ctx);

        var state = registry.GetState("findReplace")!;
        state.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void CommandContext_IsProtected_DefaultsFalse()
    {
        var ctx = new DocumentEditorCommandContext();
        ctx.IsProtected.Should().BeFalse();
    }

    [Fact]
    public void CommandContext_IsInEditableRegion_DefaultsFalse()
    {
        var ctx = new DocumentEditorCommandContext();
        ctx.IsInEditableRegion.Should().BeFalse();
    }

    private static IDocumentEditorCommandEntry AlwaysEnabledCommand(string name, bool affectsData) =>
        new FuncDocumentEditorCommandEntry(
            name,
            affectsData,
            computeEnabled: _ => true,
            execute: (_, _) => Task.CompletedTask);

    private static DocumentEditorCommandContext EditableContext() =>
        new() { IsReadOnly = false, HasDocument = true };

    private static DocumentEditorCommandContext ReadOnlyContext() =>
        new() { IsReadOnly = true, HasDocument = true };

    private static DocumentEditorCommandContext ProtectedOutsideContext() =>
        new() { IsReadOnly = false, HasDocument = true, IsProtected = true, IsInEditableRegion = false };

    private static DocumentEditorCommandContext ProtectedInsideContext() =>
        new() { IsReadOnly = false, HasDocument = true, IsProtected = true, IsInEditableRegion = true };
}
