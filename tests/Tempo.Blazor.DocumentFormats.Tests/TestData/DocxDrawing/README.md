# DOCX DrawingML Test Data

Phase 25 uses generated DOCX fixtures instead of checked-in binary packages. The fixture builder lives in `DocxDrawing/DocxDrawingFixtureBuilder.cs` and creates deterministic packages for inline pictures, floating anchors, wrap modes, crop, rotation, header/footer images, and table-cell images.

To regenerate the deterministic fixtures, update the builder and run the phase 25/37 tests:

```bash
dotnet test tests/Tempo.Blazor.DocumentFormats.Tests/Tempo.Blazor.DocumentFormats.Tests.csproj --filter "FullyQualifiedName~DocumentDocxDrawingPhase25Tests|FullyQualifiedName~DocumentDocxDrawingPhase37Tests"
```

Phase 37 roundtrip tests intentionally inspect only small canonical XML fragments, such as `wp:extent`, `wp:wrapSquare`, and `a:srcRect`. Do not add snapshots of complete DOCX parts unless the part is tiny and deterministic.

When a fixture must come from Word or OnlyOffice byte-for-byte, add the `.docx` file in this folder and describe:

- the application and version that produced it,
- the exact scenario it covers,
- the expected `wp:inline` or `wp:anchor` shape,
- the image media type and relationship part.
