using FluentAssertions;

namespace Tempo.Blazor.Tests.Planning;

public class DocumentEditorCanvasEngineDecisionTests
{
    private const string PlanRelativePath = "planning/tmdocumenteditor-canvas-onlyoffice-inspired-engine-tdd-todo-2026-06-04.md";

    [Fact]
    public void CanvasEnginePlan_ExistsAsSourceOfTruth()
    {
        var plan = ReadCanvasEnginePlan();

        plan.Should().Contain("Tento soubor je od 2026-06-04 source of truth pro novy canvas engine `TmDocumentEditor`");
        plan.Should().Contain("canvas primary surface");
        plan.Should().Contain("model jako jedina pravda");
        plan.Should().Contain("Blazor pouze jako shell");
    }

    [Fact]
    public void CanvasEnginePlan_PreservesCleanRoomGuardrails()
    {
        var plan = ReadCanvasEnginePlan();

        plan.Should().Contain("ONLYOFFICE v `/home/pavel/NetProjects/onlyfficeservergit` je AGPL");
        plan.Should().Contain("Nekopirovat zadny zdrojovy kod");
        plan.Should().Contain("Neprekladat ani mechanicky neprepisovat ONLYOFFICE implementaci");
        plan.Should().Contain("ONLYOFFICE byl pouzit pouze jako clean-room architektonicka inspirace; kod nebyl kopirovan.");
    }

    [Fact]
    public void CanvasEnginePlan_RecordsCurrentCoreEngineUxBaseline()
    {
        var plan = ReadCanvasEnginePlan();

        plan.Should().Contain("### Faze 0 baseline nespokojenosti se stavajicim core enginem");
        plan.Should().Contain("Selection stale vypada jako DOM kompromis");
        plan.Should().Contain("Per-page cache chybi jako primarni koncept");
        plan.Should().Contain("Accessibility mirror neni samostatny prvotridni kontrakt canvas enginu");
    }

    [Fact]
    public void CanvasEnginePlan_DefinesBeforeRedesignScreenshotEvidence()
    {
        var plan = ReadCanvasEnginePlan();

        plan.Should().Contain("tests/Tempo.Blazor.E2E/DocumentEditorCanvasEngineBaselineE2ETests.cs");
        plan.Should().Contain("tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/before-redesign/2026-06-04/");
        plan.Should().Contain("00-current-core-full.png");
        plan.Should().Contain("01-current-core-editor.png");
        plan.Should().Contain("document-canvas-engine-host");
    }

    [Fact]
    public void CanvasEnginePlan_DefinesTopTwentyHumanScenarios()
    {
        var plan = ReadCanvasEnginePlan();
        var scenarioLines = plan.Split(Environment.NewLine)
            .Where(line => System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d+\.\s"))
            .ToArray();

        scenarioLines.Should().Contain(line => line.Contains("Otevrit dokument"));
        scenarioLines.Should().Contain(line => line.Contains("Psat rychle dlouhou vetu"));
        scenarioLines.Should().Contain(line => line.Contains("IME, diakritiku, emoji"));
        scenarioLines.Should().Contain(line => line.Contains("Ulozit, reloadnout, exportovat"));
        scenarioLines.Should().HaveCountGreaterThanOrEqualTo(20);
    }

    [Fact]
    public void CanvasEngineBaselineE2E_ExistsAndCarriesCanvasHostGate()
    {
        var root = FindRepositoryRoot();
        var e2ePath = Path.Combine(root.FullName, "tests", "Tempo.Blazor.E2E", "DocumentEditorCanvasEngineBaselineE2ETests.cs");
        var e2e = File.ReadAllText(e2ePath);

        e2e.Should().Contain("DocumentEditorCanvasEngineBaselineE2ETests");
        e2e.Should().Contain("Baseline_CurrentCanvasEngine_CapturesDesktopScreenshots");
        e2e.Should().Contain("CanvasEngineRouteFlag_RendersCanvasHost");
        e2e.Should().Contain("CanvasEngineRouteFlag_RendersCanvasHostWhenEnabled");
        e2e.Should().Contain("document-canvas-engine-host");
        e2e.Should().NotContain("[Ignore(");
    }

    private static string ReadCanvasEnginePlan()
    {
        var root = FindRepositoryRoot();
        var candidate = Path.Combine(root.FullName, PlanRelativePath);
        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException($"Could not locate {PlanRelativePath}.", candidate);
        }

        return File.ReadAllText(candidate);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TempoBlazor.slnx")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate TempoBlazor.slnx from test output directory.");
    }
}
