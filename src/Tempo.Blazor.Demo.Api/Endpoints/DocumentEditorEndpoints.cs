using Microsoft.AspNetCore.SignalR;
using Tempo.Blazor.Demo.Api.Hubs;
using Tempo.Blazor.Demo.Api.Services;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.DocumentFormats.Odt;

namespace Tempo.Blazor.Demo.Api.Endpoints;

/// <summary>Demo endpoints for the document editor.</summary>
public static class DocumentEditorEndpoints
{
    /// <summary>Maps document editor demo endpoints.</summary>
    public static IEndpointRouteBuilder MapDocumentEditorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/document-editor").WithTags("Document Editor");

        group.MapPost("/reset", (
            DemoDocumentEditorStore store,
            InMemoryDocumentCollaborationProvider collaborationProvider,
            InMemoryDocumentSuggestionProvider suggestionProvider) =>
        {
            store.Reset();
            collaborationProvider.Reset();
            suggestionProvider.Reset();
            return Results.NoContent();
        });

        group.MapGet("/compare", async (
            string baseDocumentId,
            string compareDocumentId,
            DemoDocumentComparisonProvider comparisonProvider,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await comparisonProvider.CompareAsync(new DocumentCompareRequest
                {
                    DocumentId = baseDocumentId,
                    BaseSource = new DocumentCompareSource
                    {
                        Kind = DocumentCompareSourceKind.DocumentId,
                        DocumentId = baseDocumentId
                    },
                    CompareSource = new DocumentCompareSource
                    {
                        Kind = DocumentCompareSourceKind.DocumentId,
                        DocumentId = compareDocumentId
                    }
                }, cancellationToken);

                return Results.Ok(result);
            }
            catch
            {
                return Results.NotFound();
            }
        });

        group.MapPost("/compare", async (
            DocumentCompareRequest request,
            DemoDocumentComparisonProvider comparisonProvider,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await comparisonProvider.CompareAsync(request, cancellationToken);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new DocumentCompareResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        });

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

        group.MapPost("/formats/import", async (
            IFormFile file,
            DocumentFormatProviderKind format,
            DemoDocumentFormatProvider formatProvider,
            CancellationToken cancellationToken) =>
        {
            await using var stream = file.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            var imported = await formatProvider.ImportAsync(new DocumentFormatImportProviderRequest
            {
                DocumentId = Guid.NewGuid().ToString("N"),
                Format = format,
                FileName = file.FileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                Content = memory.ToArray()
            }, cancellationToken);

            return imported.Success ? Results.Ok(imported) : Results.BadRequest(imported);
        }).DisableAntiforgery();

        group.MapPost("/formats/export", async (
            DocumentFormatExportProviderRequest request,
            DemoDocumentFormatProvider formatProvider,
            CancellationToken cancellationToken) =>
        {
            var exported = await formatProvider.ExportAsync(request, cancellationToken);
            return exported.Success ? Results.Ok(exported) : Results.BadRequest(exported);
        });

        group.MapPost("/import/docx", async (
            IFormFile file,
            DemoDocumentEditorStore store,
            DemoDocumentFormatProvider formatProvider,
            CancellationToken cancellationToken) =>
        {
            await using var stream = file.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            var imported = await formatProvider.ImportAsync(new DocumentFormatImportProviderRequest
            {
                DocumentId = Guid.NewGuid().ToString("N"),
                Format = DocumentFormatProviderKind.Docx,
                FileName = file.FileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                Content = memory.ToArray()
            }, cancellationToken);
            if (!imported.Success || imported.Document is null)
            {
                return Results.BadRequest(imported);
            }

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
            DemoDocumentFormatProvider formatProvider,
            CancellationToken cancellationToken) =>
        {
            var loaded = await store.LoadAsync(documentId, new DocumentEditorLoadOptions { IncludeDocument = true }, cancellationToken);
            if (!loaded.Found || loaded.Document is null)
            {
                return Results.NotFound();
            }

            var exported = await formatProvider.ExportAsync(new DocumentFormatExportProviderRequest
            {
                DocumentId = documentId,
                Format = DocumentFormatProviderKind.Docx,
                Document = loaded.Document,
                FileName = loaded.Document.Metadata.Title
            }, cancellationToken);
            return Results.File(exported.Content, exported.ContentType, exported.FileName);
        });

        group.MapGet("/{documentId}/export/pdf", async (
            string documentId,
            DemoDocumentEditorStore store,
            DemoDocumentPdfExportProvider pdfProvider,
            CancellationToken cancellationToken) =>
        {
            var loaded = await store.LoadAsync(documentId, new DocumentEditorLoadOptions { IncludeDocument = true }, cancellationToken);
            if (!loaded.Found || loaded.Document is null)
            {
                return Results.NotFound();
            }

            var exported = await pdfProvider.ExportPdfAsync(new DocumentPdfExportRequest
            {
                DocumentId = documentId,
                Document = loaded.Document,
                FileName = loaded.Document.Metadata.Title,
                Options = CreatePdfExportOptions(loaded.Document)
            }, cancellationToken);
            return Results.File(exported.Content, exported.ContentType, exported.FileName);
        });

        group.MapPost("/{documentId}/export/pdf", async (
            string documentId,
            DocumentPdfExportRequest request,
            DemoDocumentPdfExportProvider pdfProvider,
            CancellationToken cancellationToken) =>
        {
            request.DocumentId = documentId;
            var exported = await pdfProvider.ExportPdfAsync(request, cancellationToken);
            return Results.Ok(exported);
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

        group.MapPost("/collaboration/join", async (
            DocumentCollaborationJoinRequest request,
            InMemoryDocumentCollaborationProvider collaboration,
            CancellationToken cancellationToken) =>
        {
            var session = await collaboration.JoinAsync(request, cancellationToken);
            return Results.Ok(session);
        });

        group.MapPost("/collaboration/{sessionId}/leave", async (
            string sessionId,
            InMemoryDocumentCollaborationProvider collaboration,
            CancellationToken cancellationToken) =>
        {
            await collaboration.LeaveAsync(sessionId, cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/collaboration/{sessionId}/batches", async (
            string sessionId,
            DocumentOperationBatch batch,
            InMemoryDocumentCollaborationProvider collaboration,
            IHubContext<DocumentEditorCollaborationHub> hubContext,
            CancellationToken cancellationToken) =>
        {
            var broadcast = await collaboration.BroadcastOperationBatchAsync(sessionId, batch, cancellationToken);
            await hubContext.Clients.Group(DocumentEditorCollaborationHub.DocumentGroup(broadcast.Batch.DocumentId))
                .SendAsync(SignalRDocumentCollaborationProvider.HubMethods.RemoteOperationBatchReceived, broadcast, cancellationToken);
            return Results.Ok(broadcast);
        });

        group.MapGet("/collaboration/documents/{documentId}/batches", async (
            string documentId,
            long afterSequence,
            InMemoryDocumentCollaborationProvider collaboration,
            CancellationToken cancellationToken) =>
        {
            var batches = await collaboration.GetOperationBatchesAsync(documentId, afterSequence, cancellationToken);
            return Results.Ok(batches);
        });

        group.MapPost("/collaboration/cursors", async (
            DocumentCollaborationCursor cursor,
            InMemoryDocumentCollaborationProvider collaboration,
            IHubContext<DocumentEditorCollaborationHub> hubContext,
            CancellationToken cancellationToken) =>
        {
            await collaboration.BroadcastCursorAsync(cursor, cancellationToken);
            await hubContext.Clients.Group(DocumentEditorCollaborationHub.DocumentGroup(cursor.DocumentId))
                .SendAsync(SignalRDocumentCollaborationProvider.HubMethods.RemoteCursorReceived, cursor, cancellationToken);
            return Results.Ok(cursor);
        });

        group.MapGet("/collaboration/documents/{documentId}/cursors", async (
            string documentId,
            InMemoryDocumentCollaborationProvider collaboration,
            CancellationToken cancellationToken) =>
        {
            var cursors = await collaboration.GetCursorsAsync(documentId, cancellationToken);
            return Results.Ok(cursors);
        });

        group.MapGet("/suggestions/documents/{documentId}", async (
            string documentId,
            DocumentSuggestionStatus? status,
            InMemoryDocumentSuggestionProvider suggestions,
            CancellationToken cancellationToken) =>
        {
            var items = await suggestions.GetSuggestionsAsync(new DocumentSuggestionQuery
            {
                DocumentId = documentId,
                Status = status
            }, cancellationToken);
            return Results.Ok(items);
        });

        group.MapPost("/suggestions", async (
            DocumentSuggestion suggestion,
            InMemoryDocumentSuggestionProvider suggestions,
            CancellationToken cancellationToken) =>
        {
            var created = await suggestions.CreateSuggestionAsync(suggestion, cancellationToken);
            return Results.Created($"/api/document-editor/suggestions/{created.Id}", created);
        });

        group.MapPost("/suggestions/review", async (
            DocumentSuggestionReviewRequest request,
            InMemoryDocumentSuggestionProvider suggestions,
            CancellationToken cancellationToken) =>
        {
            var reviewed = await suggestions.ReviewSuggestionAsync(request, cancellationToken);
            return Results.Ok(reviewed);
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

    private static DocumentPdfExportOptions CreatePdfExportOptions(DocumentEditorDocument document)
    {
        var pageSettings = document.PageSettings ?? new DocumentPageSettings();
        return new DocumentPdfExportOptions
        {
            IncludeComments = true,
            IncludeSuggestions = true,
            ReviewDisplayMode = DocumentReviewDisplayMode.AllMarkup,
            PageSetup = new DocumentPdfPageSetupOptions
            {
                PageSize = pageSettings.Size ?? DocumentPageSize.A4,
                Orientation = pageSettings.Landscape
                    ? DocumentPdfPageOrientation.Landscape
                    : DocumentPdfPageOrientation.Portrait,
                Margins = pageSettings.Margins ?? DocumentPageMargins.Default
            }
        };
    }
}
