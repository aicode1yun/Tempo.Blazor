# Canvas engine - Faze 17: Comments, revisions a restricted editing (detailní TDD + E2E)

Datum: 2026-06-04 · Nadřazený: master canvas plán, **Faze 17** · Stav: funkční scope dokončen a ověřen dle odškrtnutých bodů; 2026-06-06 doplněn E2E add/delete comment + restricted suggestions boundary · Priorita: P1

## Proč

Komentáře (highlight + rail + reply/resolve), track changes (insert/delete/format + review modes + accept/reject) a restricted editing (protect document, editable regions, suggestions provider). Velká kolaborační subdoména. Reuse R.4.6 track-changes/comments.

## Cílový stav

- Add comment to selection; comment highlight render na canvasu; comment rail sync.
- Reply, resolve, reopen, delete; select comment → scroll/caret na anchor.
- Track insertions/deletions (vč. cross-block); track formatting changes.
- Review display modes: markup/final/original.
- Accept/reject one; accept/reject all.
- Protect document; editable regions; suggestions provider boundary.

## Clean-room
- [x] Vlastní; ONLYOFFICE `Comments.js`/`RevisionsChange.js` jen koncept.

## Znovupoužití
- [ ] `core-engine/comments.mjs`, `track-changes.mjs` (R.4.6).
- [x] C# `TmDocumentCommentRail`, `TmDocumentCommentThread`, `TmDocumentCommentComposer`, `TmDocumentRevisionPanel`, `TmDocumentReviewSummary`, `TmDocumentSuggestionPanel`.
- [ ] Annotations perm-ranges (Faze 4.6 converter).

## Doporučené nové soubory

```text
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/annotations/comment-overlay.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/annotations/revision-render.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/annotations/restricted-editing.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/annotations/__tests__/comments.test.mjs
src/Tempo.Blazor/wwwroot/js/document-editor-canvas/annotations/__tests__/revisions.test.mjs
tests/Tempo.Blazor.E2E/DocumentEditorCanvasCommentsRevisionsE2ETests.cs
```

## DoD
- [x] Comments/revisions čitelné, barevně rozlišené, neruší text.
- [x] Save/reload comments + revisions; undo gate.

## Faze 17.1: Add comment + highlight + rail

### 17.1.1 RED
- [x] `comments.test.mjs`: add comment k selection vytvoří anchor + highlight; rail zobrazí thread; select comment → caret na anchor.

### 17.1.2 GREEN + screenshot + akceptace
- [x] `comment-overlay.mjs` (highlight pass) + rail sync; E2E add comment.

## Faze 17.2: Reply/resolve/reopen/delete

### 17.2.1 RED
- [x] Reply; resolve (highlight ztlumí); reopen; delete; anchor přežije editaci okolo.

### 17.2.2 GREEN + screenshot + akceptace
- [x] Thread ops; anchor maintenance; E2E reply→resolve→reopen.

## Faze 17.3: Track changes (insert/delete/format)

### 17.3.1 RED
- [x] `revisions.test.mjs`: track insertions (barva+podtržení), deletions (strikethrough, cross-block), formatting changes; autor/čas.

### 17.3.2 GREEN + screenshot + akceptace
- [x] `revision-render.mjs` overlay; reuse track-changes; E2E zapnout TC, psát/mazat.

## Faze 17.4: Review modes + accept/reject

### 17.4.1 RED
- [x] Display markup/final/original; accept/reject one; accept/reject all.

### 17.4.2 GREEN + screenshot + akceptace
- [x] Review mode render; accept/reject ops (undoable); E2E accept/reject.

## Faze 17.5: Restricted editing

### 17.5.1 RED
- [x] `restricted-editing`: protect document; editable regions (jen ty lze editovat); suggestions provider boundary.

### 17.5.2 GREEN + screenshot + akceptace fáze 17
- [x] Protect + editable regions enforcement; suggestions provider; E2E protect → edit jen v regionu; save/reload; undo gate.
- [x] Screenshot: comments/revisions barevně rozlišené, čitelné.

## Poznámky
- @mentions v komentářích (MentionProvider) reuse z tokens/mentions.
- Cross-block deletion revize jsou ošemetné — držet test na hranicích bloků.
- 2026-06-05: Doplněn `DocumentEditorCanvasCommentsRevisionsE2ETests` o screenshoty před/po komentáři, po TC psaní+mazání a po save/reload. Ověřeno: reply→resolve→reopen persistuje po reloadu, TC insertion/deletion vytvoří revizní markery, accept one + reject all odstraní pending markery, protected edit mimo region nemění model ani undo depth, povolená editace v regionu persistuje po save/reload.
- 2026-06-05 ověření: `node --test src/Tempo.Blazor/wwwroot/js/document-editor-canvas/annotations/__tests__/comments.test.mjs src/Tempo.Blazor/wwwroot/js/document-editor-canvas/annotations/__tests__/revisions.test.mjs` prošlo 7/7; `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` prošlo; `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-build --filter "FullyQualifiedName~DocumentEditorCanvasCommentsRevisionsE2ETests"` prošlo 1/1.
- 2026-06-06: Dořešen add-comment tok z reálného canvas text selection, rail sync z canvas runtime snapshotu, select comment → caret na anchor, provider-backed delete comment přes comment rail, restricted suggestions boundary v C# i JS runtime. Ověření: `node --test src/Tempo.Blazor/wwwroot/js/document-editor-canvas/annotations/__tests__/comments.test.mjs src/Tempo.Blazor/wwwroot/js/document-editor-canvas/annotations/__tests__/revisions.test.mjs` prošlo 10/10; `dotnet build src/Tempo.Blazor/Tempo.Blazor.csproj --no-restore --framework net10.0` prošlo; `dotnet build tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore` prošlo; `dotnet test tests/Tempo.Blazor.E2E/Tempo.Blazor.E2E.csproj --no-restore --filter "FullyQualifiedName~DocumentEditorCanvasCommentsRevisionsE2ETests" --logger "console;verbosity=normal"` prošlo 1/1. Screenshoty: `tests/Tempo.Blazor.E2E/TestResults/document-editor-canvas/phase17-comments-revisions/2026-06-04/Phase17_CommentsRevisionsAndRestrictedEditing_RenderAndReviewFromCanvas/20260606082117732/`.
- Zůstává neodškrtnuté pouze technické reuse: `core-engine/comments.mjs`, `track-changes.mjs` (R.4.6) a Annotations perm-ranges (Faze 4.6 converter), protože tato dodělávka implementovala produkční canvas tok bez plného převedení na tyto reuse položky.
