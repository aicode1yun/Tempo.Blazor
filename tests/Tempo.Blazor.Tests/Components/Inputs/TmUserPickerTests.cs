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

    // ── Accessibility: label association, required, and the combobox/listbox wiring ──────────────

    /// <summary>
    /// The label used to be a bare <c>&lt;label&gt;</c> with no <c>for</c>, so clicking it did nothing and
    /// a screen reader never tied it to the combobox.
    /// </summary>
    [Fact]
    public void UserPicker_Label_IsAssociatedWithTheSearchInput()
    {
        var cut = RenderPicker(p => p.Add(x => x.Label, "Owner"));

        var id = cut.Find("input").GetAttribute("id");
        id.Should().NotBeNullOrEmpty();
        cut.Find("label").GetAttribute("for").Should().Be(id);
    }

    [Fact]
    public void UserPicker_ExplicitId_IsUsedForBothTheInputAndTheLabel()
    {
        var cut = RenderPicker(p => p
            .Add(x => x.Label, "Owner")
            .Add(x => x.Id, "owner-picker"));

        cut.Find("input").GetAttribute("id").Should().Be("owner-picker");
        cut.Find("label").GetAttribute("for").Should().Be("owner-picker");
    }

    /// <summary>Once a user is picked there is no input, so a <c>for</c> would point at nothing.</summary>
    [Fact]
    public void UserPicker_WithSelection_DropsTheDanglingLabelAssociation()
    {
        var cut = RenderPicker(p => p
            .Add(x => x.Label, "Owner")
            .Add(x => x.Value, "alice"));

        cut.WaitForState(() => cut.FindAll(".tm-user-picker__selected").Count > 0);

        cut.FindAll("input").Should().BeEmpty();
        cut.Find("label").GetAttribute("for").Should().BeNull();
    }

    [Fact]
    public void UserPicker_Required_MarksLabelAndInput()
    {
        var cut = RenderPicker(p => p
            .Add(x => x.Label, "Owner")
            .Add(x => x.Required, true));

        cut.Find("label").ClassList.Should().Contain("tm-input-label-required");
        cut.Find("input").HasAttribute("required").Should().BeTrue();
        cut.Find("input").GetAttribute("aria-required").Should().Be("true");
    }

    [Fact]
    public void UserPicker_NotRequired_LeavesNoRequiredMarkers()
    {
        var cut = RenderPicker(p => p.Add(x => x.Label, "Owner"));

        cut.Find("label").ClassList.Should().NotContain("tm-input-label-required");
        cut.Find("input").HasAttribute("required").Should().BeFalse();
        cut.Find("input").GetAttribute("aria-required").Should().BeNull();
    }

    /// <summary>
    /// <c>role="combobox"</c> shipped without <c>aria-controls</c> or <c>aria-activedescendant</c>, so a
    /// screen reader had no way to announce the option the arrow keys were moving over: focus stays in the
    /// input, and nothing pointed at the highlighted <c>option</c>.
    /// </summary>
    [Fact]
    public async Task UserPicker_OpenResults_PointTheComboboxAtTheListboxAndHighlightedOption()
    {
        var cut = RenderPicker(p => p.Add(x => x.Id, "owner-picker"));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "a" });
        cut.WaitForState(() => cut.FindAll(".tm-user-picker__result-item").Count > 0);

        var input = cut.Find("input");
        var listbox = cut.Find("[role='listbox']");

        listbox.GetAttribute("id").Should().Be("owner-picker-results");
        input.GetAttribute("aria-controls").Should().Be("owner-picker-results");
        input.GetAttribute("aria-expanded").Should().Be("true");

        // First result is highlighted on open, so that is what has to be announced.
        input.GetAttribute("aria-activedescendant").Should().Be("owner-picker-option-0");
        cut.Find(".tm-user-picker__result-item--active").GetAttribute("id").Should().Be("owner-picker-option-0");
    }

    [Fact]
    public async Task UserPicker_ArrowDown_MovesActiveDescendantWithTheHighlight()
    {
        var cut = RenderPicker(p => p.Add(x => x.Id, "owner-picker"));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "a" });
        cut.WaitForState(() => cut.FindAll(".tm-user-picker__result-item").Count > 1);

        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        cut.Find("input").GetAttribute("aria-activedescendant").Should().Be("owner-picker-option-1");
        cut.Find(".tm-user-picker__result-item--active").GetAttribute("id").Should().Be("owner-picker-option-1");
    }

    /// <summary>An id pointing at an element that is not there is worse than no attribute at all.</summary>
    [Fact]
    public void UserPicker_ClosedResults_HaveNoDanglingAriaControlsOrActiveDescendant()
    {
        var cut = RenderPicker(p => p.Add(x => x.Id, "owner-picker"));

        var input = cut.Find("input");
        input.GetAttribute("aria-controls").Should().BeNull();
        input.GetAttribute("aria-activedescendant").Should().BeNull();
    }

    // ── Floating menu ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The results list is <c>position: absolute</c>, so an ancestor with <c>overflow: auto</c> — a modal
    /// body — clips it and scrolls it away from its input. The floating variant lifts it out of the flow
    /// and has script anchor it to the input.
    /// </summary>
    [Fact]
    public async Task UserPicker_FloatingMenu_AnchorsTheResultsListToTheInput()
    {
        var module = JSInterop.SetupModule(FloatingModulePath);
        module.SetupVoid("anchor", _ => true).SetVoidResult();

        var cut = RenderPicker(p => p.Add(x => x.FloatingMenu, true));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "a" });
        cut.WaitForState(() => cut.FindAll(".tm-user-picker__result-item").Count > 0);

        cut.Find("[role='listbox']").ClassList.Should().Contain("tm-user-picker__results--floating");
        module.Invocations.Should().Contain(invocation => invocation.Identifier == "anchor");
    }

    [Fact]
    public async Task UserPicker_WithoutFloatingMenu_DoesNotTouchTheFloatingLayer()
    {
        var module = JSInterop.SetupModule(FloatingModulePath);
        module.SetupVoid("anchor", _ => true).SetVoidResult();

        var cut = RenderPicker(p => { });

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "a" });
        cut.WaitForState(() => cut.FindAll(".tm-user-picker__result-item").Count > 0);

        cut.Find("[role='listbox']").ClassList.Should().NotContain("tm-user-picker__results--floating");
        module.Invocations.Should().BeEmpty();
    }

    /// <summary>
    /// The shared scroll and resize listeners live in a module shared by every picker on the page, so a
    /// closed list that never released its tracking entry would keep them bound forever.
    /// </summary>
    /// <remarks>
    /// The release has to be addressed BY ID, not by element reference: the list is out of the DOM by
    /// the time this runs, and Blazor resolves an <c>ElementReference</c> through a document query, so a
    /// detached element arrives in JS as null and would release nothing at all. bUnit cannot see that —
    /// it records the invocation either way — so the argument itself is what this asserts.
    /// </remarks>
    [Fact]
    public async Task UserPicker_FloatingMenu_ReleasesTheAnchorByIdWhenTheListCloses()
    {
        var module = JSInterop.SetupModule(FloatingModulePath);
        module.SetupVoid("anchor", _ => true).SetVoidResult();
        module.SetupVoid("release", _ => true).SetVoidResult();

        var cut = RenderPicker(p => p
            .Add(x => x.FloatingMenu, true)
            .Add(x => x.Id, "owner-picker"));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "a" });
        cut.WaitForState(() => cut.FindAll(".tm-user-picker__result-item").Count > 0);
        module.Invocations.Should().NotContain(invocation => invocation.Identifier == "release");

        var anchorCall = module.Invocations.Single(invocation => invocation.Identifier == "anchor");
        anchorCall.Arguments.Should().HaveCount(3);
        anchorCall.Arguments[2].Should().Be("owner-picker-results");

        await cut.Find(".tm-user-picker__result-item").PointerDownAsync(new PointerEventArgs());
        cut.WaitForState(() => cut.FindAll("[role='listbox']").Count == 0);

        var releaseCall = module.Invocations.Single(invocation => invocation.Identifier == "release");
        releaseCall.Arguments.Should().ContainSingle().Which.Should().Be("owner-picker-results");
    }

    /// <summary>
    /// Turning the floating layer off while a list is open has to release it. The guard used to return
    /// early on <c>FloatingMenu</c>, so the script kept writing inline left/top/width onto a list that
    /// was no longer floating.
    /// </summary>
    [Fact]
    public async Task UserPicker_FloatingMenuTurnedOffWhileOpen_ReleasesTheAnchor()
    {
        var module = JSInterop.SetupModule(FloatingModulePath);
        module.SetupVoid("anchor", _ => true).SetVoidResult();
        module.SetupVoid("release", _ => true).SetVoidResult();

        var cut = RenderPicker(p => p
            .Add(x => x.FloatingMenu, true)
            .Add(x => x.Id, "owner-picker"));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "a" });
        cut.WaitForState(() => cut.FindAll(".tm-user-picker__result-item").Count > 0);

        cut.Render(p =>
        {
            p.Add(x => x.SearchProvider, SearchProvider());
            p.Add(x => x.ResolveProvider, ResolveProvider());
            p.Add(x => x.ValueSelector, (TestUser u) => u.Login);
            p.Add(x => x.DisplaySelector, (TestUser u) => u.DisplayName);
            p.Add(x => x.MinChars, 1);
            p.Add(x => x.Id, "owner-picker");
            p.Add(x => x.FloatingMenu, false);
        });

        cut.Find("[role='listbox']").ClassList.Should().NotContain("tm-user-picker__results--floating");
        var releaseCall = module.Invocations.Single(invocation => invocation.Identifier == "release");
        releaseCall.Arguments.Should().ContainSingle().Which.Should().Be("owner-picker-results");
    }

    /// <summary>
    /// Going floating must not silently drop the list's height cap: the script overwrites max-height on
    /// every placement, so the 240px from the stylesheet has to be honoured in the script too.
    /// </summary>
    [Fact]
    public void UserPickerFloatingScript_ClampsTheMenuToTheStylesheetMaxHeight()
    {
        var repositoryRoot = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            repositoryRoot, "src", "Tempo.Blazor", "Components", "Inputs", "TmUserPicker.razor.js"));
        var css = File.ReadAllText(Path.Combine(
            repositoryRoot, "src", "Tempo.Blazor", "Components", "Inputs", "TmUserPicker.razor.css"));

        css.Should().Contain("max-height: 240px;", "the script's cap has to mirror a real stylesheet value");
        script.Should().Contain("const MAX_HEIGHT = 240;");
        script.Should().Contain("MAX_HEIGHT)");
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

    private const string FloatingModulePath = "./_content/Tempo.Blazor/Components/Inputs/TmUserPicker.razor.js";
}
