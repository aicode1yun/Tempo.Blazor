using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Abstractions.WorkItems;

namespace Tempo.Blazor.Tests.Shared;

public sealed class TmProviderCapabilityExtensionsTests
{
    [Fact]
    public void HasCapability_ReturnsTrueOnlyWhenAllRequestedFlagsArePresent()
    {
        var capabilities = SampleCapabilities.Read | SampleCapabilities.Update;

        capabilities.HasCapability(SampleCapabilities.Read).Should().BeTrue();
        capabilities.HasCapability(SampleCapabilities.Read | SampleCapabilities.Update).Should().BeTrue();
        capabilities.HasCapability(SampleCapabilities.Delete).Should().BeFalse();
        capabilities.HasCapability(SampleCapabilities.Update | SampleCapabilities.Delete).Should().BeFalse();
        capabilities.HasCapability(SampleCapabilities.None).Should().BeTrue();
    }

    [Fact]
    public void ProviderExtension_UsesAdvertisedCapabilities()
    {
        var provider = new SampleProvider(SampleCapabilities.Read | SampleCapabilities.Delete);

        provider.HasCapability(SampleCapabilities.Read).Should().BeTrue();
        provider.HasCapability(SampleCapabilities.Update).Should().BeFalse();
    }

    [Fact]
    public void WorkItemProvider_ImplementsSharedCapabilityContract()
    {
        var provider = new ReadOnlyWorkItemProvider();

        provider.Should().BeAssignableTo<ITmCapabilityProvider<TmWorkItemCapabilities>>();
        provider.HasCapability(TmWorkItemCapabilities.Read).Should().BeTrue();
        provider.HasCapability(TmWorkItemCapabilities.Update).Should().BeFalse();
    }

    [Flags]
    private enum SampleCapabilities
    {
        None = 0,
        Read = 1 << 0,
        Update = 1 << 1,
        Delete = 1 << 2
    }

    private sealed class SampleProvider(SampleCapabilities capabilities) : ITmCapabilityProvider<SampleCapabilities>
    {
        public SampleCapabilities Capabilities { get; } = capabilities;
    }

    private sealed class ReadOnlyWorkItemProvider : TmWorkItemProviderBase
    {
        public override string SourceKey => "readonly";

        public override string DisplayName => "Read only";

        public override Task<Tempo.Blazor.Models.PagedResult<TmWorkItem>> SearchAsync(
            TmWorkItemQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new Tempo.Blazor.Models.PagedResult<TmWorkItem>
            {
                Items = [],
                TotalCount = 0,
                Page = 1,
                PageSize = query.Take
            });
    }
}
