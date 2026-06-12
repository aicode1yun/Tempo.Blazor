# TmDocumentEditor: hranice track changes pro první vlnu

Plné Word-like track changes nejsou cílem první vlny editoru. Aktuální `DocumentRevision` zůstává lehký audit/review model pro lokální změny, importované Word revisions a UI indikaci, ale nebude se rozšiřovat na plnohodnotný suggestions engine přímo v core dokumentovém JSONu.

Pro návrhy změn se zavádí samostatný model `DocumentSuggestion` a provider boundary `IDocumentSuggestionProvider`. Hostitelská aplikace tak může suggestions ukládat, schvalovat a odmítat mimo `DocumentEditorDocument`, aniž by se tím zafixoval budoucí OT/CRDT nebo Word-revision model.

První implementační pravidla:

- `DocumentEditorDocument` zůstává source of truth pro aktuální obsah.
- `DocumentSuggestion` je aplikační/review vrstva nad dokumentem, ne povinná součást snapshotu.
- Provider vrací suggestions podle dokumentu a statusu.
- Accept/reject suggestion zatím znamená provider decision, nikoli automatickou mutaci core modelu.
- Budoucí Word-like track changes může nad stejnou boundary přidat přesnější range anchory, text operation payload a konfliktové řešení.
