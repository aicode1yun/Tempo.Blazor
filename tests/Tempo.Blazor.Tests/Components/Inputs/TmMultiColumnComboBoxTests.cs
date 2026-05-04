using Bunit;
using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Inputs;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Inputs;

public class TmMultiColumnComboBoxTests : LocalizationTestBase
{
    private record Product(int Id, string Name, string Category, decimal Price);

    private static IReadOnlyList<Product> GetProducts() => new List<Product>
    {
        new(1, "Laptop", "Electronics", 1200m),
        new(2, "Mouse", "Electronics", 25m),
        new(3, "Desk", "Furniture", 300m),
        new(4, "Chair", "Furniture", 150m),
    };

    private static IReadOnlyList<MultiColumnComboBoxColumn<Product>> GetColumns() => new List<MultiColumnComboBoxColumn<Product>>
    {
        new() { Title = "Name", Field = p => p.Name },
        new() { Title = "Category", Field = p => p.Category },
        new() { Title = "Price", Field = p => p.Price, Width = "80px" },
    };

    [Fact]
    public void TmMultiColumnComboBox_Renders_Trigger()
    {
        var cut = RenderComponent<TmMultiColumnComboBox<Product, int>>(p => p
            .Add(c => c.Data, GetProducts())
            .Add(c => c.ValueField, p => p.Id)
            .Add(c => c.TextField, p => p.Name)
            .Add(c => c.Columns, GetColumns()));

        cut.Find(".tm-multi-column-combo-box__trigger").Should().NotBeNull();
    }

    [Fact]
    public void TmMultiColumnComboBox_Placeholder_Shown_When_No_Value()
    {
        var cut = RenderComponent<TmMultiColumnComboBox<Product, int>>(p => p
            .Add(c => c.Data, GetProducts())
            .Add(c => c.ValueField, p => p.Id)
            .Add(c => c.TextField, p => p.Name)
            .Add(c => c.Columns, GetColumns()));

        cut.Find(".tm-multi-column-combo-box__placeholder").TextContent.Should().Contain("Select an item");
    }

    [Fact]
    public void TmMultiColumnComboBox_Click_Opens_Dropdown()
    {
        var cut = RenderComponent<TmMultiColumnComboBox<Product, int>>(p => p
            .Add(c => c.Data, GetProducts())
            .Add(c => c.ValueField, p => p.Id)
            .Add(c => c.TextField, p => p.Name)
            .Add(c => c.Columns, GetColumns()));

        cut.Find(".tm-multi-column-combo-box__trigger").Click();

        cut.Find(".tm-multi-column-combo-box__dropdown").Should().NotBeNull();
    }

    [Fact]
    public void TmMultiColumnComboBox_Dropdown_Shows_Grid_With_Columns()
    {
        var cut = RenderComponent<TmMultiColumnComboBox<Product, int>>(p => p
            .Add(c => c.Data, GetProducts())
            .Add(c => c.ValueField, p => p.Id)
            .Add(c => c.TextField, p => p.Name)
            .Add(c => c.Columns, GetColumns()));

        cut.Find(".tm-multi-column-combo-box__trigger").Click();

        var headers = cut.FindAll(".tm-multi-column-combo-box__th");
        headers.Count.Should().Be(3);
        headers[0].TextContent.Should().Be("Name");
        headers[1].TextContent.Should().Be("Category");
        headers[2].TextContent.Should().Be("Price");
    }

    [Fact]
    public void TmMultiColumnComboBox_Dropdown_Shows_Data_Rows()
    {
        var cut = RenderComponent<TmMultiColumnComboBox<Product, int>>(p => p
            .Add(c => c.Data, GetProducts())
            .Add(c => c.ValueField, p => p.Id)
            .Add(c => c.TextField, p => p.Name)
            .Add(c => c.Columns, GetColumns()));

        cut.Find(".tm-multi-column-combo-box__trigger").Click();

        var rows = cut.FindAll(".tm-multi-column-combo-box__tr");
        rows.Count.Should().Be(4);
    }

    [Fact]
    public void TmMultiColumnComboBox_Select_Row_Sets_Value()
    {
        int? selectedValue = null;

        var cut = RenderComponent<TmMultiColumnComboBox<Product, int>>(p => p
            .Add(c => c.Data, GetProducts())
            .Add(c => c.ValueField, p => p.Id)
            .Add(c => c.TextField, p => p.Name)
            .Add(c => c.Columns, GetColumns())
            .Add(c => c.ValueChanged, v => selectedValue = v));

        cut.Find(".tm-multi-column-combo-box__trigger").Click();
        var rows = cut.FindAll(".tm-multi-column-combo-box__tr");
        rows[0].Click();

        selectedValue.Should().Be(1);
    }

    [Fact]
    public void TmMultiColumnComboBox_Selected_Row_Has_Selected_Class()
    {
        var cut = RenderComponent<TmMultiColumnComboBox<Product, int>>(p => p
            .Add(c => c.Data, GetProducts())
            .Add(c => c.ValueField, p => p.Id)
            .Add(c => c.TextField, p => p.Name)
            .Add(c => c.Columns, GetColumns())
            .Add(c => c.Value, 2));

        cut.Find(".tm-multi-column-combo-box__trigger").Click();

        var rows = cut.FindAll(".tm-multi-column-combo-box__tr");
        rows[1].ClassList.Should().Contain("tm-multi-column-combo-box__tr--selected");
    }

    [Fact]
    public void TmMultiColumnComboBox_Filter_Reduces_Rows()
    {
        var cut = RenderComponent<TmMultiColumnComboBox<Product, int>>(p => p
            .Add(c => c.Data, GetProducts())
            .Add(c => c.ValueField, p => p.Id)
            .Add(c => c.TextField, p => p.Name)
            .Add(c => c.Columns, GetColumns()));

        cut.Find(".tm-multi-column-combo-box__trigger").Click();

        // Filter input
        var filterInput = cut.Find(".tm-multi-column-combo-box__filter input");
        filterInput.Input("Furn");

        var rows = cut.FindAll(".tm-multi-column-combo-box__tr");
        rows.Count.Should().Be(2); // Desk, Chair
    }

    [Fact]
    public void TmMultiColumnComboBox_Clear_Button_Clears_Value()
    {
        int? selectedValue = 999;

        var cut = RenderComponent<TmMultiColumnComboBox<Product, int>>(p => p
            .Add(c => c.Data, GetProducts())
            .Add(c => c.ValueField, p => p.Id)
            .Add(c => c.TextField, p => p.Name)
            .Add(c => c.Columns, GetColumns())
            .Add(c => c.Value, 1)
            .Add(c => c.ValueChanged, v => selectedValue = v));

        cut.Find(".tm-multi-column-combo-box__clear").Click();

        selectedValue.Should().Be(0); // default(int)
    }

    [Fact]
    public void TmMultiColumnComboBox_Disabled_Hides_Interactions()
    {
        var cut = RenderComponent<TmMultiColumnComboBox<Product, int>>(p => p
            .Add(c => c.Data, GetProducts())
            .Add(c => c.ValueField, p => p.Id)
            .Add(c => c.TextField, p => p.Name)
            .Add(c => c.Columns, GetColumns())
            .Add(c => c.Disabled, true));

        cut.Find(".tm-multi-column-combo-box--disabled").Should().NotBeNull();
    }

    [Fact]
    public void TmMultiColumnComboBox_Custom_Class_Applied()
    {
        var cut = RenderComponent<TmMultiColumnComboBox<Product, int>>(p => p
            .Add(c => c.Data, GetProducts())
            .Add(c => c.ValueField, p => p.Id)
            .Add(c => c.TextField, p => p.Name)
            .Add(c => c.Columns, GetColumns())
            .Add(c => c.Class, "my-combo"));

        cut.Find(".tm-multi-column-combo-box").ClassList.Should().Contain("my-combo");
    }

    [Fact]
    public void TmMultiColumnComboBox_NoResults_Shown_When_Filter_Matches_Nothing()
    {
        var cut = RenderComponent<TmMultiColumnComboBox<Product, int>>(p => p
            .Add(c => c.Data, GetProducts())
            .Add(c => c.ValueField, p => p.Id)
            .Add(c => c.TextField, p => p.Name)
            .Add(c => c.Columns, GetColumns()));

        cut.Find(".tm-multi-column-combo-box__trigger").Click();

        var filterInput = cut.Find(".tm-multi-column-combo-box__filter input");
        filterInput.Input("XYZ");

        cut.Find(".tm-multi-column-combo-box__empty").TextContent.Should().Contain("No results");
    }
}
