using Tempo.Blazor.Components.Diagram.Templates;

namespace Tempo.Blazor.Tests.Components.Diagram;

public class DiagramTemplateRegistryTests
{
    [Fact]
    public void RegisterTemplate_Adds_Template()
    {
        var registry = new DiagramTemplateRegistry();
        var template = new DiagramTemplate { Id = "t1", Name = "Test", Category = "General" };

        registry.RegisterTemplate(template);

        registry.GetTemplate("t1").Should().Be(template);
        registry.Count.Should().Be(1);
    }

    [Fact]
    public void RegisterTemplate_Skips_Lower_Priority_Duplicate()
    {
        var registry = new DiagramTemplateRegistry();
        var t1 = new DiagramTemplate { Id = "t1", Name = "First", Category = "General" };
        var t2 = new DiagramTemplate { Id = "t1", Name = "Second", Category = "General" };

        registry.RegisterTemplate(t1, priority: 10);
        registry.RegisterTemplate(t2, priority: 5);

        registry.GetTemplate("t1")!.Name.Should().Be("First");
    }

    [Fact]
    public void RegisterTemplate_Overrides_Higher_Priority_Duplicate()
    {
        var registry = new DiagramTemplateRegistry();
        var t1 = new DiagramTemplate { Id = "t1", Name = "First", Category = "General" };
        var t2 = new DiagramTemplate { Id = "t1", Name = "Second", Category = "General" };

        registry.RegisterTemplate(t1, priority: 5);
        registry.RegisterTemplate(t2, priority: 10);

        registry.GetTemplate("t1")!.Name.Should().Be("Second");
    }

    [Fact]
    public void GetCategories_Returns_Distinct_Sorted_Categories()
    {
        var registry = new DiagramTemplateRegistry();
        registry.RegisterTemplate(new DiagramTemplate { Id = "b", Name = "B", Category = "Beta" });
        registry.RegisterTemplate(new DiagramTemplate { Id = "a", Name = "A", Category = "Alpha" });
        registry.RegisterTemplate(new DiagramTemplate { Id = "a2", Name = "A2", Category = "Alpha" });

        var categories = registry.GetCategories();
        categories.Should().Equal("Alpha", "Beta");
    }

    [Fact]
    public void GetByCategory_Returns_Templates_In_Category_Sorted_By_Name()
    {
        var registry = new DiagramTemplateRegistry();
        registry.RegisterTemplate(new DiagramTemplate { Id = "b", Name = "Banana", Category = "Fruits" });
        registry.RegisterTemplate(new DiagramTemplate { Id = "a", Name = "Apple", Category = "Fruits" });
        registry.RegisterTemplate(new DiagramTemplate { Id = "c", Name = "Carrot", Category = "Vegetables" });

        var fruits = registry.GetByCategory("Fruits").ToList();
        fruits.Should().HaveCount(2);
        fruits.Select(t => t.Name).Should().Equal("Apple", "Banana");
    }

    [Fact]
    public void GetAll_Returns_All_Templates_Sorted_By_Category_Then_Name()
    {
        var registry = new DiagramTemplateRegistry();
        registry.RegisterTemplate(new DiagramTemplate { Id = "b", Name = "Beta", Category = "B" });
        registry.RegisterTemplate(new DiagramTemplate { Id = "a", Name = "Alpha", Category = "A" });
        registry.RegisterTemplate(new DiagramTemplate { Id = "a2", Name = "Alpha2", Category = "A" });

        var all = registry.GetAll().ToList();
        all.Select(t => t.Id).Should().Equal("a", "a2", "b");
    }

    [Fact]
    public async Task RegisterProvider_Adds_All_Provider_Templates()
    {
        var registry = new DiagramTemplateRegistry();
        var provider = new TestTemplateProvider();

        registry.RegisterProvider(provider);
        await registry.InitializeAsync();

        registry.Count.Should().Be(2);
        registry.GetTemplate("p1")!.Name.Should().Be("Provider T1");
    }

    private sealed class TestTemplateProvider : IDiagramTemplateProvider
    {
        public int Priority => 0;

        public Task<IEnumerable<DiagramTemplateCategory>> GetTemplateCategoriesAsync()
        {
            return Task.FromResult<IEnumerable<DiagramTemplateCategory>>(new List<DiagramTemplateCategory>
            {
                new()
                {
                    Name = "Cat1",
                    Templates =
                    [
                        new DiagramTemplate { Id = "p1", Name = "Provider T1", Category = "Cat1" },
                        new DiagramTemplate { Id = "p2", Name = "Provider T2", Category = "Cat1" }
                    ]
                }
            });
        }
    }
}
