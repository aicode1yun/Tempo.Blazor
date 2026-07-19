using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Rendering;
using Tempo.Blazor.Components.DataTable;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.Models;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.DataTable;

/// <summary>Column pinning, resize, and layout persistence tests.</summary>
public class TmDataTablePinResizeTests : LocalizationTestBase
{
    public record Person(string Name, string Dept, int Age);

    private static void Col(RenderTreeBuilder b, ref int seq, string title, Func<Person, object?> field, bool resizable = false)
    {
        b.OpenComponent<TmDataTableColumn<Person>>(seq++);
        b.AddAttribute(seq++, "Title", title);
        b.AddAttribute(seq++, "PropertyName", title);
        b.AddAttribute(seq++, "Field", field);
        if (resizable) b.AddAttribute(seq++, "Resizable", true);
        b.CloseComponent();
    }

    private IRenderedComponent<TmDataTable<Person>> Render(IDataTableLayoutStore? store = null, string ctx = "pin-test")
        => Render<TmDataTable<Person>>(p =>
        {
            p.Add(c => c.ViewContext, ctx);
            if (store is not null) p.Add(c => c.LayoutStore, store);
            p.Add(c => c.Items, new List<Person> { new("Ann", "A", 1), new("Bob", "B", 2) });
            p.AddChildContent(b =>
            {
                var seq = 0;
                Col(b, ref seq, "Name", x => x.Name, resizable: true);
                Col(b, ref seq, "Dept", x => x.Dept);
                Col(b, ref seq, "Age", x => x.Age);
            });
        });

    private static IElement HeaderFor(IRenderedComponent<TmDataTable<Person>> cut, string title)
        => cut.FindAll("thead th").First(h => h.TextContent.Contains(title));

    [Fact]
    public async Task PinColumn_AddsStickyClassAndReordersFirst()
    {
        var cut = Render();

        await cut.InvokeAsync(() => cut.Instance.SetColumnPinAsync("Dept", ColumnPin.Left));

        var first = cut.FindAll("thead th")[0];
        first.TextContent.Should().Contain("Dept");
        first.ClassList.Should().Contain("tm-col-pinned-left");
        first.GetAttribute("style").Should().Contain("position:sticky");
    }

    [Fact]
    public async Task PinColumn_PersistsToLayoutStore()
    {
        var store = new InMemoryDataTableLayoutStore();
        var cut = Render(store);

        await cut.InvokeAsync(() => cut.Instance.SetColumnPinAsync("Dept", ColumnPin.Right));

        var saved = await store.LoadLayoutAsync("pin-test");
        saved.Should().NotBeNull();
        saved!.ColumnPins["Dept"].Should().Be(ColumnPin.Right);
    }

    [Fact]
    public async Task PinColumn_FiresLayoutChanged()
    {
        DataTableLayout? captured = null;
        var cut = Render<TmDataTable<Person>>(p =>
        {
            p.Add(c => c.ViewContext, "pin-test");
            p.Add(c => c.LayoutChanged, Microsoft.AspNetCore.Components.EventCallback.Factory.Create<DataTableLayout>(this, l => captured = l));
            p.Add(c => c.Items, new List<Person> { new("Ann", "A", 1) });
            p.AddChildContent(b =>
            {
                var seq = 0;
                Col(b, ref seq, "Name", x => x.Name);
                Col(b, ref seq, "Dept", x => x.Dept);
            });
        });

        await cut.InvokeAsync(() => cut.Instance.SetColumnPinAsync("Dept", ColumnPin.Left));

        captured.Should().NotBeNull();
        captured!.ColumnPins["Dept"].Should().Be(ColumnPin.Left);
    }

    [Fact]
    public async Task ResizeColumn_SetsWidthAndPersists()
    {
        var store = new InMemoryDataTableLayoutStore();
        var cut = Render(store);

        await cut.InvokeAsync(() => cut.Instance.SetColumnWidthAsync("Name", 250));

        HeaderFor(cut, "Name").GetAttribute("style").Should().Contain("width:250px");
        (await store.LoadLayoutAsync("pin-test"))!.ColumnWidths["Name"].Should().Be(250);
    }

    [Fact]
    public async Task ResizeColumn_ClampsToMinimum()
    {
        var cut = Render();

        await cut.InvokeAsync(() => cut.Instance.SetColumnWidthAsync("Name", 5));

        HeaderFor(cut, "Name").GetAttribute("style").Should().Contain("width:60px");
    }

    [Fact]
    public async Task LayoutStore_LoadedOnInit_AppliesPinAndWidth()
    {
        var store = new InMemoryDataTableLayoutStore();
        await store.SaveLayoutAsync("pin-test", new DataTableLayout
        {
            ColumnWidths = new() { ["Name"] = 300 },
            ColumnPins = new() { ["Dept"] = ColumnPin.Left }
        });

        var cut = Render(store);
        await cut.InvokeAsync(() => { });
        cut.Render();

        var headers = cut.FindAll("thead th");
        headers[0].TextContent.Should().Contain("Dept");
        headers[0].ClassList.Should().Contain("tm-col-pinned-left");
        HeaderFor(cut, "Name").GetAttribute("style").Should().Contain("width:300px");
    }

    [Fact]
    public void PinButton_Click_CyclesPin()
    {
        var cut = Render();

        cut.Find("[data-testid='pin-Dept']").Click();

        HeaderFor(cut, "Dept").ClassList.Should().Contain("tm-col-pinned-left");
    }

    [Fact]
    public void ResizeHandle_RenderedForResizableColumns()
    {
        var cut = Render();

        cut.FindAll("[data-testid='resize-Name']").Should().ContainSingle();
        cut.FindAll("[data-testid='resize-Dept']").Should().BeEmpty();
    }

    [Fact]
    public void OnColumnResized_JsCallback_AppliesWidth()
    {
        var cut = Render();

        cut.InvokeAsync(() => cut.Instance.OnColumnResized("Age", 180));
        cut.Render();

        HeaderFor(cut, "Age").GetAttribute("style").Should().Contain("width:180px");
    }
}
