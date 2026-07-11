using Bunit;
using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

/// <summary>
/// Overflow measurement controller: C# strana kontraktu SetOverflowingAsync historicky volala
/// window global `tmDocumentEditorToolbar.*`, který nikde neexistoval (výjimka se tiše spolkla)
/// — More/overflow menu se tak v aplikaci nikdy neukázalo. Nově toolbar importuje ES modul
/// toolbar-overflow.mjs (stejný vzor jako TmDocumentCanvasEngineHost) a controller v něm
/// vytváří/likviduje. Tyto testy drží interop kontrakt komponenty.
/// </summary>
public class DocumentEditorToolbarOverflowControllerTests : LocalizationTestBase
{
    private const string OverflowModulePath =
        "./_content/Tempo.Blazor.DocumentEditor/js/document-editor/toolbar/toolbar-overflow.mjs";

    [Fact]
    public void Toolbar_ImportsOverflowModule_AndCreatesControllerOnFirstRender()
    {
        var module = JSInterop.SetupModule(OverflowModulePath);
        module.SetupVoid("createOverflowController", _ => true).SetVoidResult();

        RenderComponent<TmDocumentEditorToolbar>();

        var invocation = module.Invocations.Should()
            .ContainSingle(item => item.Identifier == "createOverflowController",
                "toolbar musí po prvním renderu vytvořit overflow controller v ES modulu")
            .Subject;
        invocation.Arguments.Should().HaveCount(2, "controller dostává ribbon-groups element a DotNetObjectReference");
        invocation.Arguments[1].Should().BeAssignableTo<Microsoft.JSInterop.DotNetObjectReference<TmDocumentEditorToolbar>>();
    }

    [Fact]
    public async Task OverflowSearchBox_StartsEmpty_NotWithLiteralFieldName()
    {
        // Nalezeno E2E screenshotem: SearchQuery="_overflowSearchQuery" (bez @) předávalo název
        // fieldu jako string literál — search box se otevíral s předvyplněným „_overflowSearchQuery".
        var module = JSInterop.SetupModule(OverflowModulePath);
        module.SetupVoid("createOverflowController", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentEditorToolbar>();
        await cut.InvokeAsync(() => cut.Instance.SetOverflowingAsync(
            true, ["bold", "italic", "underline", "strikethrough", "link", "clearFormatting", "alignLeft", "alignCenter"]));
        cut.Find("[data-testid='document-toolbar-more']").Click();

        var search = cut.Find("[data-testid='document-toolbar-more-search']");
        (search.GetAttribute("value") ?? string.Empty).Should().BeEmpty(
            "search box se musí otevřít prázdný — hodnota je stav komponenty, ne literál názvu fieldu");
    }

    [Fact]
    public async Task Toolbar_DisposesOverflowController_AndModuleOnDispose()
    {
        var module = JSInterop.SetupModule(OverflowModulePath);
        module.SetupVoid("createOverflowController", _ => true).SetVoidResult();
        module.SetupVoid("disposeOverflowController", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentEditorToolbar>();
        await cut.Instance.DisposeAsync();

        module.Invocations.Should().Contain(item => item.Identifier == "disposeOverflowController",
            "dispose komponenty musí odpojit observery v JS controlleru");
    }

    [Fact]
    public async Task SetOverflowingAsync_FromJsController_ShowsMoreButtonWithReportedCommands()
    {
        var module = JSInterop.SetupModule(OverflowModulePath);
        module.SetupVoid("createOverflowController", _ => true).SetVoidResult();

        var cut = RenderComponent<TmDocumentEditorToolbar>();
        cut.Find("[data-testid='document-toolbar-more']").HasAttribute("hidden").Should().BeTrue(
            "bez hlášení z JS controlleru se More tlačítko nesmí ukázat");

        await cut.InvokeAsync(() => cut.Instance.SetOverflowingAsync(true, ["bold", "italic"]));

        cut.Find("[data-testid='document-toolbar-more']").HasAttribute("hidden").Should().BeFalse(
            "hlášený overflow musí More tlačítko odkrýt");

        await cut.InvokeAsync(() => cut.Instance.SetOverflowingAsync(false, []));
        cut.Find("[data-testid='document-toolbar-more']").HasAttribute("hidden").Should().BeTrue(
            "když se okno zvětší a nic nepřetéká, More tlačítko zmizí");
    }
}
