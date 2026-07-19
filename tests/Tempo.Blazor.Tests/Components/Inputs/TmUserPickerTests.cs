using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Inputs;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Inputs;

/// <summary>TDD tests for TmUserPicker.</summary>
public class TmUserPickerTests : LocalizationTestBase
{
    private sealed record TestUser(string Login, string DisplayName);

    private static readonly TestUser[] AllUsers =
    [
        new("alice", "Alice Anderson"),
        new("bob", "Bob Brown"),
        new("carol", "Carol Clark"),
    ];

    private static Func<string, CancellationToken, Task<TmPickerSearchResult<TestUser>>> SearchProvider(
        TmPickerFetchState stateOverride = TmPickerFetchState.Ok)
        => (query, _) =>
        {
            if (stateOverride != TmPickerFetchState.Ok)
            {
                return Task.FromResult(new TmPickerSearchResult<TestUser>([], stateOverride));
            }

            var matches = AllUsers
                .Where(u => u.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var state = matches.Count > 0 ? TmPickerFetchState.Ok : TmPickerFetchState.Empty;
            return Task.FromResult(new TmPickerSearchResult<TestUser>(matches, state));
        };

    private static Func<string, CancellationToken, Task<TmPickerResolveResult<TestUser>>> ResolveProvider(
        TmPickerFetchState stateOverride = TmPickerFetchState.Ok)
        => (login, _) =>
        {
            if (stateOverride != TmPickerFetchState.Ok)
            {
                return Task.FromResult(new TmPickerResolveResult<TestUser>(default, stateOverride));
            }

            var user = AllUsers.FirstOrDefault(u => u.Login == login);
            return Task.FromResult(new TmPickerResolveResult<TestUser>(user, TmPickerFetchState.Ok));
        };

    private IRenderedComponent<TmUserPicker<TestUser>> RenderPicker(
        Action<ComponentParameterCollectionBuilder<TmUserPicker<TestUser>>> configure,
        Func<string, CancellationToken, Task<TmPickerSearchResult<TestUser>>>? search = null,
        Func<string, CancellationToken, Task<TmPickerResolveResult<TestUser>>>? resolve = null,
        int minChars = 1)
        => Render<TmUserPicker<TestUser>>(p =>
        {
            p.Add(x => x.SearchProvider, search ?? SearchProvider());
            p.Add(x => x.ResolveProvider, resolve ?? ResolveProvider());
            p.Add(x => x.ValueSelector, (TestUser u) => u.Login);
            p.Add(x => x.DisplaySelector, (TestUser u) => u.DisplayName);
            p.Add(x => x.MinChars, minChars);
            configure(p);
        });

    [Fact]
    public void UserPicker_Renders_WithLabelAndPlaceholder()
    {
        var cut = RenderPicker(p => p
            .Add(x => x.Label, "Owner")
            .Add(x => x.Placeholder, "Search people..."));

        cut.Find(".tm-user-picker").Should().NotBeNull();
        cut.Find("label").TextContent.Should().Be("Owner");
        cut.Find("input").GetAttribute("placeholder").Should().Be("Search people...");
    }

    [Fact]
    public async Task UserPicker_BelowMinChars_DoesNotShowResults()
    {
        var cut = RenderPicker(p => { }, minChars: 3);

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "al" });

        cut.FindAll(".tm-user-picker__result-item").Should().BeEmpty();
    }

    [Fact]
    public async Task UserPicker_Search_RendersOkResults_WithDisplayAndLogin()
    {
        var cut = RenderPicker(p => { });

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "alice" });
        cut.WaitForState(() => cut.FindAll(".tm-user-picker__result-item").Count > 0);

        var item = cut.Find(".tm-user-picker__result-item");
        item.TextContent.Should().Contain("Alice Anderson");
        item.TextContent.Should().Contain("[alice]");
    }

    [Fact]
    public async Task UserPicker_Search_EmptyState_RendersNoResultsMessage_DistinctFromTransient()
    {
        var cut = RenderPicker(p => { });

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "zzz-no-match" });
        cut.WaitForState(() => cut.FindAll("[data-testid='tm-user-picker-no-results']").Count > 0);

        cut.FindAll("[data-testid='tm-user-picker-search-transient']").Should().BeEmpty();
        cut.Find("[data-testid='tm-user-picker-no-results']").TextContent.Should().Be("No results found");
    }

    [Fact]
    public async Task UserPicker_Search_TransientState_RendersRetryError_NotConflatedWithEmpty()
    {
        var cut = RenderPicker(p => { }, search: SearchProvider(TmPickerFetchState.Transient));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "anything" });
        cut.WaitForState(() => cut.FindAll("[data-testid='tm-user-picker-search-transient']").Count > 0);

        cut.FindAll("[data-testid='tm-user-picker-no-results']").Should().BeEmpty();
        cut.Find("[data-testid='tm-user-picker-search-transient']").TextContent.Should().Contain("Something went wrong");
        cut.Find("[data-testid='tm-user-picker-search-retry']").Should().NotBeNull();
    }

    [Fact]
    public async Task UserPicker_TransientRetry_ReinvokesSearchProvider()
    {
        var callCount = 0;
        Func<string, CancellationToken, Task<TmPickerSearchResult<TestUser>>> flaky = (query, _) =>
        {
            callCount++;
            if (callCount == 1)
            {
                return Task.FromResult(new TmPickerSearchResult<TestUser>([], TmPickerFetchState.Transient));
            }

            return Task.FromResult(new TmPickerSearchResult<TestUser>([AllUsers[0]], TmPickerFetchState.Ok));
        };

        var cut = RenderPicker(p => { }, search: flaky);

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "alice" });
        cut.WaitForState(() => cut.FindAll("[data-testid='tm-user-picker-search-transient']").Count > 0);

        await cut.Find("[data-testid='tm-user-picker-search-retry']").ClickAsync(new());
        cut.WaitForState(() => cut.FindAll(".tm-user-picker__result-item").Count > 0);

        callCount.Should().Be(2);
        cut.FindAll("[data-testid='tm-user-picker-search-transient']").Should().BeEmpty();
    }

    [Fact]
    public async Task UserPicker_PointerDownOnResult_SelectsUser_AndFiresValueChanged()
    {
        string? changedValue = "unset";
        var cut = RenderPicker(p => p.Add(x => x.ValueChanged, EventCallback.Factory.Create<string?>(this, v => changedValue = v)));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "alice" });
        cut.WaitForState(() => cut.FindAll(".tm-user-picker__result-item").Count > 0);

        var item = cut.Find(".tm-user-picker__result-item");
        await cut.InvokeAsync(() => item.TriggerEvent("onpointerdown", new PointerEventArgs()));

        changedValue.Should().Be("alice");
        cut.FindAll(".tm-user-picker__result-item").Should().BeEmpty();
        cut.Find(".tm-user-picker__selected").TextContent.Should().Contain("Alice Anderson");
    }

    [Fact]
    public async Task UserPicker_ClearButton_ClearsSelectionAndFiresValueChangedNull()
    {
        string? changedValue = "unset";
        var cut = RenderPicker(p => p
            .Add(x => x.Value, "alice")
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string?>(this, v => changedValue = v)));

        cut.WaitForState(() => cut.FindAll(".tm-user-picker__selected").Count > 0);

        await cut.Find("[data-testid='tm-user-picker-clear']").ClickAsync(new());

        changedValue.Should().BeNull();
        cut.FindAll(".tm-user-picker__selected").Should().BeEmpty();
    }

    [Fact]
    public void UserPicker_ValueSet_ResolvesAndShowsSelectedChip()
    {
        var cut = RenderPicker(p => p.Add(x => x.Value, "bob"));

        cut.WaitForState(() => cut.FindAll(".tm-user-picker__selected").Count > 0);
        cut.Find(".tm-user-picker__selected").TextContent.Should().Contain("Bob Brown");
    }

    [Fact]
    public void UserPicker_ValueSet_ResolveTransient_RendersRetryError_NotSelectedChip()
    {
        var cut = RenderPicker(p => p.Add(x => x.Value, "bob"), resolve: ResolveProvider(TmPickerFetchState.Transient));

        cut.WaitForState(() => cut.FindAll("[data-testid='tm-user-picker-resolve-transient']").Count > 0);

        cut.FindAll(".tm-user-picker__selected").Should().BeEmpty();
        cut.Find("[data-testid='tm-user-picker-resolve-transient']").Should().NotBeNull();
    }

    [Fact]
    public async Task UserPicker_KeyboardArrowsAndEnter_SelectHighlightedResult()
    {
        var cut = RenderPicker(p => { });

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "a" }); // matches alice + carol ("Clark" has no 'a'? use broader term)
        cut.WaitForState(() => cut.FindAll(".tm-user-picker__result-item").Count > 0);

        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        cut.FindAll(".tm-user-picker__result-item").Should().BeEmpty();
        cut.Find(".tm-user-picker__selected").Should().NotBeNull();
    }

    [Fact]
    public void UserPicker_Disabled_DisablesInputAndHidesClear()
    {
        var cut = RenderPicker(p => p
            .Add(x => x.Value, "alice")
            .Add(x => x.Disabled, true));

        cut.WaitForState(() => cut.FindAll(".tm-user-picker__selected").Count > 0);
        cut.FindAll("[data-testid='tm-user-picker-clear']").Should().BeEmpty();
    }

    [Fact]
    public async Task UserPicker_ItemTemplate_OverridesDefaultResultRendering()
    {
        var cut = RenderPicker(p => p.Add(x => x.ItemTemplate, user => builder =>
        {
            builder.OpenElement(0, "em");
            builder.AddAttribute(1, "class", "custom-result");
            builder.AddContent(2, $"~{user.DisplayName}~");
            builder.CloseElement();
        }));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "alice" });
        cut.WaitForState(() => cut.FindAll(".custom-result").Count > 0);

        cut.Find(".custom-result").TextContent.Should().Be("~Alice Anderson~");
    }

    [Fact]
    public async Task UserPicker_EmptyTemplate_OverridesDefaultNoResultsRendering()
    {
        var cut = RenderPicker(p => p.Add(x => x.EmptyTemplate, (RenderFragment)(builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "custom-empty");
            builder.AddContent(2, "Nobody here");
            builder.CloseElement();
        })));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "zzz-no-match" });
        cut.WaitForState(() => cut.FindAll(".custom-empty").Count > 0);

        cut.Find(".custom-empty").TextContent.Should().Be("Nobody here");
    }
}
