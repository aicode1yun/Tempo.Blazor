# Tempo.Blazor.DocumentFormats

Optional server-side DOCX and ODT import/export support for `TmDocumentEditor`.

Use this package from an API/server project. `Tempo.Blazor` does not reference this package, so client-side component consumers can keep the core editor focused on the internal JSON document model.

Typical flow:

```csharp
var importer = new DocumentDocxImporter();
var imported = await importer.ImportAsync(upload.OpenReadStream());

var exporter = new DocumentDocxExporter();
var exported = await exporter.ExportAsync(document);
```

DOCX support uses Open XML SDK. ODT support uses ZIP and LINQ to XML.
