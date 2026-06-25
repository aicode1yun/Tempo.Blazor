# Fáze 15 — A1: Komentáře

> Stav: ☐ Neza­počato · Závisí na: Fáze 0 · Server: ✅ lokálně / 🖥️ pro sdílení (Fáze 17)
> Řídí se [`00_MASTER_PLAN.md`](00_MASTER_PLAN.md).

## Cíl & rozsah
Vláknové komentáře ukotvené k buňce/rozsahu: indikátor v rohu buňky, popover s vláknem, odpovědi, autor + čas, **resolve/reopen**, editace/mazání vlastních, postranní panel se seznamem a filtrem, @zmínky (přes abstrakci uživatelů hostitele). Perzistence do XLSX (threaded comments). Sdílení v reálném čase je až ve Fázi 17 (co-editace) — model a eventy se navrhnou tak, aby to umožnily.

OnlyOffice reference: `model/CellComment.js` (`asc_CCommentCoords`, vlákna, resolve). **Interní vzor k převzetí:** document editor už komentáře má — `DocumentEditorJsRuntimeCommentTests`, panely a JS runtime v `wwwroot/js/document-editor/`. Zrcadlit jeho architekturu.

---

## ČÁST A — Abstrakce hostitele (uživatelé/identita)

### 15A.1 Identita
- [ ] **(test)** `ISpreadsheetUserContext` vrací aktuálního uživatele (`Id`, `DisplayName`, `Color`) a seznam uživatelů pro @zmínky.
- [ ] Vytvořit `Spreadsheet/Collaboration/ISpreadsheetUserContext.cs` (Abstractions) + `DefaultSpreadsheetUserContext` (jediný anonymní uživatel) pro běh bez hostitele.
- [ ] **(test)** zelené.

---

## ČÁST B — Datový model

### 15B.1 Komentář a vlákno
- [ ] **(test)** `SpreadsheetCommentTests`: `SpreadsheetComment { string Id, string AnchorCellRef, SpreadsheetRange? AnchorRange, string AuthorId, string AuthorName, DateTime CreatedUtc, string Text, bool Resolved, List<SpreadsheetCommentReply> Replies, List<string> Mentions }`; `SpreadsheetCommentReply { Id, AuthorId, AuthorName, CreatedUtc, Text, Mentions }`.
- [ ] Vytvořit modely v `Spreadsheet/Models/`; přidat `SpreadsheetSheet.Comments` + `SpreadsheetCell.CommentId` (odkaz) + `Clone()`.
- [ ] **(test)** zelené (vč. posunu kotvy při insert/delete řádku/sloupce — návaznost na strukturální commandy).

### 15B.2 Posun kotvy
- [ ] **(test)** `Comment_AnchorShifts_OnInsertDeleteRowCol`: kotva komentáře se posune s buňkou.
- [ ] Zapojit do `InsertRow/DeleteRow/InsertColumn/DeleteColumn` commandů.
- [ ] **(test)** zelené.

---

## ČÁST C — Commandy
- [ ] **(test)** `AddCommentCommand`, `EditCommentCommand`, `DeleteCommentCommand`, `AddReplyCommand`, `EditReplyCommand`, `DeleteReplyCommand`, `ResolveCommentCommand` / `ReopenCommentCommand` — vše Undo; oprávnění (editovat/mazat jen vlastní, pokud host neurčí jinak).
- [ ] Vytvořit commandy + emit doménového eventu (`SpreadsheetCommentChanged`) pro budoucí realtime broadcast (Fáze 17).
- [ ] **(test)** zelené.

---

## ČÁST D — JS canvas rendering
- [ ] Rozšířit `spreadsheet-canvas.js`: kreslit **indikátor komentáře** (trojúhelníček v pravém horním rohu buňky); hit-test → C# event `OnCommentIndicatorClicked(cellRef)`.
- [ ] Indikátor odlišit pro vyřešené (resolved) komentáře.
- [ ] **(E2E)** buňka s komentářem má indikátor; klik otevře popover.

---

## ČÁST E — UI

### 15E.1 Popover vlákna
- [ ] **(bUnit, failing)** `TmSpreadsheetCommentPopover`: vlákno (autor, čas, text), pole pro odpověď, akce Resolve/Reopen/Upravit/Smazat (dle oprávnění), @zmínky našeptávač. Lokalizováno.
- [ ] Vytvořit komponentu `Components/Spreadsheet/Panels/TmSpreadsheetCommentPopover.razor(.cs/.css)` + lokalizace `TmSpreadsheet_Comment_*`.
- [ ] **(bUnit)** zelené.

### 15E.2 Postranní panel
- [ ] **(bUnit)** `TmSpreadsheetCommentsPanel`: seznam všech komentářů, filtr (aktuální list / sešit / vyřešené / moje), klik skočí na buňku, inline akce.
- [ ] Vytvořit panel + lokalizace.
- [ ] **(bUnit)** zelené.

### 15E.3 Vstupní body
- [ ] Tlačítko „Nový komentář" (záložka Vložit/Revize), kontextové menu buňky „Vložit komentář", zkratka (`Ctrl+Alt+M` / `Shift+F2`).
- [ ] @zmínky: našeptávač z `ISpreadsheetUserContext`; vykreslit zmínku jako chip.

---

## ČÁST F — E2E, screenshot, XLSX
- [ ] **(E2E)** přidat komentář → indikátor → odpovědět → resolve → filtr „vyřešené" ho ukáže.
- [ ] **(E2E)** @zmínka uživatele se vloží a zobrazí.
- [ ] Baseline `comment-01-popover.png`, `comment-02-panel.png`, `comment-03-resolved.png` + UX sign-off (čitelnost vlákna, barvy autorů, stav resolved, pozice popoveru u okraje).
- [ ] **(test)** XLSX round-trip threaded comments (`<threadedComments>` + legacy `<comments>` fallback).

---

## Definition of Done (Fáze 15)
- [ ] Komentáře: vlákno + odpovědi + resolve/reopen + edit/delete (oprávnění) + @zmínky.
- [ ] Indikátor v canvasu, popover, postranní panel s filtrem; posun kotvy při strukturálních změnách.
- [ ] Model + eventy připravené na realtime (Fáze 17); identita přes `ISpreadsheetUserContext`.
- [ ] Commandy atomické + undo; Unit + bUnit + E2E + screenshoty zelené, UX sign-off PASS.
- [ ] XLSX round-trip; vše lokalizováno; žádné placeholdery.
- [ ] V `00_MASTER_PLAN.md` §8 přepnout stav fáze 15 na ✅.
