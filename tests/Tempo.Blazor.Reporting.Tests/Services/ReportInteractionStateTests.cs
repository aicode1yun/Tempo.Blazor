using Tempo.Blazor.Reporting.Services;

namespace Tempo.Blazor.Reporting.Tests.Services;

public sealed class ReportInteractionStateTests
{
    [Fact]
    public void Toggle_AddsAndRemovesKeysInStableOrder()
    {
        var token = ReportInteractionState.Toggle(null, "b");
        token = ReportInteractionState.Toggle(token, "a");

        token.Should().Be("a,b");
        ReportInteractionState.Contains(token, "a").Should().BeTrue();

        ReportInteractionState.Toggle(token, "a").Should().Be("b");
    }
}
