using FluentAssertions;
using Microsoft.AspNetCore.Components.Rendering;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Wireframe;

public class BuiltInWireframeComponentProviderTests
{
    private readonly BuiltInWireframeComponentProvider _provider = new();
    private readonly List<WireframeComponentDef> _defs;

    public BuiltInWireframeComponentProviderTests()
    {
        _defs = _provider.GetDefinitions().ToList();
    }

    // ── Identity ──────────────────────────────────────────────────────────────

    [Fact]
    public void ProviderId_IsBuiltIn()
        => _provider.ProviderId.Should().Be("BuiltIn");

    [Fact]
    public void Priority_IsZero()
        => _provider.Priority.Should().Be(0);

    // ── Coverage ──────────────────────────────────────────────────────────────

    [Fact]
    public void RegistersAtLeast110Components()
        => _defs.Count.Should().BeGreaterThanOrEqualTo(110);

    [Theory]
    [InlineData("TmButton")]
    [InlineData("TmTextInput")]
    [InlineData("TmSelect")]
    [InlineData("TmDatePicker")]
    [InlineData("TmCard")]
    [InlineData("TmDataTable")]
    [InlineData("TmAlert")]
    [InlineData("TmModal")]
    [InlineData("TmTabs")]
    [InlineData("TmSidebar")]
    [InlineData("TmTopBar")]
    [InlineData("TmFormSection")]
    [InlineData("TmFileDropZone")]
    [InlineData("TmChart")]
    [InlineData("TmStepper")]
    [InlineData("TmTreeView")]
    [InlineData("TmWorkflowDesignerCanvas")]
    [InlineData("TmMarkdownEditor")]
    // Phase 1–3 new components
    [InlineData("TmAvatar")]
    [InlineData("TmAvatarGroup")]
    [InlineData("TmDropdown")]
    [InlineData("TmDropdownItem")]
    [InlineData("TmToolbar")]
    [InlineData("TmToolbarButton")]
    [InlineData("TmToolbarDivider")]
    [InlineData("TmNotificationBell")]
    [InlineData("TmTagPicker")]
    [InlineData("TmToastContainer")]
    [InlineData("TmPasswordStrengthIndicator")]
    [InlineData("TmColumnFilter")]
    [InlineData("TmColumnPicker")]
    [InlineData("TmViewManager")]
    [InlineData("TmValidatedField")]
    [InlineData("TmFormValidationMessage")]
    [InlineData("TmWorkflowToolbox")]
    [InlineData("TmWorkflowPropertiesPanel")]
    [InlineData("TmWorkflowMinimap")]
    [InlineData("TmCalendarView")]
    [InlineData("TmCalendarGrid")]
    [InlineData("TmFilterChip")]
    [InlineData("TmChangeDiff")]
    [InlineData("TmLightbox")]
    [InlineData("TmKeyboardShortcutsHelp")]
    [InlineData("TmDivider")]
    [InlineData("TmIcon")]
    // Phase 1 — Atomic inputs
    [InlineData("TmSlider")]
    [InlineData("TmRangeSlider")]
    [InlineData("TmRating")]
    [InlineData("TmMaskedTextBox")]
    [InlineData("TmMultiColumnComboBox")]
    // Phase 2 — Color controls
    [InlineData("TmColorPicker")]
    [InlineData("TmFlatColorPicker")]
    [InlineData("TmColorPalette")]
    [InlineData("TmColorGradient")]
    // Phase 3 — Signature & recurrence
    [InlineData("TmSignature")]
    [InlineData("TmSignatureCapture")]
    [InlineData("TmRecurrenceEditor")]
    // Phase 4 — Charts
    [InlineData("TmSparkline")]
    [InlineData("TmGauge")]
    [InlineData("TmStockChart")]
    // Phase 4 — Data display
    [InlineData("TmQRCode")]
    [InlineData("TmBarcode")]
    [InlineData("TmPdfViewer")]
    // Phase 5 — Buttons & navigation
    [InlineData("TmFloatingActionButton")]
    [InlineData("TmBottomNavigation")]
    [InlineData("TmMenu")]
    // Phase 6 — Layout
    [InlineData("TmStackLayout")]
    [InlineData("TmSplitter")]
    [InlineData("TmDockManager")]
    // Phase 7 — Builders & collaboration
    [InlineData("TmFormulaBuilder")]
    [InlineData("TmConditionBuilder")]
    [InlineData("TmCommentComposer")]
    [InlineData("TmCommentReactions")]
    [InlineData("TmReactionPicker")]
    [InlineData("TmShareLinkPanel")]
    [InlineData("TmSubmissionStatusTimeline")]
    [InlineData("TmAuditTrailViewer")]
    [InlineData("TmAIPrompt")]
    [InlineData("TmWidgetSelector")]
    // Phase 8 — Editors & Apps
    [InlineData("TmChat")]
    [InlineData("TmSpreadsheet")]
    [InlineData("TmGantt")]
    [InlineData("TmGanttPortfolio")]
    [InlineData("TmPivotTable")]
    [InlineData("TmTreeList")]
    [InlineData("TmDiagramEditor")]
    [InlineData("TmDocumentEditor")]
    [InlineData("TmNotionEditor")]
    [InlineData("TmNotionPage")]
    [InlineData("TmModelingEditor")]
    [InlineData("TmFileManager")]
    [InlineData("TmDocumentManager")]
    public void ExpectedComponentIsRegistered(string type)
        => _defs.Should().Contain(d => d.Type == type, $"{type} should be registered");

    // ── Definition integrity ──────────────────────────────────────────────────

    [Fact]
    public void AllDefsHaveNonEmptyType()
        => _defs.Should().AllSatisfy(d => d.Type.Should().NotBeNullOrWhiteSpace());

    [Fact]
    public void AllDefsHaveNonEmptyDisplayName()
        => _defs.Should().AllSatisfy(d => d.DisplayName.Should().NotBeNullOrWhiteSpace());

    [Fact]
    public void AllDefsHaveNonEmptyCategory()
        => _defs.Should().AllSatisfy(d => d.Category.Should().NotBeNullOrWhiteSpace());

    [Fact]
    public void AllDefsHavePositiveDimensions()
        => _defs.Should().AllSatisfy(d =>
        {
            d.DefaultWidth.Should().BePositive($"{d.Type} DefaultWidth must be > 0");
            d.DefaultHeight.Should().BePositive($"{d.Type} DefaultHeight must be > 0");
        });

    [Fact]
    public void AllDefsHaveRenderSvgDelegate()
        => _defs.Should().AllSatisfy(d => d.RenderSvg.Should().NotBeNull($"{d.Type} must have RenderSvg"));

    [Fact]
    public void AllDefsMarkedIsBuiltIn()
        => _defs.Should().AllSatisfy(d => d.IsBuiltIn.Should().BeTrue($"{d.Type} should be IsBuiltIn=true"));

    [Fact]
    public void NoDuplicateTypes()
        => _defs.Select(d => d.Type).Should().OnlyHaveUniqueItems();

    // ── Categories ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Buttons")]
    [InlineData("Inputs")]
    [InlineData("Pickers")]
    [InlineData("Data Display")]
    [InlineData("Data Table")]
    [InlineData("Feedback")]
    [InlineData("Navigation")]
    [InlineData("Layout")]
    [InlineData("Forms")]
    [InlineData("Files")]
    [InlineData("Charts")]
    [InlineData("Complex")]
    [InlineData("Icons")]
    [InlineData("Color")]
    [InlineData("Editors & Apps")]
    public void CategoryExists(string category)
        => _defs.Should().Contain(d => d.Category == category, $"category '{category}' should have components");

    // ── RenderSvg smoke test ──────────────────────────────────────────────────

    [Theory]
    [InlineData("TmButton", 120, 36)]
    [InlineData("TmTextInput", 200, 36)]
    [InlineData("TmDataTable", 500, 200)]
    [InlineData("TmModal", 400, 300)]
    [InlineData("TmChart", 360, 220)]
    public void RenderSvg_DoesNotThrow(string type, double w, double h)
    {
        var def = _defs.First(d => d.Type == type);
        var element = new WireframeElement { Id = "test", Type = type, W = w, H = h };

        var act = () =>
        {
            var builder = new RenderTreeBuilder();
            def.RenderSvg(element, builder);
        };

        act.Should().NotThrow($"{type}.RenderSvg should not throw");
    }

    [Fact]
    public void AllComponents_RenderSvgDoesNotThrow()
    {
        foreach (var def in _defs)
        {
            var element = new WireframeElement
            {
                Id = "test",
                Type = def.Type,
                W = def.DefaultWidth,
                H = def.DefaultHeight
            };

            var act = () =>
            {
                var builder = new RenderTreeBuilder();
                def.RenderSvg(element, builder);
            };

            act.Should().NotThrow($"{def.Type}.RenderSvg should not throw");
        }
    }
}
