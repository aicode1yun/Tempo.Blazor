using System.Text.Json.Serialization;
using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor;

/// <summary>
/// Documentation drift guard (phase 6 of the headless document runtime): the canonical-model and
/// operation-semantics references are the foundation MCP agents build on, so every operation
/// type, block content type, inline content type and inline mark type that exists in code MUST
/// have a corresponding mention in the committed docs. Adding a new member without documenting
/// it fails this gate.
/// </summary>
public sealed class DocumentModelDocumentationDriftTests
{
    private static string ReadDoc(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        var path = Path.Combine(directory!.FullName, "docs", fileName);
        File.Exists(path).Should().BeTrue($"docs/{fileName} must be committed");
        return File.ReadAllText(path);
    }

    [Fact]
    public void EveryOperationType_IsDocumentedInTheSemanticsReference()
    {
        var doc = ReadDoc("document-operations-semantics.md");

        foreach (var name in Enum.GetNames<DocumentOperationType>())
        {
            var wireName = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(name);
            doc.Should().Contain($"`{wireName}`",
                $"operation type '{name}' must be documented in docs/document-operations-semantics.md");
        }
    }

    [Fact]
    public void EveryBlockContentType_IsDocumentedInTheModelReference()
    {
        var doc = ReadDoc("document-canonical-model.md");

        foreach (var discriminator in Discriminators(typeof(DocumentBlockContent)))
        {
            doc.Should().Contain($"`{discriminator}`",
                $"block content type '{discriminator}' must be documented in docs/document-canonical-model.md");
        }
    }

    [Fact]
    public void EveryInlineContentType_IsDocumentedInTheModelReference()
    {
        var doc = ReadDoc("document-canonical-model.md");

        foreach (var discriminator in Discriminators(typeof(InlineContent)))
        {
            doc.Should().Contain($"`{discriminator}`",
                $"inline content type '{discriminator}' must be documented in docs/document-canonical-model.md");
        }
    }

    [Fact]
    public void EveryInlineMarkType_IsDocumentedInTheModelReference()
    {
        var doc = ReadDoc("document-canonical-model.md");

        foreach (var name in Enum.GetNames<InlineMarkType>())
        {
            var wireName = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(name);
            doc.Should().Contain($"`{wireName}`",
                $"inline mark type '{name}' must be documented in docs/document-canonical-model.md");
        }
    }

    [Fact]
    public void EverySetBlockAttributeName_SupportedByTheApplier_IsDocumented()
    {
        // The applier's attribute vocabulary is part of the operation contract for MCP tooling.
        var doc = ReadDoc("document-operations-semantics.md");

        foreach (var attribute in new[]
                 {
                     "headingLevel", "text", "paragraphProperties", "clearFormatting",
                     "table.cell.text", "order", "metadata.title",
                 })
        {
            doc.Should().Contain($"`{attribute}`",
                $"setBlockAttribute attribute '{attribute}' must be documented");
        }
    }

    private static IEnumerable<string> Discriminators(Type baseType)
        => baseType
            .GetCustomAttributes(typeof(JsonDerivedTypeAttribute), inherit: false)
            .Cast<JsonDerivedTypeAttribute>()
            .Select(attribute => attribute.TypeDiscriminator?.ToString() ?? string.Empty)
            .Where(discriminator => discriminator.Length > 0);
}
