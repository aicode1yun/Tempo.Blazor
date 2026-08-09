using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Toolbar;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Toolbar;

/// <summary>TDD tests for TmFormActionBar.</summary>
public class TmFormActionBarTests : LocalizationTestBase
{
    private const string ModulePath = "./_content/Tempo.Blazor/Components/Toolbar/TmFormActionBar.razor.js";

    [Fact]
    public void FormActionBar_RendersAsToolbarRole()
    {
        var cut = Render<TmFormActionBar>();

        cut.Find(".tm-form-action-bar").GetAttribute("role").Should().Be("toolbar");
    }

    [Fact]
    public void FormActionBar_DefaultPosition_HasStaticClass()
    {
        var cut = Render<TmFormActionBar>();

        cut.Find(".tm-form-action-bar").ClassList.Should().Contain("tm-form-action-bar--static");
    }

    [Fact]
    public void FormActionBar_StickyTopPosition_HasStickyClass()
    {
        var cut = Render<TmFormActionBar>(p => p.Add(x => x.Position, FormActionBarPosition.StickyTop));

        cut.Find(".tm-form-action-bar").ClassList.Should().Contain("tm-form-action-bar--sticky-top");
    }

    [Fact]
    public void FormActionBar_FloatingBottomPosition_HasFloatingClass()
    {
        var cut = Render<TmFormActionBar>(p => p.Add(x => x.Position, FormActionBarPosition.FloatingBottom));

        cut.Find(".tm-form-action-bar").ClassList.Should().Contain("tm-form-action-bar--floating-bottom");
    }

    [Fact]
    public void FormActionBar_RendersAllFourActionAndStatusSlots()
    {
        var cut = Render<TmFormActionBar>(p => p
            .Add(x => x.ChildContent, "<span class='child'>Doc title</span>")
            .Add(x => x.Status, "<span class='status-text'>Saved</span>")
            .Add(x => x.PrimaryActions, "<button class='primary-btn'>Save</button>")
            .Add(x => x.SecondaryActions, "<button class='secondary-btn'>Cancel</button>")
            .Add(x => x.DangerActions, "<button class='danger-btn'>Delete</button>"));

        cut.Find(".tm-form-action-bar__start .child").TextContent.Should().Be("Doc title");
        cut.Find(".tm-form-action-bar__status .status-text").TextContent.Should().Be("Saved");
        cut.Find(".tm-form-action-bar__primary .primary-btn").TextContent.Should().Be("Save");
        cut.Find(".tm-form-action-bar__secondary .secondary-btn").TextContent.Should().Be("Cancel");
        cut.Find(".tm-form-action-bar__danger .danger-btn").TextContent.Should().Be("Delete");
    }

    [Fact]
    public void FormActionBar_NoActionsProvided_DoesNotRenderEndWrapper()
    {
        var cut = Render<TmFormActionBar>();

        cut.FindAll(".tm-form-action-bar__end").Should().BeEmpty();
    }

    [Fact]
    public void FormActionBar_AriaLabel_SetsAttribute()
    {
        var cut = Render<TmFormActionBar>(p => p.Add(x => x.AriaLabel, "Document actions"));

        cut.Find(".tm-form-action-bar").GetAttribute("aria-label").Should().Be("Document actions");
    }

    [Fact]
    public void FormActionBar_LiveMessage_RendersPoliteLiveRegion()
    {
        var cut = Render<TmFormActionBar>(p => p.Add(x => x.LiveMessage, "All changes saved"));

        var region = cut.Find("[aria-live='polite']");
        region.TextContent.Should().Be("All changes saved");
    }

    [Fact]
    public void FormActionBar_TestId_SetsDataTestId()
    {
        var cut = Render<TmFormActionBar>(p => p.Add(x => x.TestId, "save-bar"));

        cut.Find(".tm-form-action-bar").GetAttribute("data-testid").Should().Be("save-bar");
    }

    [Fact]
    public void FormActionBar_Class_AppendsAdditionalClass()
    {
        var cut = Render<TmFormActionBar>(p => p.Add(x => x.Class, "my-extra-class"));

        cut.Find(".tm-form-action-bar").ClassList.Should().Contain("my-extra-class");
    }

    [Fact]
    public void FormActionBar_ShowOnScrollTrue_AddsShowOnScrollClassAndRegistersScrollListenerModule()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.SetupVoid("register", _ => true).SetVoidResult();

        var cut = Render<TmFormActionBar>(p => p.Add(x => x.ShowOnScroll, true));

        cut.Find(".tm-form-action-bar").ClassList.Should().Contain("tm-form-action-bar--show-on-scroll");
        module.Invocations.Should().Contain(invocation => invocation.Identifier == "register");
    }

    [Fact]
    public void FormActionBar_ShowOnScrollFalse_DoesNotAddClassOrRegisterListener()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.SetupVoid("register", _ => true).SetVoidResult();

        var cut = Render<TmFormActionBar>(p => p.Add(x => x.ShowOnScroll, false));

        cut.Find(".tm-form-action-bar").ClassList.Should().NotContain("tm-form-action-bar--show-on-scroll");
        module.Invocations.Should().NotContain(invocation => invocation.Identifier == "register");
    }

    /// <summary>
    /// The floating bar spans the viewport, which runs it underneath a shell's fixed side navigation.
    /// The inset has to be a variable a host can set, because the alternative — overriding <c>left</c>
    /// from application CSS — has to out-specify this rule's <c>[b-*]</c> scope attribute and loses the
    /// cascade whenever it merely ties. Asserted against the source file rather than a rendered element:
    /// bUnit has no layout, so nothing about the cascade is observable from a rendered component.
    /// </summary>
    [Fact]
    public void FormActionBarCss_FloatingBottomInset_IsHostOverridableViaCustomProperty()
    {
        var css = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Tempo.Blazor", "Components", "Toolbar", "TmFormActionBar.razor.css"));

        var floating = SelectorBlock(css, ".tm-form-action-bar--floating-bottom");

        // Logical properties, so the variable names describe what they actually do — in LTR they
        // resolve to the same left/right the released version pinned.
        floating.Should().Contain("inset-inline-start: var(--tm-form-action-bar-inset-inline-start, 0);");
        floating.Should().Contain("inset-inline-end: var(--tm-form-action-bar-inset-inline-end, 0);");
        // Unset, the fallback keeps the released full-bleed behaviour.
        floating.Should().NotContain("left: 0;");
        floating.Should().NotContain("right: 0;");
    }

    /// <summary>Text of the first declaration block whose selector line starts with <paramref name="selector"/>.</summary>
    private static string SelectorBlock(string css, string selector)
    {
        var start = css.IndexOf(selector + " {", StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, "the CSS must still declare {0}", selector);

        var end = css.IndexOf('}', start);
        end.Should().BeGreaterThan(start);

        return css[start..end];
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
