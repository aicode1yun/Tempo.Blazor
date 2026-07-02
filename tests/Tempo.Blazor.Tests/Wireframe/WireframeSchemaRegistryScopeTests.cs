using FluentAssertions;
using Tempo.Blazor.Components.Wireframe;
using Tempo.Blazor.Components.Wireframe.Models;
using Xunit;

namespace Tempo.Blazor.Tests.Wireframe;

public class WireframeSchemaRegistryScopeTests
{
    [Fact]
    public void GetAll_WithScope_ReturnsBuiltInsAndMatchingScopedSchemasOnly()
    {
        var appA = Guid.NewGuid().ToString("D");
        var appB = Guid.NewGuid().ToString("D");
        var registry = new WireframeSchemaRegistry(
        [
            new TestSchemaSource("BuiltIn", 0, Schema("TmButton", "Buttons", "Button", isBuiltIn: true)),
            new TestSchemaSource("Legacy", 10, Schema("LegacyCustom")),
            new ScopedSchemaSource("A", 10, appA, Schema("InvoiceCard", displayName: "A Invoice")),
            new ScopedSchemaSource("B", 10, appB, Schema("InvoiceCard", displayName: "B Invoice"))
        ]);

        var scoped = registry.GetAll(WireframeComponentScope.ForApp(appA)).ToList();

        scoped.Select(s => s.Type).Should().BeEquivalentTo(
        [
            "TmButton",
            $"app:{appA}:InvoiceCard"
        ]);
        scoped.Should().NotContain(s => s.Type == "LegacyCustom");
        scoped.Should().NotContain(s => s.ScopeAppId == appB);
    }

    [Fact]
    public void GetSchema_WithScope_ResolvesLocalTypeAndRejectsOtherAppType()
    {
        var appA = Guid.NewGuid().ToString("D");
        var appB = Guid.NewGuid().ToString("D");
        var registry = new WireframeSchemaRegistry(
        [
            new TestSchemaSource("BuiltIn", 0, Schema("TmButton", "Buttons", "Button", isBuiltIn: true)),
            new ScopedSchemaSource("A", 10, appA, Schema("InvoiceCard", displayName: "A Invoice")),
            new ScopedSchemaSource("B", 10, appB, Schema("InvoiceCard", displayName: "B Invoice"))
        ]);

        var appASchema = registry.GetSchema("InvoiceCard", WireframeComponentScope.ForApp(appA));

        appASchema.Should().NotBeNull();
        appASchema!.Type.Should().Be($"app:{appA}:InvoiceCard");
        appASchema.LocalType.Should().Be("InvoiceCard");
        registry.GetSchema($"app:{appB}:InvoiceCard", WireframeComponentScope.ForApp(appA)).Should().BeNull();
        registry.GetSchema("TmButton", WireframeComponentScope.ForApp(appA)).Should().NotBeNull();
    }

    [Fact]
    public void GetAll_WithoutScope_PreservesUnscopedCustomBehavior()
    {
        var appA = Guid.NewGuid().ToString("D");
        var registry = new WireframeSchemaRegistry(
        [
            new TestSchemaSource("Legacy", 10, Schema("LegacyCustom")),
            new ScopedSchemaSource("A", 10, appA, Schema("InvoiceCard"))
        ]);

        registry.GetAll().Select(s => s.Type).Should().BeEquivalentTo(["LegacyCustom"]);
        registry.GetSchema("LegacyCustom").Should().NotBeNull();
    }

    [Fact]
    public void GetAll_WithTargetPacks_HidesUndeclaredAppPacks()
    {
        var appA = Guid.NewGuid().ToString("D");
        var appB = Guid.NewGuid().ToString("D");
        var registry = new WireframeSchemaRegistry(
        [
            new TestSchemaSource("BuiltIn", 0, Schema("TmButton", "Buttons", "Button", isBuiltIn: true)),
            new ScopedSchemaSource("A", 10, appA, Schema("InvoiceCard", displayName: "A Invoice")),
            new ScopedSchemaSource("B", 10, appB, Schema("InvoiceCard", displayName: "B Invoice"))
        ]);
        var scope = WireframeComponentScope.ForApp(appA);

        registry.GetAll(scope, ["tempo"])
            .Select(s => s.Type)
            .Should().BeEquivalentTo(["TmButton"]);
        registry.GetSchema("InvoiceCard", scope, ["tempo"]).Should().BeNull();

        registry.GetAll(scope, ["tempo", $"app:{appA}"])
            .Select(s => s.Type)
            .Should().BeEquivalentTo(["TmButton", $"app:{appA}:InvoiceCard"]);
        registry.GetSchema("InvoiceCard", scope, ["tempo", $"app:{appA}"])
            .Should().NotBeNull();
    }

    private static WireframeComponentSchema Schema(
        string type,
        string category = "Custom",
        string? displayName = null,
        bool isBuiltIn = false)
        => new()
        {
            Type = type,
            Category = category,
            DisplayName = displayName ?? type,
            IsBuiltIn = isBuiltIn,
            Props = []
        };

    private sealed class TestSchemaSource(string id, int priority, params WireframeComponentSchema[] schemas)
        : IWireframeSchemaSource
    {
        public string SourceId => id;
        public int Priority => priority;
        public IEnumerable<WireframeComponentSchema> GetSchemas() => schemas;
    }

    private sealed class ScopedSchemaSource(
        string id,
        int priority,
        string scopeAppId,
        params WireframeComponentSchema[] schemas)
        : IWireframeScopedSchemaSource
    {
        public string SourceId => id;
        public int Priority => priority;
        public string ScopeAppId => scopeAppId;
        public IEnumerable<WireframeComponentSchema> GetSchemas() => schemas;
    }
}
