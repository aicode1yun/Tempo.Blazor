using Bunit;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Modeling;
using Tempo.Blazor.Modeling;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Modeling;

public sealed class TmModelingSourcePanelTests : LocalizationTestBase
{
    [Fact]
    public void Clicking_load_model_raises_on_load_requested()
    {
        var requested = false;
        using var cut = Render<TmModelingSourcePanel>(parameters => parameters
            .Add(p => p.Metadata, CreateMetadata(isFresh: true))
            .Add(p => p.OnLoadRequested, EventCallback.Factory.Create(this, () => requested = true)));

        cut.Find("[data-testid='modeling-source-load-button']").Click();

        requested.Should().BeTrue();
    }

    [Fact]
    public void Stale_metadata_shows_warning_with_icon()
    {
        using var cut = Render<TmModelingSourcePanel>(parameters => parameters
            .Add(p => p.Metadata, CreateMetadata(isFresh: false)));

        var warning = cut.Find("[data-testid='modeling-source-freshness-warning']");

        warning.Should().NotBeNull();
        warning.QuerySelector(".tm-modeling-source-panel__warning-icon").Should().NotBeNull();
    }

    [Fact]
    public void Fresh_metadata_does_not_show_stale_warning()
    {
        using var cut = Render<TmModelingSourcePanel>(parameters => parameters
            .Add(p => p.Metadata, CreateMetadata(isFresh: true)));

        cut.FindAll("[data-testid='modeling-source-freshness-warning']").Should().BeEmpty();
    }

    [Fact]
    public void Null_metadata_renders_empty_state_without_exception()
    {
        using var cut = Render<TmModelingSourcePanel>(parameters => parameters
            .Add(p => p.Metadata, (ModelingMetadataDto?)null));

        cut.Find("[data-testid='modeling-source-panel']").Should().NotBeNull();
        cut.Find("[data-testid='modeling-source-empty']").Should().NotBeNull();
    }

    [Fact]
    public void Long_source_system_uses_wrapping_value_element()
    {
        var sourceSystem = new string('A', 200);
        var metadata = CreateMetadata(isFresh: true);
        metadata.SourceSystem = sourceSystem;

        using var cut = Render<TmModelingSourcePanel>(parameters => parameters
            .Add(p => p.Metadata, metadata));

        var sourceSystemElement = cut.Find("[data-testid='modeling-source-system']");

        sourceSystemElement.TextContent.Should().Be(sourceSystem);
        sourceSystemElement.GetAttribute("title").Should().Be(sourceSystem);
        sourceSystemElement.ClassList.Should().Contain("tm-modeling-source-panel__value");
    }

    private static ModelingMetadataDto CreateMetadata(bool isFresh) => new()
    {
        SourceSystem = "Tempo tests",
        SourceVersion = "1.0",
        LoadedAt = new DateTimeOffset(2026, 6, 6, 8, 0, 0, TimeSpan.Zero),
        IsFresh = isFresh
    };
}
