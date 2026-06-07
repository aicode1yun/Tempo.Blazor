using Microsoft.Extensions.Logging;
using Tempo.Blazor.Components.Modeling;
using Tempo.Blazor.Modeling;

namespace Tempo.Blazor.Tests.Modeling;

public sealed class ModelingNotationProfileRegistryTests
{
    [Fact]
    public void Registry_returns_profile_by_key()
    {
        var profile = new TestNotationProfile("bpmn", "BPMN");
        var registry = new ModelingNotationProfileRegistry([profile]);

        registry.GetProfile("bpmn").Should().Be(profile);
        registry.GetProfile("BPMN").Should().Be(profile);
    }

    [Fact]
    public void Unknown_key_returns_null()
    {
        var registry = new ModelingNotationProfileRegistry([new TestNotationProfile("bpmn", "BPMN")]);

        registry.GetProfile("archimate").Should().BeNull();
        registry.GetProfile(null!).Should().BeNull();
        registry.GetProfile(" ").Should().BeNull();
    }

    [Fact]
    public void Empty_registry_returns_null_for_any_key()
    {
        var registry = new ModelingNotationProfileRegistry([]);

        registry.Count.Should().Be(0);
        registry.GetProfile("bpmn").Should().BeNull();
    }

    [Fact]
    public void Duplicate_notation_key_logs_warning_and_uses_first_profile()
    {
        var first = new TestNotationProfile("bpmn", "First BPMN");
        var second = new TestNotationProfile("BPMN", "Second BPMN");
        var logger = new CaptureLogger<ModelingNotationProfileRegistry>();

        var registry = new ModelingNotationProfileRegistry([first, second], logger);

        registry.GetProfile("bpmn").Should().Be(first);
        registry.Count.Should().Be(1);
        logger.Messages.Should().Contain(message =>
            message.Level == LogLevel.Warning
            && message.Text.Contains("Duplicate modeling notation profile key", StringComparison.Ordinal));
    }

    [Fact]
    public void Relationship_rules_return_false_for_null_arguments()
    {
        var rules = new ModelingRelationshipRulesProvider(new ModelingNotationProfileRegistry(
        [
            new TestNotationProfile(
                "bpmn",
                "BPMN",
                supportedElementTypes: ["task", "event"],
                supportedRelationshipTypes: ["sequenceFlow"])
        ]));

        rules.IsValidRelationship(null!, "task", "event", "sequenceFlow").Should().BeFalse();
        rules.IsValidRelationship("bpmn", null!, "event", "sequenceFlow").Should().BeFalse();
        rules.IsValidRelationship("bpmn", "task", null!, "sequenceFlow").Should().BeFalse();
        rules.IsValidRelationship("bpmn", "task", "event", null!).Should().BeFalse();
    }

    [Fact]
    public void Relationship_rules_validate_supported_profile_values()
    {
        var rules = new ModelingRelationshipRulesProvider(new ModelingNotationProfileRegistry(
        [
            new TestNotationProfile(
                "bpmn",
                "BPMN",
                supportedElementTypes: ["task", "event"],
                supportedRelationshipTypes: ["sequenceFlow"])
        ]));

        rules.IsValidRelationship("bpmn", "task", "event", "sequenceFlow").Should().BeTrue();
        rules.IsValidRelationship("bpmn", "task", "gateway", "sequenceFlow").Should().BeFalse();
        rules.IsValidRelationship("bpmn", "task", "event", "association").Should().BeFalse();
        rules.IsValidRelationship("unknown", "task", "event", "sequenceFlow").Should().BeFalse();
    }

    [Fact]
    public void Built_in_relationship_dispatcher_uses_custom_notation_rules_provider()
    {
        var registry = new ModelingNotationProfileRegistry(
        [
            new TestNotationProfile(
                "custom",
                "Custom",
                supportedElementTypes: ["source", "target"],
                supportedRelationshipTypes: ["customFlow"])
        ]);
        var rules = new BuiltInModelingRelationshipRulesProvider(registry, [new CustomNotationRulesProvider()]);

        rules.IsValidRelationship("custom", "source", "target", "customFlow").Should().BeTrue();
        rules.IsValidRelationship("custom", "source", "target", "profileOnly").Should().BeFalse();
    }

    [Fact]
    public void Viewpoint_rules_return_false_for_unknown_viewpoint()
    {
        var rules = new ModelingViewpointRulesProvider(new ModelingNotationProfileRegistry(
        [
            new TestNotationProfile(
                "archimate",
                "ArchiMate",
                supportedElementTypes: ["businessActor", "applicationComponent"],
                supportedViewpointKeys: ["layered"])
        ]));

        rules.IsElementAllowedInViewpoint("archimate", "layered", "businessActor").Should().BeTrue();
        rules.IsElementAllowedInViewpoint("archimate", "motivation", "businessActor").Should().BeFalse();
        rules.IsElementAllowedInViewpoint("archimate", "layered", "businessProcess").Should().BeFalse();
        rules.IsElementAllowedInViewpoint("unknown", "layered", "businessActor").Should().BeFalse();
    }

    [Fact]
    public void Viewpoint_rules_return_false_for_null_arguments()
    {
        var rules = new ModelingViewpointRulesProvider(new ModelingNotationProfileRegistry(
        [
            new TestNotationProfile(
                "archimate",
                "ArchiMate",
                supportedElementTypes: ["businessActor"],
                supportedViewpointKeys: ["layered"])
        ]));

        rules.IsElementAllowedInViewpoint(null!, "layered", "businessActor").Should().BeFalse();
        rules.IsElementAllowedInViewpoint("archimate", null!, "businessActor").Should().BeFalse();
        rules.IsElementAllowedInViewpoint("archimate", "layered", null!).Should().BeFalse();
    }

    private sealed class TestNotationProfile : IModelingNotationProfile
    {
        public TestNotationProfile(
            string notationKey,
            string displayName,
            IReadOnlyCollection<string>? supportedElementTypes = null,
            IReadOnlyCollection<string>? supportedRelationshipTypes = null,
            IReadOnlyCollection<string>? supportedViewpointKeys = null)
        {
            NotationKey = notationKey;
            DisplayName = displayName;
            SupportedElementTypes = supportedElementTypes ?? [];
            SupportedRelationshipTypes = supportedRelationshipTypes ?? [];
            SupportedViewpointKeys = supportedViewpointKeys ?? [];
        }

        public string NotationKey { get; }

        public string DisplayName { get; }

        public IReadOnlyCollection<string> SupportedElementTypes { get; }

        public IReadOnlyCollection<string> SupportedRelationshipTypes { get; }

        public IReadOnlyCollection<string> SupportedViewpointKeys { get; }
    }

    private sealed class CustomNotationRulesProvider : IModelingNotationRelationshipRulesProvider
    {
        public IReadOnlyCollection<string> NotationKeys { get; } = ["custom"];

        public bool IsValidRelationship(string notationKey, string sourceType, string targetType, string relationshipType)
            => string.Equals(notationKey, "custom", StringComparison.OrdinalIgnoreCase)
                && string.Equals(sourceType, "source", StringComparison.Ordinal)
                && string.Equals(targetType, "target", StringComparison.Ordinal)
                && string.Equals(relationshipType, "customFlow", StringComparison.Ordinal);
    }

    private sealed class CaptureLogger<T> : ILogger<T>
    {
        public List<CapturedLogMessage> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
            => NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(new CapturedLogMessage(logLevel, formatter(state, exception)));
        }
    }

    private sealed record CapturedLogMessage(LogLevel Level, string Text);

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
