using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Wireframe;

namespace Tempo.Blazor.Tests.Wireframe;

public sealed class WireframeSchemaServiceExtensionsTests
{
    [Fact]
    public void AddWireframeSchemas_RegistersSchemasAndUiRoleVocabulary()
    {
        var services = new ServiceCollection();

        services.AddWireframeSchemas();

        using var provider = services.BuildServiceProvider();
        var schemaRegistry = provider.GetRequiredService<WireframeSchemaRegistry>();
        var vocabulary = provider.GetRequiredService<UiRoleVocabulary>();

        schemaRegistry.GetSchema("TmButton").Should().NotBeNull();
        vocabulary.Find("TmSearchBox")!.Slug.Should().Be("search-input");
    }
}
