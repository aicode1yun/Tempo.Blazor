# TmDocumentEditor: rozhodnutí OT/CRDT pro prototyp

Pro první prototyp volíme operation-log model s deterministickým CRDT-style orderingem nad blokovým JSON dokumentem. Není to ještě plný CRDT s per-character identifikátory; cílem je stabilní append-only log, idempotentní replay a jasný provider boundary pro budoucí rozšíření.

## OT vs CRDT

OT je vhodné pro centralizovaný editor s jedním autoritativním serverem, který transformuje operace podle pořadí doručení. CRDT je vhodnější pro offline a pozdější peer/reconnect scénáře, protože operace se dají deterministicky seřadit a opakovaně aplikovat bez ztráty lokálních změn.

## Vyhodnocení modelu

- Blokové operace: `InsertBlock`, `DeleteBlock`, `MoveBlock`, `SetBlockAttribute` jsou přirozený základ. Konflikty se řeší deterministicky podle logical timestamp, client id a operation id.
- Textové operace: první verze podporuje `InsertText`, `DeleteText`, `AddMark`, `RemoveMark` nad inline indexem a offsetem. Později bude potřeba stabilnější text range identita.
- Inline marks: aktuálně jsou transformované jen vůči delete rozsahům. Mark proti delete se zahodí, pokud cíl zmizí.
- Komentářové anchory: zůstávají mimo první transformaci; později se budou přepočítávat přes stejný operation stream.
- Tabulky: první prototyp je bere jako bloky/cell text přes `SetBlockAttribute`; granularita buněk přijde později.
- Undo/redo: lokální command stack zůstává UI vrstva. Operation log je append-only a neprovádí destruktivní revert.
- Offline edits: offline draft může nést operation batches a replay po reconnectu používá idempotentní log.

## Rozhodnutí

První algoritmus je hybrid: append-only operation log, idempotentní replay podle operation id, LWW pro konfliktní blokové atributy/move operace a deterministické posuny textových offsetů pro základní souběžné insert/delete scénáře. Full CRDT per-character identifikátory odkládáme do další iterace.

## Upřesnění po JS WYSIWYG enginu

Live WYSIWYG surface je vlastněný JavaScriptem. Blazor drží autoritativní model pro persistenci, export, revize, suggestions a panely, ale úspěšné remote operace se do aktivního editoru aplikují přes JS patcher (`applyRemoteOperationBatch`) místo full snapshot renderu.

Provider boundary posílá strukturované `DocumentOperationBatch` payloady. Textové změny používají `InsertText`/`DeleteText`, formátování `AddInlineMark`/`RemoveInlineMark`, objektové změny `InsertBlock`/`UpdateBlock`/`DeleteBlock` a revize `CreateRevision`/`AcceptRevision`/`RejectRevision`. `SetBlockAttribute("text")` zůstává jen jako legacy/import fallback pro starší nebo externí batch payloady, protože by jinak ztrácel inline marks, tokeny a rich obsah.

Recovery pravidlo: pokud JS patch selže nebo engine není dostupný, komponenta provede snapshot refresh z C# modelu. Tento fallback je explicitně výjimečná synchronizační cesta, ne běžný render flow pro real-time spolupráci.
