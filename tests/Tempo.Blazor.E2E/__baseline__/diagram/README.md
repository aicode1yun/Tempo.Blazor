# Diagram canvas — baseline screenshots

Referenční screenshoty pre-refactor stavu diagram editoru pro fázi 0.7 plánu
`planning/DIAGRAM_UNIFIED_SVG_PLAN.md`. Po dokončení F2 / F3 / F5 budou sloužit
jako porovnávací bázy pro pixel-diff (případně DOM-diff) testy.

## Automatické pořízení (doporučené)

1. Ve dvou terminálech:
   - Terminál A: `dotnet run --project src/Tempo.Blazor.Demo.Wasm` (běží na
     `https://localhost:7106`).
   - Terminál B:
     ```powershell
     dotnet test tests/Tempo.Blazor.E2E --filter "TestCategory=BaselineGeneration"
     ```
2. Playwright spustí `DiagramBaselineScreenshots.GenerateAllBaselines` a uloží
   všechny PNG do této složky (přepíše stávající).
3. Zkontroluj `git diff --stat tests/Tempo.Blazor.E2E/__baseline__/diagram/`
   a pokud jsou PNG v pořádku, commitni je do větve `ediagrameditorrewrite`.

## Obsah (5 stavů)

| Soubor                                  | Scénář                                                                    |
| --------------------------------------- | ------------------------------------------------------------------------- |
| `baseline-01-empty.png`                 | Prázdný diagram po kliknutí na **New document**.                          |
| `baseline-02-single-node.png`           | Jeden node (derivovaný smazáním ostatních z UML sample přes `OnDeleteSelected`). |
| `baseline-03-sample-with-edges.png`     | Výchozí stav dema — UML sample se všemi nody a hranami.                   |
| `baseline-04-selected-node.png`         | Sample s vybraným prvním nodem (resize + rotate handle visible).          |
| `baseline-05-rotated-node.png`          | Vybraný první node otočený o 45° přes `OnRotateEnded`.                    |

> **Pořadí v generátoru** je záměrně 03 → 04 → 05 → 02 → 01. Jdeme od bohatého stavu
> postupným odstraňováním, protože Insert-dropdown flow po `New document` je v Blazor
> renderu nespolehlivý (v prvním pokusu vyhazoval timeout na `.tm-diagram-node`).
> Všechny mutace jedou přes stabilní `[JSInvokable]` entry-pointy
> (`OnSelectionChanged`, `OnRotateEnded`, `OnDeleteSelected`).

Screenshoty pořizuje Playwright funkcí `ILocator.ScreenshotAsync()` na
`.tm-diagram-canvas` — tzn. vnější rámeček dema (toolbar, navigace) není na
obrázku.

## Ruční fallback

Pokud Playwright z nějakého důvodu selže, lze screenshot pořídit ručně přes
DevTools → *Capture node screenshot* nad uzlem `.tm-diagram-canvas`. Stavy 01–05
jsou popsány v tabulce výše.

## Rollback

Při nutnosti vrátit refaktor stačí `git switch main` — baseline složka zůstane
ve větvi `ediagrameditorrewrite` a hlavní větev není ovlivněna.
