using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Diagram;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class TmDiagramEditorRulerTests : LocalizationTestBase
{
    public TmDiagramEditorRulerTests()
    {
        var registry = Services.GetRequiredService<DiagramStencilRegistry>();
        registry.RegisterProvider(new BuiltInDiagramStencilProvider());
    }

    [Fact]
    public void DefaultState_RulersAreHidden()
    {
        var doc = new DiagramDocument();
        var cut = RenderComponent<TmDiagramEditor>(p => p
            .Add(e => e.Document, doc));

        var rulerElements = cut.FindAll(".tm-diagram-ruler");
        rulerElements.Count.Should().Be(0);
    }

    [Fact]
    public void ToggleRulersButton_ShowsRulers()
    {
        var doc = new DiagramDocument();
        var cut = RenderComponent<TmDiagramEditor>(p => p
            .Add(e => e.Document, doc));

        var toolbarButtons = cut.FindAll("button");
        var rulerBtn = toolbarButtons.FirstOrDefault(b =>
            (b.GetAttribute("aria-label")?.Contains("rulers", StringComparison.OrdinalIgnoreCase) == true ||
             b.GetAttribute("title")?.Contains("rulers", StringComparison.OrdinalIgnoreCase) == true ||
             b.GetAttribute("aria-label")?.Contains("pravítka", StringComparison.OrdinalIgnoreCase) == true ||
             b.GetAttribute("title")?.Contains("pravítka", StringComparison.OrdinalIgnoreCase) == true));
        rulerBtn.Should().NotBeNull();

        rulerBtn!.Click();
        cut.Render();

        var rulerElements = cut.FindAll(".tm-diagram-ruler");
        rulerElements.Count.Should().Be(2);
    }

    [Fact]
    public void ToggleRulersButton_Twice_HidesRulers()
    {
        var doc = new DiagramDocument();
        var cut = RenderComponent<TmDiagramEditor>(p => p
            .Add(e => e.Document, doc));

        var toolbarButtons = cut.FindAll("button");
        var rulerBtn = toolbarButtons.FirstOrDefault(b =>
            (b.GetAttribute("aria-label")?.Contains("rulers", StringComparison.OrdinalIgnoreCase) == true ||
             b.GetAttribute("title")?.Contains("rulers", StringComparison.OrdinalIgnoreCase) == true ||
             b.GetAttribute("aria-label")?.Contains("pravítka", StringComparison.OrdinalIgnoreCase) == true ||
             b.GetAttribute("title")?.Contains("pravítka", StringComparison.OrdinalIgnoreCase) == true));
        rulerBtn.Should().NotBeNull();

        rulerBtn!.Click();
        cut.Render();
        rulerBtn.Click();
        cut.Render();

        var rulerElements = cut.FindAll(".tm-diagram-ruler");
        rulerElements.Count.Should().Be(0);
    }
}
