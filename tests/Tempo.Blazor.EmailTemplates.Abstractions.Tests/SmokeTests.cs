using Tempo.Blazor.EmailTemplates.Abstractions;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests;

public class SmokeTests
{
    [Fact]
    public void AbstractionsAssembly_IsResolvable()
    {
        EmailTemplatesAbstractions.Assembly.GetName().Name
            .Should().Be("Tempo.Blazor.EmailTemplates.Abstractions");
    }
}
