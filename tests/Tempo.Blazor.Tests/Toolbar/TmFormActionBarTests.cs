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
}
