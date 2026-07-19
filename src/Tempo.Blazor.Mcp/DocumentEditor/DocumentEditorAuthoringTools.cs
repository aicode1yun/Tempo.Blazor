using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentFormats;
using Tempo.Blazor.DocumentFormats.Docx;
using Tempo.Blazor.DocumentFormats.Html;
using Tempo.Blazor.DocumentFormats.Markdown;
using Tempo.Blazor.DocumentFormats.Odt;

namespace Tempo.Blazor.Mcp.DocumentEditor;

/// <summary>
/// MCP authoring tools for DocumentEditor documents: create an empty document, import content
/// from markdown/HTML/DOCX/ODT (markdown is the primary agent authoring path — import rough
/// content, refine with the semantic edit tools) and export back (also the agent's verification
/// channel). All writes go through the provider save pipeline with optimistic concurrency.
/// </summary>
[McpServerToolType]
public static class DocumentEditorAuthoringTools
{
    private static readonly string[] TextFormats = ["markdown", "html"];
    private static readonly string[] BinaryFormats = ["docx", "odt"];

    [McpServerTool(Name = "document_editor_create")]
    [Description("Create a new empty DocumentEditor document with one empty paragraph (immediately addressable via describe/insert tools). Returns the new id, firstBlockId, concurrencyToken and contentDigest. Fails when the document id already exists.")]
    public static async Task<string> Create(
        IDocumentEditorProvider documents,
        [Description("Optional explicit document id; generated when omitted.")] string? documentId = null,
        [Description("Optional document title.")] string? title = null,
        [Description("Landscape page orientation.")] bool landscape = false,
        [Description("Optional full DocumentPageSettings JSON (persistence format) overriding the default page setup; 'landscape' still applies on top.")] string? pageSettingsJson = null)
    {
        var id = string.IsNullOrWhiteSpace(documentId) ? Guid.NewGuid().ToString("N") : documentId;
        var existing = await documents.LoadAsync(id, new DocumentEditorLoadOptions { IncludeDocument = false, IncludeJson = false });
        if (existing.Found)
        {
            return McpToolResults.Failure(
                McpToolResults.ValidationFailed,
                $"Document '{id}' already exists. Use document_editor_import to replace its content or omit documentId to create a fresh document.");
        }

        var document = DocumentEditorDocument.Empty(id);
        if (!string.IsNullOrWhiteSpace(title))
        {
            document.Metadata.Title = title;
        }

        if (!string.IsNullOrWhiteSpace(pageSettingsJson))
        {
            try
            {
                var pageSettings = JsonSerializer.Deserialize<DocumentPageSettings>(pageSettingsJson, DocumentEditorJson.Options);
                if (pageSettings is not null)
                {
                    document.PageSettings = pageSettings;
                }
            }
            catch (JsonException ex)
            {
                return McpToolResults.Failure(McpToolResults.ValidationFailed, $"pageSettingsJson could not be parsed: {ex.Message}");
            }
        }

        document.PageSettings.Landscape = landscape || document.PageSettings.Landscape;

        var firstBlock = new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Order = 0,
            Content = new ParagraphBlockContent()
        };
        document.Blocks.Add(firstBlock);

        return await SaveNewOrReplacedAsync(documents, id, document, expectedConcurrencyToken: null, force: false,
            extra => extra["firstBlockId"] = firstBlock.Id);
    }

    [McpServerTool(Name = "document_editor_import")]
    [Description("Import markdown, HTML, DOCX, or ODT content into a DocumentEditor document. markdown/html pass 'content' as plain text; docx/odt pass base64-encoded package bytes. Without documentId a new document is created; with documentId the existing document's content is REPLACED (pass expectedConcurrencyToken) or a new document with that id is created. Markdown is the primary agent authoring path: import rough content, then refine with the semantic edit tools.")]
    public static async Task<string> Import(
        IDocumentEditorProvider documents,
        [Description("Source format: markdown, html, docx, or odt.")] string format,
        [Description("Content: plain text for markdown/html, base64 package bytes for docx/odt.")] string content,
        [Description("Optional target document id — replaces the existing document or creates one with this id.")] string? documentId = null,
        [Description("Optional document title.")] string? title = null,
        [Description("Optional optimistic-concurrency token when replacing an existing document.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false)
    {
        var normalizedFormat = format.Trim().ToLowerInvariant();
        if (!TextFormats.Contains(normalizedFormat) && !BinaryFormats.Contains(normalizedFormat))
        {
            return McpToolResults.Failure(
                McpToolResults.InvalidOperation,
                $"Format '{format}' is not supported by document_editor_import. Supported formats: markdown, html, docx, odt.");
        }

        var id = string.IsNullOrWhiteSpace(documentId) ? Guid.NewGuid().ToString("N") : documentId;
        var existing = await documents.LoadAsync(id, new DocumentEditorLoadOptions { IncludeDocument = false, IncludeJson = false });
        if (existing.Found && McpConcurrency.TokenConflict(expectedConcurrencyToken, existing.ConcurrencyToken, "document_editor_describe_document") is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        DocumentEditorDocument document;
        var importWarnings = new List<object>();
        try
        {
            switch (normalizedFormat)
            {
                case "markdown":
                    document = new DocumentMarkdownImporter().Import(content, new DocumentMarkdownImportOptions
                    {
                        DocumentId = id,
                        Title = title
                    });
                    break;

                case "html":
                    document = new DocumentHtmlImporter().Import(content, new DocumentHtmlImportOptions
                    {
                        DocumentId = id,
                        Title = title
                    });
                    break;

                default:
                {
                    byte[] bytes;
                    try
                    {
                        bytes = Convert.FromBase64String(content);
                    }
                    catch (FormatException)
                    {
                        return McpToolResults.Failure(
                            McpToolResults.ValidationFailed,
                            $"Content for format '{normalizedFormat}' must be base64-encoded package bytes.");
                    }

                    using var stream = new MemoryStream(bytes);
                    var importOptions = new DocumentFormatImportOptions { DocumentId = id, FileName = title };
                    var result = normalizedFormat == "docx"
                        ? await new DocumentDocxImporter().ImportAsync(stream, importOptions)
                        : await new DocumentOdtImporter().ImportAsync(stream, importOptions);
                    document = result.Document;
                    importWarnings.AddRange(result.Warnings.Select(w => new
                    {
                        severity = w.Severity,
                        code = w.Code,
                        message = w.Message
                    }));
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return McpToolResults.Failure(
                McpToolResults.ValidationFailed,
                $"The {normalizedFormat} content could not be imported: {ex.Message}");
        }

        document.DocumentId = id;
        if (!string.IsNullOrWhiteSpace(title))
        {
            document.Metadata.Title = title;
        }

        return await SaveNewOrReplacedAsync(documents, id, document, expectedConcurrencyToken, force, extra =>
        {
            extra["format"] = normalizedFormat;
            extra["replacedExisting"] = existing.Found;
            extra["blockCount"] = document.Blocks.Count;
            extra["importWarnings"] = importWarnings;
        });
    }

    [McpServerTool(Name = "document_editor_export")]
    [Description("Export a DocumentEditor document to markdown, html, docx, or odt. markdown/html return 'content' text (agent verification channel); docx/odt return 'contentBase64' package bytes. Also returns the current concurrencyToken + contentDigest so the export can be paired with a document state.")]
    public static async Task<string> Export(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Target format: markdown, html, docx, or odt.")] string format,
        [Description("Wrap HTML export in a full document (<html>…); false returns a fragment.")] bool includeHtmlWrapper = false)
    {
        var normalizedFormat = format.Trim().ToLowerInvariant();
        if (!TextFormats.Contains(normalizedFormat) && !BinaryFormats.Contains(normalizedFormat))
        {
            return McpToolResults.Failure(
                McpToolResults.InvalidOperation,
                $"Format '{format}' is not supported by document_editor_export. Supported formats: markdown, html, docx, odt.");
        }

        var load = await documents.LoadAsync(documentId, new DocumentEditorLoadOptions { IncludeDocument = true, IncludeJson = false });
        if (!load.Found || load.Document is null)
        {
            return DocumentEditorSemanticCore.DocumentNotFound(load, documentId);
        }

        var document = load.Document;
        var baseline = new Dictionary<string, object?>
        {
            ["id"] = documentId,
            ["format"] = normalizedFormat,
            ["concurrencyToken"] = load.ConcurrencyToken,
            ["contentDigest"] = DocumentEditorDescribeTools.ComputeContentDigest(document)
        };

        try
        {
            switch (normalizedFormat)
            {
                case "markdown":
                    baseline["content"] = new DocumentMarkdownExporter().Export(document);
                    baseline["contentType"] = "text/markdown";
                    return McpToolResults.Success(baseline);

                case "html":
                    baseline["content"] = new DocumentHtmlExporter().Export(document, new DocumentHtmlExportOptions
                    {
                        IncludeDocumentWrapper = includeHtmlWrapper
                    });
                    baseline["contentType"] = "text/html";
                    return McpToolResults.Success(baseline);

                default:
                {
                    var exportOptions = new DocumentFormatExportOptions
                    {
                        FileName = string.IsNullOrWhiteSpace(document.Metadata.Title) ? documentId : document.Metadata.Title,
                        AllowImagePlaceholders = true
                    };
                    var result = normalizedFormat == "docx"
                        ? await new DocumentDocxExporter().ExportAsync(document, exportOptions)
                        : await new DocumentOdtExporter().ExportAsync(document, exportOptions);
                    baseline["contentBase64"] = Convert.ToBase64String(result.Content);
                    baseline["contentType"] = result.ContentType;
                    baseline["fileName"] = result.FileName;
                    baseline["exportWarnings"] = result.Warnings.Select(w => new
                    {
                        severity = w.Severity,
                        code = w.Code,
                        message = w.Message
                    }).ToList();
                    return McpToolResults.Success(baseline);
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return McpToolResults.Failure(
                McpToolResults.Error,
                $"The document could not be exported to {normalizedFormat}: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- shared save path

    private static async Task<string> SaveNewOrReplacedAsync(
        IDocumentEditorProvider documents,
        string documentId,
        DocumentEditorDocument document,
        string? expectedConcurrencyToken,
        bool force,
        Action<Dictionary<string, object?>>? extend = null)
    {
        var postFixWarnings = DocumentEditorMcpPostFixer.Fix(document);
        var validation = DocumentEditorValidationEngine.Validate(document);
        if (!validation.IsValid)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The document is invalid; nothing was saved.", validation.Errors);
        }

        var normalized = DocumentEditorJson.Serialize(document);
        var save = await documents.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = documentId,
            Document = document,
            JsonSnapshot = normalized,
            BaseConcurrencyToken = expectedConcurrencyToken,
            ConcurrencyMode = force
                ? DocumentEditorConcurrencyMode.Force
                : string.IsNullOrEmpty(expectedConcurrencyToken)
                    ? DocumentEditorConcurrencyMode.Optional
                    : DocumentEditorConcurrencyMode.Required,
            NormalizeJson = true
        });

        if (save.Conflict)
        {
            return McpToolResults.Failure(
                McpToolResults.Conflict,
                "The document was modified since you read it. Re-read with document_editor_describe_document and retry.");
        }

        if (!save.Success)
        {
            return McpToolResults.Failure(McpToolResults.Error, save.ErrorMessage ?? "The document could not be saved.");
        }

        var savedDocument = save.Document ?? document;
        var payload = new Dictionary<string, object?>
        {
            ["id"] = documentId,
            ["concurrencyToken"] = save.ConcurrencyToken,
            ["contentDigest"] = DocumentEditorDescribeTools.ComputeContentDigest(savedDocument),
            ["postFixWarnings"] = DocumentEditorMcpPostFixer.ToToolWarnings(postFixWarnings)
        };
        extend?.Invoke(payload);
        return McpToolResults.Success(payload);
    }
}
