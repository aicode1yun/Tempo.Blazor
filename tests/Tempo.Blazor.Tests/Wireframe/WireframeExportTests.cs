using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Wireframe.Export;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Wireframe;

/// <summary>
/// Tests for wireframe export options, dialog, and related models.
/// </summary>
public class WireframeExportTests : LocalizationTestBase
{
    // ── WireframeExportOptions ────────────────────────────────────────────────

    [Fact]
    public void ExportOptions_Defaults()
    {
        var opts = new WireframeExportOptions();
        opts.IncludeBackground.Should().BeTrue();
        opts.Scale.Should().Be(1);
        opts.PageRange.Should().Be("all");
        opts.BackgroundColor.Should().BeNull();
    }

    [Fact]
    public void ExportOptions_CanSetProperties()
    {
        var opts = new WireframeExportOptions
        {
            IncludeBackground = false,
            Scale = 2,
            PageRange = "0,1",
            BackgroundColor = "#ff0000"
        };
        opts.IncludeBackground.Should().BeFalse();
        opts.Scale.Should().Be(2);
        opts.PageRange.Should().Be("0,1");
        opts.BackgroundColor.Should().Be("#ff0000");
    }

    // ── WireframeExportRequest ────────────────────────────────────────────────

    [Fact]
    public void ExportRequest_Defaults()
    {
        var req = new WireframeExportRequest();
        req.Svg.Should().BeEmpty();
        req.FileName.Should().Be("wireframe");
        req.Options.Should().NotBeNull();
    }

    [Fact]
    public void ExportRequest_CanSetProperties()
    {
        var req = new WireframeExportRequest
        {
            Svg = "<svg></svg>",
            FileName = "my-wireframe",
            Options = new WireframeExportOptions { Scale = 3 }
        };
        req.Svg.Should().Be("<svg></svg>");
        req.FileName.Should().Be("my-wireframe");
        req.Options.Scale.Should().Be(3);
    }

    // ── WireframeExportDialogResult ───────────────────────────────────────────

    [Fact]
    public void DialogResult_Defaults()
    {
        var result = new WireframeExportDialogResult();
        result.FileName.Should().Be("wireframe");
        result.Format.Should().Be("png");
        result.Options.Should().NotBeNull();
    }

    // ── TmWireframeExportDialog rendering ─────────────────────────────────────

    [Fact]
    public void Dialog_Renders_WhenShowIsTrue()
    {
        var cut = Render<TmWireframeExportDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.DefaultFileName, "test"));

        cut.Find(".tm-wd-export-dialog").Should().NotBeNull();
    }

    [Fact]
    public void Dialog_DoesNotRenderContent_WhenShowIsFalse()
    {
        var cut = Render<TmWireframeExportDialog>(parameters => parameters
            .Add(p => p.Show, false));

        // Modal is rendered but hidden; check that inner content is not present
        cut.FindAll(".tm-wd-export-dialog").Should().BeEmpty();
    }

    [Fact]
    public void Dialog_ContainsFormatSelect()
    {
        var cut = Render<TmWireframeExportDialog>(parameters => parameters
            .Add(p => p.Show, true));

        var selects = cut.FindAll("select");
        selects.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Dialog_ContainsScaleSelect_WhenPngSelected()
    {
        var cut = Render<TmWireframeExportDialog>(parameters => parameters
            .Add(p => p.Show, true));

        // Default format is png, so scale select should be visible
        cut.FindAll("select").Count.Should().Be(2); // format + scale
    }

    [Fact]
    public void Dialog_OnExport_EmitsResult()
    {
        WireframeExportDialogResult? captured = null;
        var cut = Render<TmWireframeExportDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.OnExport, EventCallback.Factory.Create<WireframeExportDialogResult>(this, r => captured = r)));

        // Click the Export button (primary button in footer)
        var buttons = cut.FindAll("button");
        var exportBtn = buttons.FirstOrDefault(b => b.TextContent.Contains("Export"));
        exportBtn.Should().NotBeNull();
        exportBtn!.Click();

        captured.Should().NotBeNull();
        captured!.Format.Should().Be("png");
        captured.Options.Scale.Should().Be(1);
    }

    [Fact]
    public void Dialog_OnClose_EmitsEvent()
    {
        var closed = false;
        var cut = Render<TmWireframeExportDialog>(parameters => parameters
            .Add(p => p.Show, true)
            .Add(p => p.OnClose, EventCallback.Factory.Create(this, () => closed = true)));

        var buttons = cut.FindAll("button");
        var cancelBtn = buttons.FirstOrDefault(b => b.TextContent.Contains("Cancel"));
        cancelBtn.Should().NotBeNull();
        cancelBtn!.Click();

        closed.Should().BeTrue();
    }
}
