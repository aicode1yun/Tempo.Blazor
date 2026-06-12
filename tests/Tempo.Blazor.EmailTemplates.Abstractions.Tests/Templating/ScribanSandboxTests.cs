using Tempo.Blazor.EmailTemplates.Abstractions.Templating;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests.Templating;

public class ScribanSandboxTests
{
    [Fact]
    public void LoopLimit_AbortsRunawayForLoop()
    {
        var engine = new ScribanTemplateEngine(new TemplateSecurityOptions { LoopLimit = 10 });

        var result = engine.Render("{{ for i in 1..100000 }}x{{ end }}", new { });

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void LoopLimit_AbortsInfiniteWhile_NoHang()
    {
        var engine = new ScribanTemplateEngine(new TemplateSecurityOptions { LoopLimit = 100 });

        var result = engine.Render("{{ while true }}{{ end }}", new { });

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void RecursiveLimit_AbortsInfiniteRecursion()
    {
        var engine = new ScribanTemplateEngine(new TemplateSecurityOptions { RecursiveLimit = 20, LoopLimit = 100000 });

        var result = engine.Render("{{ func recurse(n); recurse(n + 1); end; recurse 0 }}", new { });

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void MaxOutputLength_AbortsExcessiveOutput()
    {
        var engine = new ScribanTemplateEngine(new TemplateSecurityOptions { MaxOutputLength = 50, LoopLimit = 100000 });

        var result = engine.Render("{{ for i in 1..1000 }}xxxxxxxxxx{{ end }}", new { });

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Include_IsDisabled()
    {
        var engine = new ScribanTemplateEngine();

        var result = engine.Render("{{ include 'secret.txt' }}", new { });

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Reflection_IsNotReachable()
    {
        var engine = new ScribanTemplateEngine();

        // Accessing .NET methods/types yields nothing (no reflection exposed), never the type name.
        var result = engine.Render("[{{ x.get_type }}{{ x.GetType }}]", new { X = 5 });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("[]");
        result.Value.Should().NotContain("Int32");
    }
}
