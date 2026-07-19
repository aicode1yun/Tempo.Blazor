using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Modeling;
using Tempo.Blazor.Modeling;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Modeling;

public sealed class TmModelingViewSelectorTests : LocalizationTestBase
{
    public TmModelingViewSelectorTests()
    {
        Services.AddSingleton<IModelingNotationProfile>(new TestNotationProfile(
            "bpmn",
            "BPMN 2.0",
            ["default", "process"]));
        Services.AddSingleton<IModelingNotationProfile>(new TestNotationProfile(
            "archimate",
            "ArchiMate 3",
            ["overview", "application"]));
        Services.AddSingleton<IModelingNotationProfile>(new TestNotationProfile(
            "custom-empty",
            "Custom empty",
            []));
    }

    [Fact]
    public void Changing_notation_updates_available_viewpoints()
    {
        using var cut = Render<TmModelingViewSelector>(parameters => parameters
            .Add(p => p.NotationKey, "bpmn"));

        cut.Find("[data-testid='modeling-notation-select']").Change("archimate");

        var options = cut.Find("[data-testid='modeling-viewpoint-select']").QuerySelectorAll("option");
        options.Select(option => option.GetAttribute("value")).Should().Contain(["overview", "application"]);
        cut.Markup.Should().Contain("Application usage");
    }

    [Fact]
    public void Unknown_notation_shows_empty_viewpoint_state_without_exception()
    {
        using var cut = Render<TmModelingViewSelector>(parameters => parameters
            .Add(p => p.NotationKey, "unknown"));

        cut.Find("[data-testid='modeling-view-selector']").Should().NotBeNull();
        cut.FindAll("[data-testid='modeling-viewpoint-select']").Should().BeEmpty();
        cut.Find("[data-testid='modeling-viewpoint-empty']").TextContent.Should().Contain("No viewpoints");
    }

    [Fact]
    public void Notation_without_viewpoints_disables_viewpoint_choice_with_message()
    {
        using var cut = Render<TmModelingViewSelector>(parameters => parameters
            .Add(p => p.NotationKey, "custom-empty"));

        cut.FindAll("[data-testid='modeling-viewpoint-select']").Should().BeEmpty();
        cut.Find("[data-testid='modeling-viewpoint-empty']").TextContent.Should().Contain("No viewpoints");
    }

    [Fact]
    public void Selecting_viewpoint_emits_selected_value()
    {
        string? selected = null;
        using var cut = Render<TmModelingViewSelector>(parameters => parameters
            .Add(p => p.NotationKey, "archimate")
            .Add(p => p.OnViewpointChanged, EventCallback.Factory.Create<string?>(this, value => selected = value)));

        cut.Find("[data-testid='modeling-viewpoint-select']").Change("application");

        selected.Should().Be("application");
    }

    [Fact]
    public void Null_notation_shows_select_notation_prompt()
    {
        using var cut = Render<TmModelingViewSelector>(parameters => parameters
            .Add(p => p.NotationKey, (string?)null));

        cut.Find("[data-testid='modeling-notation-select']").TextContent.Should().Contain("Select notation");
        cut.Find("[data-testid='modeling-viewpoint-empty']").TextContent.Should().Contain("Select a notation");
    }

    private sealed class TestNotationProfile : IModelingNotationProfile
    {
        public TestNotationProfile(string notationKey, string displayName, IReadOnlyCollection<string> viewpoints)
        {
            NotationKey = notationKey;
            DisplayName = displayName;
            SupportedViewpointKeys = viewpoints;
        }

        public string NotationKey { get; }

        public string DisplayName { get; }

        public IReadOnlyCollection<string> SupportedElementTypes { get; } = [];

        public IReadOnlyCollection<string> SupportedRelationshipTypes { get; } = [];

        public IReadOnlyCollection<string> SupportedViewpointKeys { get; }
    }
}
