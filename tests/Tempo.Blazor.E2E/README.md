# Tempo.Blazor E2E Tests

This project contains end-to-end tests for the Tempo.Blazor component library using Playwright and MSTest.

## Test Coverage

### Server Rendering Tests (`ServerRenderingTests.cs`)
- Basic component rendering (TmButton, TmCard, TmBadge, TmAlert)
- Form inputs rendering (TmInput, TmSelect, TmCheckbox)
- Dark mode toggle functionality
- Localization switching (CZ/EN)
- Page navigation without errors

### InteractiveAuto Tests (`InteractiveAutoTests.cs`)
- Prerendering without errors
- WASM boot and hydration
- Rich Editor rendering after WASM boot
- Dashboard drag & drop functionality
- Workflow Designer rendering
- Scheduler views (Month, Week, Day, Timeline)
- DataTable client-side data handling
- Memory leak detection

### WASM Tests (`InteractiveAutoTests.cs` - `WasmTests` class)
- WASM app loading and rendering

## Prerequisites

1. Install Playwright browsers:
```bash
dotnet tool install --global Microsoft.Playwright.CLI
playwright install
```

2. Start the demo applications:
```bash
# Terminal 1 - Start WASM Demo
dotnet run --project src/Tempo.Blazor.Demo

# Terminal 2 - Start Server Demo
dotnet run --project src/Tempo.Blazor.Demo.Server

# Terminal 3 - Start InteractiveAuto Demo
dotnet run --project src/Tempo.Blazor.Demo.InteractiveAuto/Tempo.Blazor.Demo.InteractiveAuto
```

## Running Tests

### Run all tests
```bash
dotnet test tests/Tempo.Blazor.E2E/
```

### Run tests by category
```bash
# Server tests only
dotnet test tests/Tempo.Blazor.E2E/ --filter "Category=Server"

# WASM tests only
dotnet test tests/Tempo.Blazor.E2E/ --filter "Category=WASM"

# InteractiveAuto tests only
dotnet test tests/Tempo.Blazor.E2E/ --filter "Category=InteractiveAuto"
```

### Run specific test
```bash
dotnet test tests/Tempo.Blazor.E2E/ --filter "FullyQualifiedName~TmButton_Renders"
```

### Run with UI (headed mode)
Set the `Headless` property to `false` in the test context or modify the `ClassInitialize` method.

## Test URLs

| Application | URL |
|------------|-----|
| WASM Demo | https://localhost:7106 |
| Server Demo | https://localhost:7107 |
| InteractiveAuto Demo | https://localhost:7108 |

## Test Architecture

```
PlaywrightTestBase (abstract)
    ├── WasmTestBase (BaseUrl: https://localhost:7106)
    │   └── WasmTests
    ├── ServerTestBase (BaseUrl: https://localhost:7107)
    │   └── ServerRenderingTests
    └── InteractiveAutoTestBase (BaseUrl: https://localhost:7108)
        └── InteractiveAutoTests
```

## Adding New Tests

1. Create a new test class inheriting from the appropriate base class:
```csharp
[TestClass]
public class MyNewTests : ServerTestBase
{
    [TestMethod]
    public async Task MyTest()
    {
        var page = await CreatePageAsync();
        // Test code here
    }
}
```

2. Use the helper methods from `PlaywrightTestBase`:
- `CreatePageAsync()` - Creates a new page and navigates to base URL
- `NavigateToPageAsync(page, "Menu Text")` - Clicks navigation menu
- `ToggleDarkModeAsync(page)` - Toggles dark mode
- `SwitchLanguageAsync(page, "cs")` - Switches language
- `TakeScreenshotAsync(page, "name")` - Takes screenshot
- `GetHeapSizeAsync(page)` - Gets JS heap size

### Document Editor Tests

Document editor WYSIWYG tests should exercise the real demo editor and the JS-owned runtime path. Open the page with `OpenDocumentEditorPageAsync`, wait for `[data-testid='document-wysiwyg-host']` plus `WaitForWysiwygBodyAsync`, and prefer DOM/provider assertions over Blazor render-count assertions for the editable surface.

Use `window.tmDocumentEditorRuntime` and `window.tmDocumentEditorDebug` only for runtime invariants and diagnostics such as undo state, dirty state, snapshot reload counts, render stats, and selection snapshots. Keep coverage spread across typing, undo/redo, formatting, track changes, comments, images, tables, headers/footers, collaboration, save/reload, DOCX import/export, PDF export, and comparison. Additional runtime details are documented in `docs/document-editor-js-owned-runtime.md`.

Document editor E2E files are classified by `DocumentEditorE2EContractAuditTests`:
- `DocumentEditor:HumanWorkflow` covers user-visible behavior through Playwright mouse, keyboard, locator and provider interactions.
- `DocumentEditor:DiagnosticRuntime` covers strict runtime/layout probes and must not be counted as UX parity coverage by itself.
- `DocumentEditor:ProviderBoundary` covers save/load/import/export/collaboration boundaries.
- `DocumentEditor:LayoutVisual` covers visible geometry, viewport safety and UI placement.
- `LegacyMixed` or obsolete coverage must point at a stricter replacement file before it can remain green.

For local editor assertions, prefer `WaitForEditorStableAsync(page, reason, blockId, expectedText)` over fixed sleeps. The helper waits for the host, visible blocks, optional expected text and absence of Blazor/runtime error UI; it deliberately does not wait for save/autosave.

## CI/CD Integration

For CI/CD pipelines, use headless mode and ensure demo apps are running:

```yaml
# Example GitHub Actions step
- name: Run E2E Tests
  run: |
    dotnet run --project src/Tempo.Blazor.Demo &
    dotnet run --project src/Tempo.Blazor.Demo.Server &
    dotnet run --project src/Tempo.Blazor.Demo.InteractiveAuto/Tempo.Blazor.Demo.InteractiveAuto &
    sleep 30  # Wait for apps to start
    dotnet test tests/Tempo.Blazor.E2E/ --no-build
```

## Troubleshooting

### Browser not found
```bash
playwright install
```

### Connection refused
Ensure demo applications are running on the expected ports.

### Timeout errors
Increase timeout values in `PlaywrightTestBase.cs` or ensure apps are fully loaded before tests run.

### Screenshots not showing
Check `TestContext.TestResultsDirectory` for screenshot locations.
