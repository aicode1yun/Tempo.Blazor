using System.Text.Json;
using Tempo.Blazor.Modeling;

namespace Tempo.Blazor.Tests.Modeling;

public sealed class ModelingProviderContractTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Provider_contract_can_be_implemented_without_blazor_assembly_dependency()
    {
        IModelingModelProvider provider = new TestModelingModelProvider();

        var model = await provider.GetModelAsync(
            new ModelingModelRequest
            {
                ProviderKey = provider.ProviderKey,
                SourceKind = "demo",
                SourceId = "source-1"
            },
            CancellationToken.None);

        provider.ProviderKey.Should().Be("test-provider");
        model.Id.Should().Be("test-model");

        var referencedAssemblies = typeof(IModelingModelProvider)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        referencedAssemblies.Should().NotContain("Tempo.Blazor");
        referencedAssemblies.Should().NotContain("Microsoft.AspNetCore.Components");
    }

    [Fact]
    public void Request_with_null_filter_options_keeps_dictionary_safe_to_access()
    {
        const string json = """
            {
              "providerKey": "demo",
              "sourceKind": "workspace",
              "sourceId": "source-1",
              "filterOptions": null
            }
            """;

        var request = JsonSerializer.Deserialize<ModelingModelRequest>(json, Options)!;

        request.FilterOptions.Should().NotBeNull();
        request.FilterOptions.Should().BeEmpty();
        request.FilterOptions.TryGetValue("missing", out _).Should().BeFalse();
    }

    [Fact]
    public void Generation_result_with_empty_issues_and_null_document_serializes_without_failure()
    {
        var result = new ModelingDiagramGenerationResultDto
        {
            Document = null,
            GeneratedAt = new DateTimeOffset(2026, 6, 6, 9, 15, 0, TimeSpan.Zero)
        };

        var json = JsonSerializer.Serialize(result, Options);
        var restored = JsonSerializer.Deserialize<ModelingDiagramGenerationResultDto>(json, Options)!;

        restored.Document.Should().BeNull();
        restored.Issues.Should().BeEmpty();
        restored.GeneratedAt.Should().Be(result.GeneratedAt);

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty(nameof(ModelingDiagramGenerationResultDto.Document)).ValueKind
            .Should().Be(JsonValueKind.Null);
        document.RootElement.GetProperty(nameof(ModelingDiagramGenerationResultDto.Issues)).GetArrayLength()
            .Should().Be(0);
    }

    private sealed class TestModelingModelProvider : IModelingModelProvider
    {
        public string ProviderKey => "test-provider";

        public Task<ModelingModelDto> GetModelAsync(ModelingModelRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new ModelingModelDto
            {
                Id = "test-model",
                Title = "Test model",
                Notation = request.Notation
            });
        }
    }
}
