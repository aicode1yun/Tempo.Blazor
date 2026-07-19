using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Maps;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Maps;

public class TmMapTests : LocalizationTestBase
{
    private const string ModulePath = "./_content/Tempo.Blazor.Maps/js/map.js";

    private BunitJSModuleInterop SetupMapModule()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.Mode = JSRuntimeMode.Loose;
        return module;
    }

    // ── MAP-1: render zobrazí .tm-map kontejner s aria-label a role ────────

    [Fact]
    public void Render_DisplaysMapContainer()
    {
        SetupMapModule();

        var cut = Render<TmMap>();

        var root = cut.Find(".tm-map");
        root.Should().NotBeNull();
        root.GetAttribute("role").Should().Be("application");
        root.GetAttribute("aria-label").Should().NotBeNullOrEmpty();
    }

    // ── MAP-2: root má data-testid a inner container s unikátním id ────────

    [Fact]
    public void Render_HasTestIdAndInnerContainerWithId()
    {
        SetupMapModule();

        var cut = Render<TmMap>();

        cut.Find("[data-testid='map']").Should().NotBeNull();
        var container = cut.Find(".tm-map__container");
        container.GetAttribute("id").Should().NotBeNullOrEmpty();
    }

    // ── MAP-3: init je voláno s centrem a zoomem ───────────────────────────

    [Fact]
    public void Init_IsInvokedWithCenterAndZoom()
    {
        var module = SetupMapModule();

        var cut = Render<TmMap>(parameters => parameters
            .Add(p => p.CenterLatitude, 50.08)
            .Add(p => p.CenterLongitude, 14.43)
            .Add(p => p.Zoom, 12.0));

        var initInvocation = module.Invocations.Single(i => i.Identifier == "init");
        initInvocation.Arguments.Should().Contain(50.08);
        initInvocation.Arguments.Should().Contain(14.43);
        initInvocation.Arguments.Should().Contain(12.0);
    }

    // ── MAP-4: init dostane DotNetObjectReference pro callbacky ────────────

    [Fact]
    public void Init_ReceivesDotNetObjectReference()
    {
        var module = SetupMapModule();

        var cut = Render<TmMap>();

        var initInvocation = module.Invocations.Single(i => i.Identifier == "init");
        initInvocation.Arguments.Should().Contain(a => a is DotNetObjectReference<TmMap>);
    }

    // ── MAP-5: markery jsou předány do setData (atomická výměna vrstev) ────

    [Fact]
    public void Markers_ArePassedToSetData()
    {
        var module = SetupMapModule();
        var markers = new List<MapMarker>
        {
            new("m1", 50.0, 14.0, "Prague"),
            new("m2", 49.2, 16.6, "Brno"),
        };

        var cut = Render<TmMap>(parameters => parameters
            .Add(p => p.Markers, markers));

        module.Invocations.Should().Contain(i => i.Identifier == "setData");
    }

    // ── MAP-6: 0 markerů → setData se při initu nevolá ─────────────────────

    [Fact]
    public void EmptyMarkers_DoNotInvokeSetData()
    {
        var module = SetupMapModule();

        var cut = Render<TmMap>(parameters => parameters
            .Add(p => p.Markers, new List<MapMarker>()));

        module.Invocations.Should().NotContain(i => i.Identifier == "setData");
    }

    // ── MAP-7: duplicitní Id markerů se deduplikují (první vyhrává) ────────

    [Fact]
    public async Task DuplicateMarkerIds_AreDeduplicated()
    {
        var module = SetupMapModule();
        var markers = new List<MapMarker>
        {
            new("dup", 50.0, 14.0, "First"),
            new("dup", 49.2, 16.6, "Second"),
            new(null, 48.9, 14.4, "NoId"),
            new(null, 48.8, 14.3, "NoId2"),
        };

        var cut = Render<TmMap>(parameters => parameters
            .Add(p => p.Markers, markers));

        var batch = module.Invocations.Single(i => i.Identifier == "setData");
        var payload = (MapDataPayload)batch.Arguments[1]!;
        // duplicate non-null id filtered (first wins), null ids all kept
        payload.Markers.Count.Should().Be(3);
        payload.Markers.Single(m => m.Id == "dup").Title.Should().Be("First");
    }

    // ── MAP-8: DisposeAsync volá JS dispose ────────────────────────────────

    [Fact]
    public async Task DisposeAsync_InvokesJsDispose()
    {
        var module = SetupMapModule();
        var cut = Render<TmMap>();

        await cut.Instance.DisposeAsync();

        module.Invocations.Should().Contain(i => i.Identifier == "dispose");
    }

    // ── MAP-9: TmMap implementuje IAsyncDisposable ─────────────────────────

    [Fact]
    public void TmMap_ImplementsIAsyncDisposable()
    {
        typeof(IAsyncDisposable).IsAssignableFrom(typeof(TmMap)).Should().BeTrue();
    }

    // ── MAP-10: JSDisconnectedException při dispose se ignoruje ────────────

    [Fact]
    public async Task DisposeAsync_SwallowsJSDisconnectedException()
    {
        var module = SetupMapModule();
        module.SetupVoid("dispose", _ => true)
              .SetException(new JSDisconnectedException("circuit down"));
        var cut = Render<TmMap>();

        var act = async () => await cut.Instance.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    // ── MAP-11: opakovaný DisposeAsync je bezpečný ─────────────────────────

    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        SetupMapModule();
        var cut = Render<TmMap>();

        await cut.Instance.DisposeAsync();
        var act = async () => await cut.Instance.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    // ── MAP-12: HandleMarkerClick vyvolá OnMarkerClick s MapMarker ─────────

    [Fact]
    public async Task HandleMarkerClick_RaisesOnMarkerClick()
    {
        SetupMapModule();
        MapMarker? received = null;
        var cut = Render<TmMap>(parameters => parameters
            .Add(p => p.OnMarkerClick, EventCallback.Factory.Create<MapMarker>(
                this, m => received = m)));

        await cut.InvokeAsync(() => cut.Instance.HandleMarkerClick("m1", 50.0, 14.0));

        received.Should().NotBeNull();
        received!.Id.Should().Be("m1");
        received.Latitude.Should().Be(50.0);
        received.Longitude.Should().Be(14.0);
    }

    // ── MAP-13: HandleZoomChanged vyvolá OnZoomChanged s MapViewport ───────

    [Fact]
    public async Task HandleZoomChanged_RaisesOnZoomChanged()
    {
        SetupMapModule();
        MapViewport? received = null;
        var cut = Render<TmMap>(parameters => parameters
            .Add(p => p.OnZoomChanged, EventCallback.Factory.Create<MapViewport>(
                this, v => received = v)));

        await cut.InvokeAsync(() => cut.Instance.HandleZoomChanged(10.0, 49.8, 15.5));

        received.Should().NotBeNull();
        received!.Zoom.Should().Be(10.0);
        received.Latitude.Should().Be(49.8);
        received.Longitude.Should().Be(15.5);
    }

    // ── MAP-14: HandleMapClick vyvolá OnMapClick s MapViewport ─────────────

    [Fact]
    public async Task HandleMapClick_RaisesOnMapClick()
    {
        SetupMapModule();
        MapViewport? received = null;
        var cut = Render<TmMap>(parameters => parameters
            .Add(p => p.OnMapClick, EventCallback.Factory.Create<MapViewport>(
                this, v => received = v)));

        await cut.InvokeAsync(() => cut.Instance.HandleMapClick(50.1, 14.4, 13.0));

        received.Should().NotBeNull();
        received!.Latitude.Should().Be(50.1);
        received.Longitude.Should().Be(14.4);
        received.Zoom.Should().Be(13.0);
    }

    // ── MAP-15: SetViewAsync volá JS setView ───────────────────────────────

    [Fact]
    public async Task SetViewAsync_InvokesJsSetView()
    {
        var module = SetupMapModule();
        var cut = Render<TmMap>();

        await cut.Instance.SetViewAsync(new MapViewport(49.5, 15.0, 9.0));

        var invocation = module.Invocations.Single(i => i.Identifier == "setView");
        invocation.Arguments.Should().Contain(49.5);
        invocation.Arguments.Should().Contain(15.0);
        invocation.Arguments.Should().Contain(9.0);
    }

    // ── MAP-16: GetViewportAsync čte viewport z JS ─────────────────────────

    [Fact]
    public async Task GetViewportAsync_ReturnsViewportFromJs()
    {
        var module = SetupMapModule();
        module.Setup<MapViewport>("getViewport", _ => true)
              .SetResult(new MapViewport(49.8, 15.4, 7.5));
        var cut = Render<TmMap>();

        var viewport = await cut.Instance.GetViewportAsync();

        viewport.Should().NotBeNull();
        viewport!.Latitude.Should().Be(49.8);
        viewport.Longitude.Should().Be(15.4);
        viewport.Zoom.Should().Be(7.5);
    }

    // ── MAP-17: ClearMarkersAsync volá JS clearMarkers ─────────────────────

    [Fact]
    public async Task ClearMarkersAsync_InvokesJsClearMarkers()
    {
        var module = SetupMapModule();
        var cut = Render<TmMap>();

        await cut.Instance.ClearMarkersAsync();

        module.Invocations.Should().Contain(i => i.Identifier == "clearMarkers");
    }

    // ── MAP-18: InvalidateSizeAsync volá JS invalidateSize (skrytý tab) ────

    [Fact]
    public async Task InvalidateSizeAsync_InvokesJsInvalidateSize()
    {
        var module = SetupMapModule();
        var cut = Render<TmMap>();

        await cut.Instance.InvalidateSizeAsync();

        module.Invocations.Should().Contain(i => i.Identifier == "invalidateSize");
    }

    // ── MAP-19: metody po dispose už JS nevolají a nevyhazují ──────────────

    [Fact]
    public async Task MethodsAfterDispose_DoNotThrow()
    {
        var module = SetupMapModule();
        var cut = Render<TmMap>();
        await cut.Instance.DisposeAsync();
        var invocationCountAfterDispose = module.Invocations
            .Count(i => i.Identifier != "dispose" && i.Identifier != "init");

        await cut.Instance.InvalidateSizeAsync();
        await cut.Instance.ClearMarkersAsync();
        await cut.Instance.SetViewAsync(new MapViewport(49.0, 15.0, 8.0));

        module.Invocations
            .Count(i => i.Identifier != "dispose" && i.Identifier != "init")
            .Should().Be(invocationCountAfterDispose);
    }

    // ── MAP-20: změna Markers parametru atomicky vymění vrstvy (setData) ───

    [Fact]
    public void ChangedMarkers_AtomicallyReplaceViaSetData()
    {
        var module = SetupMapModule();
        var cut = Render<TmMap>(parameters => parameters
            .Add(p => p.Markers, new List<MapMarker> { new("a", 50.0, 14.0, null) }));

        cut.Render(parameters => parameters
            .Add(p => p.Markers, new List<MapMarker> { new("b", 49.0, 16.0, null) }));

        // Atomic swap: single setData per data change, no separate clear + add.
        module.Invocations.Should().NotContain(i => i.Identifier == "clearMarkers");
        module.Invocations.Count(i => i.Identifier == "setData").Should().Be(2);
    }

    // ── MAP-21: Height parametr se propíše do stylu ────────────────────────

    [Fact]
    public void Height_IsAppliedToRootStyle()
    {
        SetupMapModule();

        var cut = Render<TmMap>(parameters => parameters
            .Add(p => p.Height, "555px"));

        cut.Find(".tm-map").GetAttribute("style").Should().Contain("555px");
    }

    // ── MAP-22: Class parametr přidá CSS třídu ─────────────────────────────

    [Fact]
    public void Class_IsAppliedToRoot()
    {
        SetupMapModule();

        var cut = Render<TmMap>(parameters => parameters
            .Add(p => p.Class, "my-custom-map"));

        cut.Find(".tm-map").ClassList.Should().Contain("my-custom-map");
    }
}
