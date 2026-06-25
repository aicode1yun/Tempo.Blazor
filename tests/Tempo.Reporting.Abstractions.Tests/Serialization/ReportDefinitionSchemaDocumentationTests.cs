using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tempo.Reporting.Abstractions.Definitions;

namespace Tempo.Reporting.Abstractions.Tests.Serialization;

public sealed class ReportDefinitionSchemaDocumentationTests
{
    [Fact]
    public void JsonSchema_DocumentsEveryRuntimeElementDiscriminator()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindSchemaPath()));
        var root = document.RootElement;
        var oneOf = root.GetProperty("$defs")
            .GetProperty("reportElement")
            .GetProperty("oneOf");

        var documented = oneOf
            .EnumerateArray()
            .Select(item => item.GetProperty("$ref").GetString())
            .Select(reference => reference!["#/$defs/".Length..])
            .Select(definitionName => root.GetProperty("$defs")
                .GetProperty(definitionName)
                .GetProperty("properties")
                .GetProperty("type")
                .GetProperty("const")
                .GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var runtime = typeof(ReportElement)
            .GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(attribute => Convert.ToString(attribute.TypeDiscriminator, System.Globalization.CultureInfo.InvariantCulture))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        documented.Should().Equal(runtime);
    }

    [Fact]
    public void JsonSchema_ContainsAuthoringDescriptionsForElementDefinitions()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindSchemaPath()));
        var defs = document.RootElement.GetProperty("$defs");
        foreach (var definitionName in new[]
        {
            "textBoxElement",
            "imageElement",
            "shapeElement",
            "lineElement",
            "tableElement",
            "chartElement",
            "subReportElement",
        })
        {
            defs.GetProperty(definitionName)
                .GetProperty("description")
                .GetString()
                .Should()
                .NotBeNullOrWhiteSpace();
        }
    }

    private static string FindSchemaPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "TempoBlazor.slnx")))
        {
            current = current.Parent;
        }

        current.Should().NotBeNull();
        return Path.Combine(
            current!.FullName,
            "src",
            "Tempo.Reporting.Abstractions",
            "docs",
            "report-definition.schema.json");
    }
}
