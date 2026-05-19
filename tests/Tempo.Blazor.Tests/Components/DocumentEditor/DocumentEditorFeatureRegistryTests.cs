using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor.Features;
using Tempo.Blazor.Components.DocumentEditor.Registry;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class DocumentEditorFeatureRegistryTests
{
    [Fact]
    public void FeatureInterface_ExposesExpectedExtensionPoints()
    {
        var members = typeof(IDocumentEditorFeature)
            .GetMembers()
            .Select(member => member.Name)
            .ToArray();

        members.Should().Contain([
            nameof(IDocumentEditorFeature.Name),
            nameof(IDocumentEditorFeature.Requires),
            nameof(IDocumentEditorFeature.RegisterCommands),
            nameof(IDocumentEditorFeature.RegisterToolbar),
            nameof(IDocumentEditorFeature.RegisterShortcuts),
            nameof(IDocumentEditorFeature.RegisterFloatingUi),
            nameof(IDocumentEditorFeature.ConfigureSchema)
        ]);
    }

    [Fact]
    public void GetOrderedFeatures_AllowsFeatureWithoutDependencies()
    {
        var registry = new DocumentEditorFeatureRegistry();
        registry.Register(new RecordingFeature("text"));

        registry.GetOrderedFeatures()
            .Select(feature => feature.Name)
            .Should()
            .Equal("text");
    }

    [Fact]
    public void Register_DuplicateFeatureName_ThrowsClearError()
    {
        var registry = new DocumentEditorFeatureRegistry();
        registry.Register(new RecordingFeature("text"));

        var act = () => registry.Register(new RecordingFeature("TEXT"));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*TEXT*already registered*");
    }

    [Fact]
    public void GetOrderedFeatures_MissingDependency_ThrowsFeatureAndDependencyNames()
    {
        var registry = new DocumentEditorFeatureRegistry();
        registry.Register(new RecordingFeature("table", "schema"));

        var act = () => registry.GetOrderedFeatures();

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*table*schema*");
    }

    [Fact]
    public void GetOrderedFeatures_ReturnsDependenciesBeforeDependents()
    {
        var registry = new DocumentEditorFeatureRegistry();
        registry.Register(new RecordingFeature("table", "schema"));
        registry.Register(new RecordingFeature("schema"));
        registry.Register(new RecordingFeature("image", "schema"));

        registry.GetOrderedFeatures()
            .Select(feature => feature.Name)
            .Should()
            .Equal("schema", "table", "image");
    }

    [Fact]
    public void Registry_ExposesTryGetGetRequiredAndGetAll()
    {
        var feature = new RecordingFeature("image");
        var registry = new DocumentEditorFeatureRegistry();
        registry.Register(feature);

        registry.TryGet("IMAGE", out var found).Should().BeTrue();
        found.Should().BeSameAs(feature);
        registry.GetRequired("image").Should().BeSameAs(feature);
        registry.GetAll().Should().ContainSingle().Which.Should().BeSameAs(feature);
    }

    [Fact]
    public void Bootstrap_CallsSchemaCommandsToolbarShortcutsAndFloatingUiInOrder()
    {
        var calls = new List<string>();
        var registry = new DocumentEditorFeatureRegistry();
        registry.Register(new RecordingFeature("base", calls: calls));
        registry.Register(new RecordingFeature("child", calls, "base"));

        registry.Bootstrap(new DocumentEditorFeatureBootstrapContext(
            new DocumentEditorCommandRegistry(),
            new DocumentEditorToolbarRegistry(),
            new DocumentEditorShortcutRegistry(),
            new DocumentFloatingUiRegistry(),
            new DocumentEditorSchemaBuilder()));

        calls.Should().Equal(
            "base.schema",
            "child.schema",
            "base.commands",
            "child.commands",
            "base.toolbar",
            "child.toolbar",
            "base.shortcuts",
            "child.shortcuts",
            "base.floating",
            "child.floating");
    }

    [Theory]
    [InlineData(typeof(TextFormattingFeature), DocumentEditorFeatureNames.TextFormatting)]
    [InlineData(typeof(ParagraphFeature), DocumentEditorFeatureNames.Paragraph)]
    [InlineData(typeof(ClipboardFeature), DocumentEditorFeatureNames.Clipboard)]
    [InlineData(typeof(FindReplaceFeature), DocumentEditorFeatureNames.FindReplace)]
    [InlineData(typeof(ImageFeature), DocumentEditorFeatureNames.Image)]
    [InlineData(typeof(TableFeature), DocumentEditorFeatureNames.Table)]
    [InlineData(typeof(CommentsFeature), DocumentEditorFeatureNames.Comments)]
    [InlineData(typeof(TrackChangesFeature), DocumentEditorFeatureNames.TrackChanges)]
    [InlineData(typeof(HeadersFootersFeature), DocumentEditorFeatureNames.HeadersFooters)]
    [InlineData(typeof(ImportExportFeature), DocumentEditorFeatureNames.ImportExport)]
    [InlineData(typeof(RestrictedEditingFeature), DocumentEditorFeatureNames.RestrictedEditing)]
    [InlineData(typeof(OfflineCollaborationFeature), DocumentEditorFeatureNames.OfflineCollaboration)]
    public void BuiltInFeatureSkeletons_ExposeStableNames(Type featureType, string expectedName)
    {
        var feature = Activator.CreateInstance(featureType).Should().BeAssignableTo<IDocumentEditorFeature>().Subject;

        feature.Name.Should().Be(expectedName);
    }

    [Fact]
    public void BuiltInDefaults_ContainAllExpectedFeatures()
    {
        DocumentEditorBuiltInFeatures.CreateDefaultRegistry()
            .GetAll()
            .Select(feature => feature.Name)
            .Should()
            .BeEquivalentTo([
                DocumentEditorFeatureNames.TextFormatting,
                DocumentEditorFeatureNames.Paragraph,
                DocumentEditorFeatureNames.Clipboard,
                DocumentEditorFeatureNames.FindReplace,
                DocumentEditorFeatureNames.Image,
                DocumentEditorFeatureNames.Table,
                DocumentEditorFeatureNames.Comments,
                DocumentEditorFeatureNames.TrackChanges,
                DocumentEditorFeatureNames.HeadersFooters,
                DocumentEditorFeatureNames.ImportExport,
                DocumentEditorFeatureNames.RestrictedEditing,
                DocumentEditorFeatureNames.OfflineCollaboration
            ]);
    }

    private sealed class RecordingFeature : IDocumentEditorFeature
    {
        private readonly List<string>? _calls;

        public RecordingFeature(string name, params string[] requires)
            : this(name, null, requires)
        {
        }

        public RecordingFeature(string name, List<string>? calls, params string[] requires)
        {
            Name = name;
            Requires = requires;
            _calls = calls;
        }

        public string Name { get; }

        public IReadOnlyList<string> Requires { get; }

        public void RegisterCommands(DocumentEditorCommandRegistry commands) => _calls?.Add($"{Name}.commands");

        public void RegisterToolbar(DocumentEditorToolbarRegistry toolbar) => _calls?.Add($"{Name}.toolbar");

        public void RegisterShortcuts(DocumentEditorShortcutRegistry shortcuts) => _calls?.Add($"{Name}.shortcuts");

        public void RegisterFloatingUi(DocumentFloatingUiRegistry floatingUi) => _calls?.Add($"{Name}.floating");

        public void ConfigureSchema(DocumentEditorSchemaBuilder schema) => _calls?.Add($"{Name}.schema");
    }
}
