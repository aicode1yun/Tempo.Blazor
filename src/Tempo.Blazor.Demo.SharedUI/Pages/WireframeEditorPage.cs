// Partial class for WireframeEditorPage – holds string constants that contain
// angle-bracket characters, which confuse the Razor parser when placed in @code blocks.

namespace Tempo.Blazor.Demo.SharedUI.Pages;

public partial class WireframeEditorPage
{
    private const string _schemaHint =
        "{\n" +
        "  \"version\": \"1.0\",\n" +
        "  \"title\": \"My wireframe\",\n" +
        "  \"width\": 1280,\n" +
        "  \"height\": 800,\n" +
        "  \"elements\": [\n" +
        "    {\n" +
        "      \"id\": \"abc12345\",\n" +
        "      \"type\": \"TmButton\",\n" +
        "      \"x\": 100, \"y\": 200,\n" +
        "      \"w\": 120, \"h\": 36,\n" +
        "      \"zIndex\": 0,\n" +
        "      \"props\": {\n" +
        "        \"label\": \"Click me\",\n" +
        "        \"variant\": \"primary\"\n" +
        "      }\n" +
        "    }\n" +
        "  ],\n" +
        "  \"connectors\": []\n" +
        "}";

    private const string _providerCode =
        "// 1. Implement IWireframeComponentProvider\n" +
        "public class MarketingComponentProvider : IWireframeComponentProvider\n" +
        "{\n" +
        "    public string ProviderId => \"Marketing\";\n" +
        "    public int Priority => 10;   // higher than BuiltIn (0)\n" +
        "\n" +
        "    public IEnumerable<WireframeComponentDef> GetDefinitions()\n" +
        "    {\n" +
        "        yield return new WireframeComponentDef\n" +
        "        {\n" +
        "            Type         = \"HeroSection\",\n" +
        "            DisplayName  = \"Hero Section\",\n" +
        "            Category     = \"Marketing\",\n" +
        "            DefaultWidth = 820,\n" +
        "            DefaultHeight = 140,\n" +
        "            IsBuiltIn    = false,\n" +
        "            RenderSvg    = RenderHero,\n" +
        "            Props        = [ new PropDef { Name=\"heading\",    DisplayName=\"Heading\",    Type=PropType.String },\n" +
        "                             new PropDef { Name=\"subheading\", DisplayName=\"Subheading\", Type=PropType.String },\n" +
        "                             new PropDef { Name=\"showCta\",    DisplayName=\"Show CTA\",   Type=PropType.Bool   } ]\n" +
        "        };\n" +
        "        // ... FeatureCard, CtaBanner, FooterBar ...\n" +
        "    }\n" +
        "\n" +
        "    private static void RenderHero(WireframeElement el, RenderTreeBuilder b)\n" +
        "    {\n" +
        "        WireframeSvg.Rect(b, 0, 0, el.W, el.H, fill:\"#f0f9ff\", stroke:\"#bae6fd\");\n" +
        "        WireframeSvg.Text(b, el.GetString(\"heading\",\"Hero\"), 16, 32, fontSize:18, bold:true);\n" +
        "        WireframeSvg.Text(b, el.GetString(\"subheading\",\"\"), 16, 56, color:WireframeSvg.ColorMuted);\n" +
        "    }\n" +
        "}\n" +
        "\n" +
        "// 2. Register in DI (Program.cs / Startup.cs)\n" +
        "builder.Services.AddSingleton<IWireframeComponentProvider,\n" +
        "                              MarketingComponentProvider>();";
}
