# Proofing out-of-the-box: LanguageTool integration

`TmDocumentEditor` ships with a word-list based proofing runtime (squiggle overlay + spelling
context menu). Until now the word lists were bring-your-own via `DocumentProofingOptions`
(`FlaggedWords`/`Suggestions`). Phase 7 adds an **async provider seam** plus a **reference
LanguageTool implementation**, so hosts get real spellchecking — including Czech — without
building their own checker.

## Building blocks

| Piece | Package | Purpose |
| --- | --- | --- |
| `ITempoProofingProvider` | Tempo.Blazor.Abstractions | Async check contract: `CheckAsync(DocumentProofingCheckRequest) → DocumentProofingCheckResult` (issues = word + offset/length + suggestions + rule metadata). |
| `DocumentProofingService.BuildOptions` | Tempo.Blazor.Abstractions | Materializes provider issues into `DocumentProofingOptions` word lists, merged over host base options. |
| `LanguageToolProofingProvider` | **Tempo.Blazor.Proofing.LanguageTool** | Speaks the LanguageTool v2 HTTP protocol (`POST {base}/v2/check`, form-encoded `text`+`language`, JSON `matches`). |
| `TmDocumentEditor.ProofingProvider` | Tempo.Blazor.DocumentEditor | New optional parameter. The editor extracts document plain text, checks after load and after edits (debounced via `ProofingCheckDebounce`, default 1.2 s against the live canvas model), and pushes refreshed word lists into the canvas engine at runtime. Provider failures are **fail-open** — an unreachable server never breaks editing. |

## Self-hosted LanguageTool (docker compose)

LanguageTool ships Czech (`cs-CZ`) support out of the box, including the Morfologik spelling
dictionary. A minimal self-host:

```yaml
# docker-compose.yml
services:
  languagetool:
    image: erikvl87/languagetool:6.4
    container_name: languagetool
    restart: unless-stopped
    ports:
      - "8010:8010"
    environment:
      # Optional tuning — see https://github.com/Erikvl87/docker-languagetool
      - langtool_maxTextLength=50000
      - Java_Xms=512m
      - Java_Xmx=1g
    # Optional: extend the built-in Czech spelling dictionary with your own terms
    # (one word per line). The same mechanism works for other languages (…/en/…, …/de/…).
    volumes:
      - ./cs-custom-words.txt:/LanguageTool/org/languagetool/resource/cs/hunspell/spelling_custom.txt:ro
```

```bash
docker compose up -d
curl -d "text=Tato smlouvva je špatně" -d "language=cs-CZ" http://localhost:8010/v2/check
```

## Wiring it into the editor

```csharp
var proofing = new LanguageToolProofingProvider(
    httpClient, // any HttpClient; BaseAddress fallback order: options → client → http://localhost:8010
    new LanguageToolProofingOptions
    {
        BaseAddress = new Uri("http://languagetool.internal:8010"),
        Language = "cs-CZ",                    // or "auto" for detection; CreateCzech() shortcut exists
        DisabledRules = ["WHITESPACE_RULE"],
    });
```

```razor
<TmDocumentEditor DocumentId="contract-1"
                  Provider="@DocumentProvider"
                  ProofingProvider="@proofing"
                  ProofingOptions="@(new DocumentProofingOptions { DefaultLanguage = "cs-CZ" })" />
```

`ProofingOptions.DefaultLanguage` is passed as the per-request language; host-supplied
`FlaggedWords`/`Suggestions` are preserved and merged with the provider findings.

## Custom dictionary (Czech terminology)

Two complementary levels:

- **Server-side** — mount extra words into the container's Czech hunspell dictionary (see the
  compose volume above). Best for organization-wide terminology; applies to every client.
- **Client-side** — `LanguageToolProofingOptions.CustomDictionary` (case-insensitive set) suppresses
  findings without a server round-trip, and `provider.AddToDictionary(word)` adds to it at runtime.
  Wire it to your persistence if the user's "Add to dictionary" choice should survive sessions; the
  editor's context-menu *Add to dictionary* action additionally suppresses the word in the current
  canvas session.

## Demo & E2E

The demo API hosts a LanguageTool-**protocol** endpoint (`POST /languagetool/v2/check`,
`src/Tempo.Blazor.Demo.Api/Endpoints/LanguageToolEndpoints.cs`) backed by a small Czech demo
dictionary, so the real provider runs end-to-end in tests without a container:

- `https://localhost:7106/canvas-engine-host?documentId=phase-7-proofing-czech&proofing=languagetool&showToolbar=true`
  — Czech seed with `smlouvva`/`chybbou`, red squiggles, context-menu corrections.
- `…&proofing=languagetool-down` — provider pointed at an unreachable server: fail-open, no
  squiggles, editing unaffected.

E2E coverage: `tests/Tempo.Blazor.E2E/DocumentEditorProofingE2ETests.cs` (fix-from-context-menu +
fail-open edge case, screenshots in `__screenshots__/document-editor-proofing/`).
