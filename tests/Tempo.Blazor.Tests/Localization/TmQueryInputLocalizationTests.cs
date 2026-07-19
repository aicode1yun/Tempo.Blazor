using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.Inputs;

namespace Tempo.Blazor.Tests.Localization;

/// <summary>TmQueryInput must resolve its placeholder and dropdown status texts via ITmLocalizer.</summary>
public class TmQueryInputLocalizationTests : LocalizationTestBase
{
    private static Func<QuerySuggestionRequest, Task<IReadOnlyList<QuerySuggestion>>> Empty
        => _ => Task.FromResult<IReadOnlyList<QuerySuggestion>>([]);

    [Fact]
    public void QueryInput_DefaultPlaceholder_UsesLocalizer_English()
    {
        var cut = Render<TmQueryInput>();
        cut.Find(".tm-query-input__input").GetAttribute("placeholder").Should().Be("Type a query…");
    }

    [Fact]
    public void QueryInput_ExplicitPlaceholder_Overrides()
    {
        var cut = Render<TmQueryInput>(p => p.Add(c => c.Placeholder, "status = ..."));
        cut.Find(".tm-query-input__input").GetAttribute("placeholder").Should().Be("status = ...");
    }

    [Fact]
    public void QueryInput_DefaultPlaceholder_UsesLocalizer_Czech()
    {
        UseCzechLocalization();
        var cut = Render<TmQueryInput>();
        cut.Find(".tm-query-input__input").GetAttribute("placeholder").Should().Be("Zadejte dotaz…");
    }

    [Fact]
    public void QueryInput_EmptyState_UsesLocalizer_Czech()
    {
        UseCzechLocalization();
        var cut = Render<TmQueryInput>(p => p
            .Add(c => c.DebounceMs, 0)
            .Add(c => c.SuggestionsProvider, Empty));

        cut.Find(".tm-query-input__input").Input("zzz");

        cut.WaitForAssertion(() =>
            cut.Find(".tm-query-input__status").TextContent.Should().Be("Žádné návrhy"));
    }
}
