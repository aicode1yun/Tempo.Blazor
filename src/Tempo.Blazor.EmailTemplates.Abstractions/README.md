# Tempo.Blazor.EmailTemplates.Abstractions

Engine and contracts behind the Tempo.Blazor email template editor. This package has **no Blazor
dependency** and is safe to reference from API / background-service projects that need to render or
send emails.

## What's inside

- **Block model** — strongly typed document model covering the full MJML 4 component set.
- **MJML generation** — model → MJML markup.
- **MJML import** — bidirectional, lossless MJML → model parser (full MJML 4 parity).
- **Scriban templating** — sandboxed variable substitution, validation and variable extraction.
- **Render pipeline** — document + data → HTML + plain-text via [Mjml.Net](https://github.com/SebastianStehle/mjml-net).
- **DTOs & validators** — request/response contracts with localized FluentValidation.
- **Host contracts** — `IEmailTemplateStore`, `IEmailSender` for the consuming application.

## Installation

```
dotnet add package Tempo.Blazor.EmailTemplates.Abstractions
```

## Registration

```csharp
services.AddTempoEmailTemplateEngine();
```

## Template syntax

Variable substitution uses Scriban. See [TEMPLATE_SYNTAX](https://github.com/ptyll/Tempo.Blazor/blob/main/docs/email-templates/TEMPLATE_SYNTAX.md).

> Quick-start and API docs are filled in as the package matures (see the implementation plan).
