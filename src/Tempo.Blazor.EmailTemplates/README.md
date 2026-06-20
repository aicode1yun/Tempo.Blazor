# Tempo.Blazor.EmailTemplates

A visual, drag-and-drop email template editor component for [Tempo.Blazor](https://github.com/ptyll/Tempo.Blazor).
Built entirely on Tempo.Blazor components — no MudBlazor or Syncfusion dependency.

## Features

- **`TmEmailTemplateEditor`** — three-panel editor (toolbox · canvas · properties).
- **Full MJML 4 parity** — every block, every attribute, the complete `<mj-head>` section.
- **Bidirectional MJML** — import existing MJML templates and export back losslessly.
- **Scriban variables** — variable picker, live preview, sample-data generation.
- **Live preview** — sandboxed desktop / mobile preview with plain-text view.
- Undo/redo, copy/paste, autosave, keyboard shortcuts, localization (EN/CS/FR) and design-token theming.

## Installation

```
dotnet add package Tempo.Blazor.EmailTemplates
```

## Registration

```csharp
services.AddTempoEmailTemplates();
```

## Usage

```razor
<TmEmailTemplateEditor @bind-Document="document" OnSave="SaveAsync" />
```

The consuming application supplies an `IEmailTemplateStore` (persistence) and an `IEmailSender`
(delivery) from `Tempo.Blazor.EmailTemplates.Abstractions`.

> Quick-start and full API docs are filled in as the package matures (see the implementation plan).
