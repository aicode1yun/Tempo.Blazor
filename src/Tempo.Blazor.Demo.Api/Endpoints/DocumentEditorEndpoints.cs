using Tempo.Blazor.Demo.Api.Services;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats.Docx;
using Tempo.Blazor.DocumentFormats.Odt;

namespace Tempo.Blazor.Demo.Api.Endpoints;

/// <summary>Demo endpoints for the document editor.</summary>
public static class DocumentEditorEndpoints
{
    /// <summary>Maps document editor demo endpoints.</summary>
    public static IEndpointRouteBuilder MapDocumentEditorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/document-editor").WithTags("Document Editor");

        group.MapGet("/{documentId}", async (
            string documentId,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            var result = await store.LoadAsync(documentId, new DocumentEditorLoadOptions
            {
                IncludeDocument = true,
                IncludeJson = true
            }, cancellationToken);

            return result.Found ? Results.Ok(result) : Results.NotFound(result);
        });

        group.MapPut("/{documentId}", async (
            string documentId,
            DocumentEditorSaveRequest request,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            request.DocumentId = documentId;
            request.ConcurrencyMode = DocumentEditorConcurrencyMode.Optional;
            var result = await store.SaveAsync(request, cancellationToken);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        group.MapGet("/documents/{documentId}", async (
            string documentId,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            var result = await store.LoadAsync(documentId, new DocumentEditorLoadOptions
            {
                IncludeDocument = true,
                IncludeJson = true
            }, cancellationToken);

            return result.Found ? Results.Ok(result) : Results.NotFound(result);
        });

        group.MapPut("/documents/{documentId}", async (
            string documentId,
            DocumentEditorSaveRequest request,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            request.DocumentId = documentId;
            request.ConcurrencyMode = DocumentEditorConcurrencyMode.Optional;
            var result = await store.SaveAsync(request, cancellationToken);
            return result.Success ? Results.Ok(result) : Results.Conflict(result);
        });

        group.MapPost("/import/docx", async (
            IFormFile file,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            await using var stream = file.OpenReadStream();
            var imported = await new DocumentDocxImporter().ImportAsync(stream, new()
            {
                FileName = file.FileName
            }, cancellationToken);
            var document = imported.Document;
            await store.SaveAsync(new DocumentEditorSaveRequest
            {
                DocumentId = document.DocumentId,
                Document = document,
                ConcurrencyMode = DocumentEditorConcurrencyMode.Force
            }, cancellationToken);
            return Results.Ok(imported);
        }).DisableAntiforgery();

        group.MapGet("/{documentId}/export/docx", async (
            string documentId,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            var loaded = await store.LoadAsync(documentId, new DocumentEditorLoadOptions { IncludeDocument = true }, cancellationToken);
            if (!loaded.Found || loaded.Document is null)
            {
                return Results.NotFound();
            }

            var exported = await new DocumentDocxExporter().ExportAsync(loaded.Document, cancellationToken: cancellationToken);
            return Results.File(exported.Content, exported.ContentType, exported.FileName);
        });

        group.MapPost("/import/odt", async (
            IFormFile file,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            await using var stream = file.OpenReadStream();
            var imported = await new DocumentOdtImporter().ImportAsync(stream, new()
            {
                FileName = file.FileName
            }, cancellationToken);
            var document = imported.Document;
            await store.SaveAsync(new DocumentEditorSaveRequest
            {
                DocumentId = document.DocumentId,
                Document = document,
                ConcurrencyMode = DocumentEditorConcurrencyMode.Force
            }, cancellationToken);
            return Results.Ok(imported);
        }).DisableAntiforgery();

        group.MapGet("/{documentId}/export/odt", async (
            string documentId,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            var loaded = await store.LoadAsync(documentId, new DocumentEditorLoadOptions { IncludeDocument = true }, cancellationToken);
            if (!loaded.Found || loaded.Document is null)
            {
                return Results.NotFound();
            }

            var exported = await new DocumentOdtExporter().ExportAsync(loaded.Document, cancellationToken: cancellationToken);
            return Results.File(exported.Content, exported.ContentType, exported.FileName);
        });

        group.MapPost("/images", async (
            IFormFile file,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            await using var stream = file.OpenReadStream();
            var asset = await store.SaveImageAsync(file.FileName, file.ContentType, stream, cancellationToken);
            return Results.Created($"/api/document-editor/images/{asset.Id}", asset);
        }).DisableAntiforgery();

        group.MapGet("/images/{imageId}", (
            string imageId,
            DemoDocumentEditorStore store) =>
        {
            var image = store.GetImage(imageId);
            return image is null
                ? Results.NotFound()
                : Results.File(image.Content, image.ContentType, image.FileName);
        });

        group.MapGet("/documents/{documentId}/versions", async (
            string documentId,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            var versions = await store.GetVersionsAsync(documentId, cancellationToken);
            return Results.Ok(versions);
        });

        group.MapPost("/documents/{documentId}/versions", async (
            string documentId,
            DocumentVersionCreateRequest request,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            request.DocumentId = documentId;
            var version = await store.CreateVersionAsync(request, cancellationToken);
            return Results.Created($"/api/document-editor/documents/{documentId}/versions/{version.Id}", version);
        });

        group.MapPost("/documents/{documentId}/renditions", async (
            string documentId,
            DocumentRenditionRequest request,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            request.DocumentId = documentId;
            var result = await store.CreateRenditionAsync(request, cancellationToken);
            return result.Success && result.Rendition is not null
                ? Results.Created($"/api/document-editor/renditions/{result.Rendition.Id}", result)
                : Results.BadRequest(result);
        });

        group.MapGet("/renditions/{renditionId}", async (
            string renditionId,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            var rendition = await store.GetRenditionAsync(renditionId, cancellationToken);
            return rendition is null ? Results.NotFound() : Results.Ok(rendition);
        });

        group.MapGet("/renditions/{renditionId}/pages", async (
            string renditionId,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            var pages = await store.GetRenditionPagesAsync(renditionId, cancellationToken);
            return Results.Ok(pages);
        });

        group.MapGet("/renditions/{renditionId}/anchors", async (
            string renditionId,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            var anchors = await store.GetRenditionAnchorMapAsync(renditionId, cancellationToken);
            return Results.Ok(anchors);
        });

        group.MapGet("/documents/{documentId}/comments", async (
            string documentId,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            var comments = await store.GetCommentsAsync(documentId, cancellationToken);
            return Results.Ok(comments);
        });

        group.MapPost("/documents/{documentId}/comments", async (
            string documentId,
            DocumentComment comment,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            var created = await store.CreateCommentAsync(documentId, comment, cancellationToken);
            return Results.Created($"/api/document-editor/documents/{documentId}/comments/{created.Id}", created);
        });

        group.MapPost("/documents/{documentId}/comments/{commentId}/replies", async (
            string documentId,
            string commentId,
            DocumentCommentEntry entry,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            var updated = await store.AddCommentReplyAsync(documentId, commentId, entry, cancellationToken);
            return Results.Ok(updated);
        });

        group.MapPost("/documents/{documentId}/comments/{commentId}/resolve", async (
            string documentId,
            string commentId,
            DocumentEditorAuthor resolvedBy,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            var updated = await store.ResolveCommentAsync(documentId, commentId, resolvedBy, cancellationToken);
            return Results.Ok(updated);
        });

        group.MapPost("/documents/{documentId}/comments/{commentId}/reopen", async (
            string documentId,
            string commentId,
            DocumentEditorAuthor reopenedBy,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            var updated = await store.ReopenCommentAsync(documentId, commentId, reopenedBy, cancellationToken);
            return Results.Ok(updated);
        });

        group.MapPost("/documents/{documentId}/comments/{commentId}/delete", async (
            string documentId,
            string commentId,
            DocumentEditorAuthor deletedBy,
            DemoDocumentEditorStore store,
            CancellationToken cancellationToken) =>
        {
            await store.DeleteCommentAsync(documentId, commentId, deletedBy, cancellationToken);
            return Results.NoContent();
        });

        return app;
    }
}
