# Tempo.Blazor – AI Agent Guide

## Project Overview

**Tempo.Blazor** is a comprehensive Blazor component library with 125+ reusable Razor components designed for AI-assisted development. The library provides a complete UI toolkit for building modern Blazor applications with support for multiple render modes (WebAssembly, Server, InteractiveAuto), localization, theming (light/dark), FluentValidation integration, and a CSS design system based on custom properties.

### Key Features
- **125+ reusable Razor components** organized into 28 categories (inputs, data tables, pickers, layout, feedback, charts, dashboards, workflow designer, etc.)
- **Multi-target .NET support**: .NET 8.0, 9.0, and 10.0
- **Full localization support** via `ITmLocalizer` (English + Czech built-in, extensible)
- **CSS design system** with CSS custom properties (`--tm-*` tokens)
- **Dark mode support** via `ThemeService`
- **FluentValidation integration** (optional separate package)
- **Icon extensibility** via `IconRegistry` and `IIconProvider`
- **WCAG 2.1 AA accessibility compliance**

## Technology Stack

| Category | Technology | Version |
|----------|------------|---------|
| Framework | .NET | 8.0, 9.0, 10.0 |
| UI Framework | Blazor (WASM, Server, InteractiveAuto) | Latest |
| Language | C# | 12 (latest) |
| Styling | CSS Custom Properties (Design Tokens) | - |
| Validation | FluentValidation | 12.1.1 |
| Localization | Microsoft.Extensions.Localization | Matching .NET version |
| Unit Testing | xUnit + bUnit | xUnit 2.9.3, bUnit 1.38.5 |
| E2E Testing | Playwright + MSTest | Playwright 1.51.0 |
| Assertions | FluentAssertions | 8.4.0 |
| Mocking | NSubstitute | 5.3.0 |

## Project Structure

```
TempoBlazor.slnx
├── src/
│   ├── Tempo.Blazor.Abstractions/    # Interfaces and models (NuGet package)
│   ├── Tempo.Blazor/                 # Main component library (NuGet package)
│   ├── Tempo.Blazor.FluentValidation/# Optional FluentValidation integration
│   ├── Tempo.Blazor.Demo/            # Blazor WASM demo application
│   ├── Tempo.Blazor.Demo.Shared/     # Shared DTOs between API and Demo
│   ├── Tempo.Blazor.Demo.SharedUI/   # Shared UI components for all demos
│   ├── Tempo.Blazor.Demo.Api/        # ASP.NET Core Minimal API for demo data
│   ├── Tempo.Blazor.Demo.Server/     # Blazor Server demo application
│   └── Tempo.Blazor.Demo.InteractiveAuto/ # InteractiveAuto render mode demo
├── tests/
│   ├── Tempo.Blazor.Tests/           # bUnit component tests
│   ├── Tempo.Blazor.E2E/             # Playwright end-to-end tests
│   ├── Tempo.Blazor.Demo.Api.Tests/  # API integration tests
│   └── Tempo.Blazor.FluentValidation.Tests/  # Validation tests
└── .github/workflows/                 # CI/CD pipelines
```

### Project Dependencies

```
Tempo.Blazor.Abstractions (zero UI dependencies)
    ↑
Tempo.Blazor ────────┐
    ↑                │
Tempo.Blazor.FluentValidation (optional)
    ↑                │
Tempo.Blazor.Demo ◄──┘
    ↑
Tempo.Blazor.Demo.Shared ← Tempo.Blazor.Demo.Api
```

## Build and Test Commands

### Prerequisites
- .NET SDK 8.0, 9.0, and 10.0 installed
- For E2E tests: Playwright browsers installed (`playwright install`)

### Build
```bash
# Build entire solution
dotnet build TempoBlazor.slnx

# Build specific project
dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj

# Build in Release mode (creates NuGet packages)
dotnet build -c Release
```

### Test
```bash
# Run all tests
dotnet test

# Run with verbosity
dotnet test --verbosity normal

# Run specific test project
dotnet test tests/Tempo.Blazor.Tests/
dotnet test tests/Tempo.Blazor.E2E/
```

### Package Creation
```bash
# Create NuGet packages
dotnet pack src/Tempo.Blazor.Abstractions/ -c Release -o ./packages
dotnet pack src/Tempo.Blazor/ -c Release -o ./packages
dotnet pack src/Tempo.Blazor.FluentValidation/ -c Release -o ./packages
```

### Run Demo Applications
```bash
# Start Demo API (terminal 1)
cd src/Tempo.Blazor.Demo.Api
dotnet run
# API runs on: https://localhost:5100

# Start Demo WASM (terminal 2)
cd src/Tempo.Blazor.Demo
dotnet run
# App runs on: https://localhost:7106

# Start Demo Server (terminal 2)
cd src/Tempo.Blazor.Demo.Server
dotnet run
# App runs on: https://localhost:7107
```

## Code Organization

### Component Categories (28 folders in `src/Tempo.Blazor/Components/`)

| Category | Components |
|----------|------------|
| Activity | `TmActivityLog`, `TmActivityComments`, `TmActivityAttachments`, `TmActivityTimeline`, `TmRichEditorFull`, `TmRichEditorSimple` |
| Avatars | `TmAvatar`, `TmAvatarGroup` |
| Buttons | `TmButton`, `TmSplitButton`, `TmCopyButton` |
| Charts | `TmChart` (Bar, Line, Pie, Donut, HorizontalBar — pure SVG) |
| Dashboard | `TmDashboard`, `TmWidgetSelector` (drag & resize grid, JS interop) |
| DataDisplay | `TmBadge`, `TmCard`, `TmEmptyState`, `TmMultiViewList`, `TmStatCard`, `TmAccordion`, `TmAccordionItem`, `TmChip`, `TmChipGroup`, `TmKanbanBoard`, `TmChangeDiff` |
| DataTable | `TmDataTable`, `TmDataTableColumn`, `TmColumnFilter`, `TmColumnPicker`, `TmPagination`, `TmViewManager`, `TmBulkActionBar` |
| Dropdowns | `TmDropdown`, `TmDropdownItem`, `TmFilterableDropdown` |
| Feedback | `TmNotificationBell`, `TmSkeleton`, `TmSpinner`, `TmAlert`, `TmDialog`, `TmModal`, `TmProgressBar`, `TmToastContainer`, `TmTooltip`, `TmPopover` |
| Files | `TmAttachmentManager`, `TmFileDropZone` |
| Filters | `TmFilterBuilder`, `TmFilterChip` |
| Forms | `TmFormField`, `TmFormRow`, `TmFormSection`, `TmValidationSummary`, `TmValidatedField`, `TmDynamicFormRenderer`, `TmFormValidationMessage`, `TmInlineEdit` |
| Gallery | `TmImageGallery`, `TmLightbox` |
| Icons | `TmIcon`, `IconRegistry`, `IIconProvider`, `IconNames` |
| ImportExport | `TmImportWizard`, `TmImportPreview`, `TmExportOptions` |
| Inputs | `TmTextInput`, `TmTextArea`, `TmSelect`, `TmCheckbox`, `TmToggle`, `TmRadio`, `TmRadioGroup`, `TmSearchInput`, `TmPasswordStrengthIndicator`, `TmNumberInput`, `TmEntityPicker`, `TmExpressionEditor`, `TmMultiSelect` |
| Layout | `TmSidebar`, `TmBreadcrumbs`, `TmTopBar`, `TmCommandPalette`, `TmDrawer`, `TmSection`, `TmKeyboardShortcutsHelp` |
| Navigation | `TmTabs`, `TmTabPanel`, `TmContextMenu`, `TmContextMenuItem` |
| Notifications | `TmNotificationBell` (extended, per-item read, severity) |
| Pickers | `TmDatePicker`, `TmDateRangePicker`, `TmDateTimePicker`, `TmDateTimeRangePicker`, `TmTimePicker`, `TmTimeRangePicker`, `TmCalendarView` |
| Scheduler | `TmScheduler` with multiple views (Month, Week, Day, Timeline, Agenda) |
| Tags | `TmTagPicker` |
| Timeline | `TmTimeline` |
| Toolbar | `TmToolbar`, `TmToolbarButton`, `TmToolbarDivider` |
| TreeView | `TmTreeView` |
| Workflow | `TmStepper`, `TmWorkflowDesignerCanvas`, `TmWorkflowToolbox`, `TmWorkflowPropertiesPanel`, `TmWorkflowMinimap` |

### CSS Architecture

```
wwwroot/css/
├── tempo-blazor.css          # Main entry point with @imports
├── tempo-blazor.bundled.css  # Auto-generated bundled version
├── tokens.css                # Design tokens (colors, spacing, typography)
├── tokens-dark.css           # Dark mode token overrides
├── base.css                  # Reset and base styles
├── animations.css            # Keyframes and animation utilities
├── breakpoints.css           # Responsive breakpoints
└── components/               # Individual component styles (90+ files)
    ├── _button.css
    ├── _input.css
    ├── _data-table.css
    └── ...
```

### Abstractions (Shared Library)

`Tempo.Blazor.Abstractions` contains zero-UI dependencies:
- **Interfaces**: `IDataTableDataProvider`, `IDropdownDataProvider`, `IFileAttachmentProvider`, `ITmLocalizer`, etc.
- **Models**: `SelectOption`, `DropdownItem`, `DataTableView`, `PagedResult`, `FilterDefinition`, etc.

This allows API/backend projects to reference these contracts without pulling Blazor dependencies.

## Development Conventions

### TDD Workflow
1. **RED**: Write bUnit test first
2. **GREEN**: Implement component to make test pass
3. **REFACTOR**: Clean up while keeping tests green

### Component Guidelines

#### Parameter Attributes
Every `[Parameter]` must have an XML documentation comment:
```csharp
/// <summary>Visual style variant. Defaults to Primary.</summary>
[Parameter] public ButtonVariant Variant { get; set; } = ButtonVariant.Primary;
```

#### No Hardcoded Text
All user-visible strings must use localization via `ITmLocalizer`:
```razor
<!-- GOOD -->
<button aria-label="@Loc["TmButton_AriaLabel"]">@Loc["TmButton_Text"]</button>

<!-- BAD -->
<button aria-label="Click me">Click me</button>
```

#### CSS Custom Properties
No hardcoded colors/sizes in CSS. Always use tokens:
```css
/* GOOD */
.tm-btn {
    background: var(--tm-color-primary);
    padding: var(--tm-space-2) var(--tm-space-4);
}

/* BAD */
.tm-btn {
    background: #3b82f6;
    padding: 8px 16px;
}
```

### Global Usings
All components have access to `ITmLocalizer` via `_Imports.razor`:
```razor
@inject ITmLocalizer Loc
```

### Component Naming
- **Prefix**: `Tm` (Tempo)
- **Format**: `Tm{ComponentName}.razor`
- **Namespace**: `Tempo.Blazor.Components.{Category}`

## Testing Strategy

### Unit Tests (bUnit)
**Location**: `tests/Tempo.Blazor.Tests/`

Test organization mirrors component structure:
```
Tests/
├── Components/
│   ├── Buttons/TmButtonTests.cs
│   ├── Inputs/TmTextInputTests.cs
│   └── ...
├── Localization/
│   ├── LocalizationTestBase.cs
│   └── TmButtonLocalizationTests.cs
└── Theme/ThemeServiceTests.cs
```

**Test base class** provides mocked localization:
```csharp
public class LocalizationTestBase : TestContext
{
    protected LocalizationTestBase()
    {
        Services.AddSingleton<ITmLocalizer>(new MockTmLocalizer());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }
}
```

### E2E Tests (Playwright)
**Location**: `tests/Tempo.Blazor.E2E/`

Uses MSTest runner (`EnableMSTestRunner=true`). Tests run against running Demo applications:
- WASM: `https://localhost:7106`
- Server: `https://localhost:7107`
- InteractiveAuto: `https://localhost:7108`

Base classes provided:
- `WasmTestBase` – for WASM demo tests
- `ServerTestBase` – for Server demo tests
- `InteractiveAutoTestBase` – for InteractiveAuto demo tests

### API Tests
**Location**: `tests/Tempo.Blazor.Demo.Api.Tests/`

Uses `Microsoft.AspNetCore.Mvc.Testing` for integration testing.

## Localization

### Resource Files
**Location**: `src/Tempo.Blazor/Resources/`
- `TmResources.resx` – English (default)
- `TmResources.cs.resx` – Czech

### Adding New Keys
1. Add to `TmResources.resx` (English)
2. Add to `TmResources.cs.resx` (Czech)
3. Use in component: `@Loc["KeyName"]`
4. Add to `MockTmLocalizer` in `LocalizationTestBase.cs` for tests

### Consuming Application Setup
```csharp
// Program.cs
builder.Services.AddTempoBlazor();

// Optional: Override with custom localizer
builder.Services.AddSingleton<ITmLocalizer, MyCustomLocalizer>();

// Set culture
var culture = new CultureInfo("cs");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;
```

## Theming

### Using the Design System
Add CSS to `index.html`:
```html
<link href="_content/Tempo.Blazor/css/tempo-blazor.css" rel="stylesheet" />
```

### Theme Service
```csharp
// ThemeService is automatically registered by AddTempoBlazor()

// Component
@inject ThemeService ThemeService

<div data-theme="@ThemeService.ThemeName">
    <button @onclick="ThemeService.Toggle">Toggle Theme</button>
</div>
```

### Customizing Tokens
Override in app's CSS:
```css
:root {
    --tm-color-primary: #your-brand-color;
    --tm-font-sans: 'Your Font', sans-serif;
}
```

## FluentValidation Integration

### Setup
```bash
dotnet add package Tempo.Blazor.FluentValidation
```

```csharp
// Program.cs
builder.Services.AddTempoFluentValidation(typeof(MyValidator).Assembly);
```

### Usage
```razor
<EditForm Model="model" OnValidSubmit="Submit">
    <FluentValidationValidator />
    
    <TmFormField Label="Name">
        <TmTextInput @bind-Value="model.Name" />
        <ValidationMessage For="() => model.Name" />
    </TmFormField>
</EditForm>
```

## Custom Icons

Register custom icons in `Program.cs`:
```csharp
// Inline SVG
IconRegistry.Register("my-logo", "<path d='...'/><circle .../>");

// Or custom provider
IconRegistry.RegisterProvider(new MyFontIconProvider());
```

Use in components:
```razor
<TmIcon Name="my-logo" />
```

## JavaScript Interop

Four JS files in `wwwroot/js/` require `<script>` tags in `index.html` when using specific components:
- `dashboard.js` — required by `TmDashboard` (drag & resize grid)
- `workflow-designer.js` — required by `TmWorkflowDesignerCanvas` (SVG drag, pan, zoom, transition creation)
- `richEditor.js` — required by `TmRichEditorFull` / `TmRichEditorSimple` (contenteditable interop)
- `scheduler.js` — required by `TmScheduler` (drag & drop events)

## Security Considerations

1. **XSS Prevention**: Components use `@` (encoded) output by default. Use `@((MarkupString)…)` only for trusted content.
2. **Icon SVGs**: Custom icons are rendered as `MarkupString`. Ensure SVG content is trusted/sanitized.
3. **No Secrets**: Demo API uses mock data stores with generated fake data.

## CI/CD Pipeline

**GitHub Actions**: `.github/workflows/publish-nuget.yml`

- **Triggers**: Push to main/master, tags (v*), pull requests, manual dispatch
- **Build Matrix**: .NET 8.0, 9.0, 10.0
- **Tests**: All tests must pass before publish
- **Packages**: Published to GitHub Packages
- **Versions**:
  - Tags: `v1.2.3` → version `1.2.3`
  - Manual (no suffix): `1.0.0`
  - Manual (with suffix): `1.0.0-beta1`
  - CI builds: `1.0.0-ci-{timestamp}`

## Useful Commands Reference

```bash
# Restore packages
dotnet restore

# Watch mode for development
dotnet watch --project src/Tempo.Blazor.Demo

# Clean build artifacts
dotnet clean

# Format code
dotnet format

# List NuGet package references
dotnet list package

# Check for outdated packages
dotnet list package --outdated

# Run specific test
dotnet test --filter "FullyQualifiedName~TmButtonTests"

# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"
```

## Language Note

- **Code, XML documentation, and comments**: English
- **Planning documents** (in `planning/`): Czech
- **Library localization**: English (`en`) and Czech (`cs`) built-in, extensible

## NuGet Packages

| Package | Description |
|---------|-------------|
| `Tempo.Blazor` | Main component library with all UI components |
| `Tempo.Blazor.Abstractions` | Interfaces and models, zero UI dependencies |
| `Tempo.Blazor.FluentValidation` | FluentValidation integration for EditForm |

---

*This file is intended for AI coding agents. For human-readable documentation, see `README.md`.*
