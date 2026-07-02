using Tempo.Blazor.Components.Wireframe.Stencil;

namespace Tempo.Blazor.Tests.Wireframe;

public class StencilTokenResolverTests
{
    [Fact]
    public void Resolve_ElementOverride_Wins()
    {
        var resolver = Resolver(
            elementOverrides: new Dictionary<string, string> { ["palette.primary"] = "#111111" },
            documentTheme: new Dictionary<string, string> { ["palette.primary"] = "#222222" },
            packTheme: new Dictionary<string, string> { ["palette.primary"] = "#333333" },
            packDefaults: new Dictionary<string, string> { ["palette.primary"] = "#444444" });

        resolver.Resolve("palette.primary", "#555555").Should().Be("#111111");
    }

    [Fact]
    public void Resolve_DocumentTheme_WinsAfterMissingOverride()
    {
        var resolver = Resolver(
            documentTheme: new Dictionary<string, string> { ["palette.primary"] = "#222222" },
            packTheme: new Dictionary<string, string> { ["palette.primary"] = "#333333" },
            packDefaults: new Dictionary<string, string> { ["palette.primary"] = "#444444" });

        resolver.Resolve("palette.primary", "#555555").Should().Be("#222222");
    }

    [Fact]
    public void Resolve_PackTheme_WinsAfterMissingDocumentTheme()
    {
        var resolver = Resolver(
            packTheme: new Dictionary<string, string> { ["palette.primary"] = "#333333" },
            packDefaults: new Dictionary<string, string> { ["palette.primary"] = "#444444" });

        resolver.Resolve("palette.primary", "#555555").Should().Be("#333333");
    }

    [Fact]
    public void Resolve_PackDefault_WinsAfterMissingTheme()
    {
        var resolver = Resolver(
            packDefaults: new Dictionary<string, string> { ["palette.primary"] = "#444444" });

        resolver.Resolve("palette.primary", "#555555").Should().Be("#444444");
    }

    [Fact]
    public void Resolve_LiteralFallback_WinsAfterMissingToken()
    {
        Resolver().Resolve("palette.primary", "#555555").Should().Be("#555555");
    }

    [Fact]
    public void Resolve_TokenLessPack_PassesLiteralThrough()
    {
        Resolver().Resolve("12px", "12px").Should().Be("12px");
    }

    [Fact]
    public void Resolve_MissingKeyWithoutFallback_ReturnsEmptyString()
    {
        Resolver().Resolve("palette.missing").Should().Be(string.Empty);
    }

    [Fact]
    public void Resolve_HugeUnknownKey_NeverThrowsAndReturnsFallback()
    {
        var resolver = Resolver();
        var key = new string('x', 40000);
        var act = () => resolver.Resolve(key, "literal");

        act.Should().NotThrow();
        resolver.Resolve(key, "literal").Should().Be("literal");
    }

    [Fact]
    public void Evaluate_TokenCall_UsesResolver()
    {
        var resolver = Resolver(
            packDefaults: new Dictionary<string, string> { ["palette.primary"] = "#444444" });
        var context = new StencilEvalContext(
            Props: new Dictionary<string, object?>(),
            SizeW: 0,
            SizeH: 0,
            RepeatIndex: 0,
            Tokens: resolver);

        new StencilEvaluator()
            .Evaluate("token(\"palette.primary\", \"#555555\")", context)
            .AsString()
            .Should()
            .Be("#444444");
    }

    private static StencilTokenResolver Resolver(
        IReadOnlyDictionary<string, string>? elementOverrides = null,
        IReadOnlyDictionary<string, string>? documentTheme = null,
        IReadOnlyDictionary<string, string>? packTheme = null,
        IReadOnlyDictionary<string, string>? packDefaults = null)
    {
        return new StencilTokenResolver(elementOverrides, documentTheme, packTheme, packDefaults);
    }
}
