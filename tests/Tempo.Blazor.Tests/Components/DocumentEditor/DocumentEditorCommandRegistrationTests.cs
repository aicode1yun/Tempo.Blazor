using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.Components.DocumentEditor.Registry;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>
/// Fáze 16: deklarativní DocumentEditorToolbarRegistry.IsAvailable item s nezaregistrovaným
/// CommandName tiše zahodí, zatímco ručně psaný toolbar (IsCommandEnabled, fallback !ReadOnly)
/// ho renderoval enabled — dvě cesty se chovaly opačně. Kontrakty:
/// (1) KAŽDÝ CommandName z built-in metadata je zaregistrovaný v editoru,
/// (2) route-only formátovací příkazy jsou zaregistrované (registry-driven enable/disable žije),
/// (3) sjednocený fallback: registry připojený + příkaz nezaregistrovaný ⇒ DISABLED
///     (deklarativní cesta skrývá); bez registry ⇒ historický fallback !ReadOnly.
/// </summary>
public sealed class DocumentEditorCommandRegistrationTests : LocalizationTestBase
{
    private const string InteropModulePath = "./_content/Tempo.Blazor.DocumentEditor/js/document-editor-canvas/interop.mjs";

    // ─── (1) Metadata → registry pokrytí ─────────────────────────────────────

    [Fact]
    public void EveryBuiltInToolbarCommand_IsRegisteredInEditorRegistry()
    {
        var registry = RenderEditorAndGetRegistry("cmd-coverage");

        var missing = DocumentEditorBuiltInToolbar.DefaultItems
            .Where(item => !string.IsNullOrEmpty(item.CommandName))
            .Select(item => item.CommandName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(name => !registry.TryGet(name, out _))
            .ToList();

        missing.Should().BeEmpty(
            "deklarativní IsAvailable itemy s nezaregistrovaným CommandName tiše zahodí — každý built-in item musí mít registrovaný příkaz");
    }

    [Theory]
    [InlineData("changeCase")]
    [InlineData("superscript")]
    [InlineData("subscript")]
    [InlineData("smallCaps")]
    [InlineData("allCaps")]
    [InlineData("doubleStrikethrough")]
    [InlineData("increaseFontSize")]
    [InlineData("decreaseFontSize")]
    [InlineData("spacingBefore")]
    [InlineData("spacingAfter")]
    [InlineData("alignLeft")]
    [InlineData("alignCenter")]
    [InlineData("alignRight")]
    [InlineData("alignJustify")]
    [InlineData("insertEquation")]
    [InlineData("insertSymbol")]
    [InlineData("showRuler")]
    [InlineData("zoomPageWidth")]
    [InlineData("differentFirstPage")]
    [InlineData("differentOddEven")]
    [InlineData("closeHeaderFooter")]
    public void RouteOnlyAndMetadataCommands_AreRegistered_WithState(string commandName)
    {
        var registry = RenderEditorAndGetRegistry($"cmd-{commandName.ToLowerInvariant()}");

        registry.TryGet(commandName, out _).Should().BeTrue($"příkaz '{commandName}' musí být registrovaný");
        registry.GetState(commandName).Should().NotBeNull($"stav příkazu '{commandName}' musí být viditelný přes GetState");
    }

    // ─── (2) Metadata fixy ───────────────────────────────────────────────────

    [Fact]
    public void SpacingItems_UseOwnCommandNames_NotLineSpacing()
    {
        var byId = DocumentEditorBuiltInToolbar.DefaultItems.ToDictionary(i => i.Id);
        byId["spacingBefore"].CommandName.Should().Be("spacingBefore", "sdílení lineSpacing stavu ukazovalo cizí hodnotu");
        byId["spacingAfter"].CommandName.Should().Be("spacingAfter");
    }

    [Fact]
    public void FindItemByCommandName_InsertEquation_ResolvesDeterministicallyToInsertTabItem()
    {
        // Insert i Math tab sdílí příkaz insertEquation (jeden příkaz, dvě umístění) — first-match
        // mapa musí deterministicky vracet Insert-tab item (Id == CommandName), aby overflow menu
        // ukázalo příkaz právě jednou s Insert metadaty.
        var item = DocumentEditorBuiltInToolbar.FindItemByCommandName("insertEquation");
        item.Should().NotBeNull();
        item!.Id.Should().Be("insertEquation");
        item.Tab.Should().Be(DocumentToolbarTab.Insert);
    }

    // ─── (3) Sjednocený fallback ─────────────────────────────────────────────

    [Fact]
    public void Toolbar_WithRegistry_UnregisteredCommandRendersDisabled()
    {
        // Registry připojený, ale bold v něm chybí → tlačítko musí být DISABLED (shodné s deklarativní
        // cestou, která item skryje) — dřív fallback !ReadOnly renderoval enabled a klik prošel.
        var registry = new DocumentEditorCommandRegistry();
        registry.RefreshAllAsync(new DocumentEditorCommandContext()).GetAwaiter().GetResult();

        var cut = Render<TmDocumentEditorToolbar>(p => p
            .Add(x => x.CommandRegistry, registry));

        var button = cut.Find("[data-testid='document-bold']");
        button.HasAttribute("disabled").Should().BeTrue(
            "registry je zdroj pravdy — nezaregistrovaný příkaz nesmí být klikatelný");
    }

    [Fact]
    public void Toolbar_WithoutRegistry_KeepsHistoricalReadOnlyFallback()
    {
        var cut = Render<TmDocumentEditorToolbar>();

        cut.Find("[data-testid='document-bold']").HasAttribute("disabled").Should().BeFalse(
            "bez registry platí historický fallback !ReadOnly (žádný breaking change pro standalone toolbar)");

        var cutReadOnly = Render<TmDocumentEditorToolbar>(p => p
            .Add(x => x.ReadOnly, true));
        cutReadOnly.Find("[data-testid='document-bold']").HasAttribute("disabled").Should().BeTrue();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private DocumentEditorCommandRegistry RenderEditorAndGetRegistry(string documentId)
    {
        SetupCanvasModule();
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument(documentId);

        var cut = Render<TmDocumentEditor>(parameters => parameters
            .Add(p => p.DocumentId, documentId)
            .Add(p => p.Provider, provider));

        cut.WaitForAssertion(() => cut.Find("[data-testid='document-canvas-engine-host']").Should().NotBeNull());
        var toolbar = cut.FindComponent<TmDocumentEditorToolbar>();
        toolbar.Instance.CommandRegistry.Should().NotBeNull();
        return toolbar.Instance.CommandRegistry!;
    }

    private void SetupCanvasModule()
    {
        var module = JSInterop.SetupModule(InteropModulePath);
        module.Setup<string>("mount", _ => true).SetResult("canvas-host-test-handle");
        module.Setup<bool>("isDirty", _ => true).SetResult(false);
        module.SetupVoid("markSaved", _ => true).SetVoidResult();
        module.SetupVoid("focus", _ => true).SetVoidResult();
        module.Setup<string?>("getFormattingStateJson", _ => true).SetResult("""{"bold":false,"alignment":"left"}""");
        module.Setup<string?>("getUndoStateJson", _ => true).SetResult("""{"canUndo":false,"canRedo":false}""");
        module.Setup<string?>("getSelectionStateJson", _ => true).SetResult("""{"isCollapsed":true}""");
        module.Setup<string?>("getDiagnosticsJson", _ => true).SetResult("""{"architectureName":"CanvasDocumentEngine"}""");
        module.SetupVoid("dispose", _ => true).SetVoidResult();
    }
}
