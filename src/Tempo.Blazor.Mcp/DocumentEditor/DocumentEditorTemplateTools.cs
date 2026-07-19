using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.DocumentFormats.HeadlessLayout;

namespace Tempo.Blazor.Mcp.DocumentEditor;

/// <summary>
/// MCP template/assembly tools: author document templates (tokens, IF/ELSEIF/ELSE conditional
/// chains, repeating sections via <see cref="DocumentAssemblyMetadata"/>), introspect them
/// (document_template_describe) and assemble+render them with token values
/// (document_assemble_render — IF/ELSE evaluation, repeat expansion, computed expressions over
/// the injected clock). Authoring compiles into canonical insertBlock/updateBlock/deleteBlock
/// operations applied through the same pipeline as document_editor_apply_operations.
/// </summary>
[McpServerToolType]
public static class DocumentEditorTemplateTools
{
    [McpServerTool(Name = "document_editor_insert_token")]
    [Description("Insert an assembly token (TokenRun) into a text block at a plain-text offset. Tokens resolve at assembly time by key; optional expression computes the value (SUM/COUNT/CURRENCY/TODAY/DATEADD, arithmetic — see docs/document-canonical-model.md). When the host registers IDocumentTokenValueProvider, the key is validated against it (disable with validateKey=false).")]
    public static async Task<string> InsertToken(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Target text block id.")] string blockId,
        [Description("Plain-text offset to insert the token at (0..textLength).")] int offset,
        [Description("Stable token key, e.g. tenant.name.")] string key,
        [Description("Display label shown in the editor; defaults to the key.")] string? displayName = null,
        [Description("Optional token type metadata (text, date, number, url…).")] string? tokenType = null,
        [Description("Fallback text rendered when the token has no value.")] string? fallbackText = null,
        [Description("Optional computed assembly expression, e.g. CURRENCY(SUM(items,'price'),'cs-CZ','CZK').")] string? expression = null,
        [Description("Table cell id when the block is nested in a table cell.")] string? tableCellId = null,
        [Description("Validate the key against the host token provider when one is registered.")] bool validateKey = true,
        [Description("Optional optimistic-concurrency token.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false,
        IDocumentEditorMcpCollaborationBridge? collaborationBridge = null,
        IDocumentTokenValueProvider? tokenProvider = null,
        CancellationToken cancellationToken = default)
    {
        var load = await documents.LoadAsync(documentId, new DocumentEditorLoadOptions { IncludeDocument = true, IncludeJson = false });
        if (!load.Found || load.Document is null)
        {
            return DocumentEditorSemanticCore.DocumentNotFound(load, documentId);
        }

        if (McpConcurrency.TokenConflict(expectedConcurrencyToken, load.ConcurrencyToken, "document_editor_describe_document") is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        var block = DocumentEditorSemanticCore.FindBlock(load.Document, blockId, tableCellId);
        if (block is null)
        {
            return DocumentEditorSemanticCore.BlockNotFound(blockId, tableCellId);
        }

        var inlines = DocumentEditorSemanticCore.GetInlineList(block.Content);
        if (inlines is null)
        {
            return McpToolResults.Failure(
                McpToolResults.InvalidOperation,
                $"Block '{blockId}' has no inline text. Tokens can be inserted into paragraph, heading, list, and quote blocks.");
        }

        var total = DocumentEditorSemanticCore.PlainTextOf(inlines).Length;
        if (offset < 0 || offset > total)
        {
            return McpToolResults.Failure(
                McpToolResults.ValidationFailed,
                $"Insert offset {offset} is outside the block's plain text (textLength {total}).");
        }

        var token = new TokenRun
        {
            Key = key,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? key : displayName,
            TokenType = tokenType,
            FallbackText = fallbackText,
            Expression = string.IsNullOrWhiteSpace(expression) ? null : expression
        };

        if (validateKey && tokenProvider is not null && string.IsNullOrWhiteSpace(expression))
        {
            var values = await tokenProvider.ResolveTokenValuesAsync(
                new DocumentTokenResolutionContext { DocumentId = documentId },
                [token],
                cancellationToken);
            if (!values.ContainsKey(key))
            {
                return McpToolResults.Failure(
                    McpToolResults.ValidationFailed,
                    $"Token key '{key}' is not known to the host token provider. Pass validateKey=false to insert it anyway (it will render its fallback text until the host supplies a value).");
            }
        }

        // Compiled as a whole-block replace: split the text run at the offset and put the token
        // between the halves (there is no lower-level insert-inline operation; updateBlock
        // persistence payloads are normalized by the JS applier — convergence-tested).
        var replacement = McpJsonHelpers.Clone(block, DocumentEditorJson.Options);
        var newInlines = DocumentEditorSemanticCore.GetInlineList(replacement.Content)!;
        InsertInlineAtPlainOffset(newInlines, offset, token);

        var operation = new DocumentOperation
        {
            Type = DocumentOperationType.UpdateBlock,
            Target = new DocumentOperationTarget { BlockId = blockId, TableCellId = tableCellId },
            Block = replacement
        };

        return await DocumentEditorSemanticCore.ApplyAsync(
            documents, documentId, load, [operation], expectedConcurrencyToken, force,
            _ => new Dictionary<string, object?> { ["blockId"] = blockId, ["tokenKey"] = key },
            collaborationBridge);
    }

    [McpServerTool(Name = "document_editor_wrap_conditional")]
    [Description("Wrap ranges of TOP-LEVEL body blocks into an IF/ELSEIF/ELSE conditional chain of content controls (DocumentAssemblyMetadata), or update the branch/expression of an existing conditional control (pass existingControlBlockId). branchesJson: [{\"branch\":\"if|elseif|else\",\"expression\":\"contract.amount > 10000\",\"blockIds\":[\"p1\"]}]. At assembly time the first truthy branch survives, the rest are dropped. Note: blocks inside content controls are no longer operation-addressable — finish text edits first or replace via document_editor_update_block on the control.")]
    public static async Task<string> WrapConditional(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Branches JSON array (wrap mode): branch, optional expression, blockIds of top-level body blocks to wrap.")] string? branchesJson = null,
        [Description("Existing conditional control block id to update instead of wrapping.")] string? existingControlBlockId = null,
        [Description("Update mode: new branch kind (if, elseif, else).")] string? branch = null,
        [Description("Update mode: new condition expression.")] string? expression = null,
        [Description("Optional chain group id; generated when omitted (wrap mode).")] string? groupId = null,
        [Description("Optional optimistic-concurrency token.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false,
        IDocumentEditorMcpCollaborationBridge? collaborationBridge = null)
    {
        var load = await documents.LoadAsync(documentId, new DocumentEditorLoadOptions { IncludeDocument = true, IncludeJson = false });
        if (!load.Found || load.Document is null)
        {
            return DocumentEditorSemanticCore.DocumentNotFound(load, documentId);
        }

        if (McpConcurrency.TokenConflict(expectedConcurrencyToken, load.ConcurrencyToken, "document_editor_describe_document") is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        if (!string.IsNullOrWhiteSpace(existingControlBlockId))
        {
            return await UpdateConditionalAsync(documents, documentId, load, existingControlBlockId, branch, expression, expectedConcurrencyToken, force, collaborationBridge);
        }

        if (string.IsNullOrWhiteSpace(branchesJson))
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "Pass branchesJson (wrap mode) or existingControlBlockId (update mode).");
        }

        List<BranchSpec>? branches;
        try
        {
            branches = JsonSerializer.Deserialize<List<BranchSpec>>(branchesJson, McpJson.Options);
        }
        catch (JsonException ex)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, $"branchesJson could not be parsed: {ex.Message}");
        }

        if (branches is null || branches.Count == 0)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "branchesJson must contain at least one branch.");
        }

        var validationError = ValidateBranches(branches);
        if (validationError is not null)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, validationError);
        }

        var topLevel = load.Document.Blocks.ToDictionary(b => b.Id, StringComparer.Ordinal);
        foreach (var missing in branches.SelectMany(b => b.BlockIds).Where(id => !topLevel.ContainsKey(id)))
        {
            return McpToolResults.Failure(
                McpToolResults.NotFound,
                $"Block '{missing}' was not found among TOP-LEVEL body blocks. Conditional chains wrap body blocks only; use document_editor_describe_document to inspect addresses.");
        }

        var chainId = string.IsNullOrWhiteSpace(groupId) ? Guid.NewGuid().ToString("N") : groupId;
        var anchorOrder = branches.SelectMany(b => b.BlockIds).Min(id => topLevel[id].Order);
        var operations = new List<DocumentOperation>();
        var controlBlockIds = new List<string>();

        for (var index = 0; index < branches.Count; index++)
        {
            var spec = branches[index];
            var control = DocumentAssemblyMetadata.CreateConditionalBlock(
                spec.Branch.ToLowerInvariant(), spec.Expression, chainId);
            var controlBlockId = Guid.NewGuid().ToString("N");
            controlBlockIds.Add(controlBlockId);
            var order = anchorOrder + index * 1e-6;
            operations.Add(new DocumentOperation
            {
                Type = DocumentOperationType.InsertBlock,
                Target = new DocumentOperationTarget { Order = order },
                Block = new DocumentBlock
                {
                    Id = controlBlockId,
                    Type = DocumentBlockType.ContentControl,
                    Order = order,
                    Content = new ContentControlBlockContent
                    {
                        Control = control,
                        Blocks = spec.BlockIds
                            .Select(id => McpJsonHelpers.Clone(topLevel[id], DocumentEditorJson.Options))
                            .ToList()
                    }
                }
            });
        }

        operations.AddRange(branches.SelectMany(b => b.BlockIds).Distinct().Select(id => new DocumentOperation
        {
            Type = DocumentOperationType.DeleteBlock,
            Target = new DocumentOperationTarget { BlockId = id }
        }));

        return await DocumentEditorSemanticCore.ApplyAsync(
            documents, documentId, load, operations, expectedConcurrencyToken, force,
            _ => new Dictionary<string, object?> { ["groupId"] = chainId, ["controlBlockIds"] = controlBlockIds },
            collaborationBridge);
    }

    [McpServerTool(Name = "document_editor_insert_repeating_section")]
    [Description("Insert a repeating section bound to a collection token: at assembly time the row template is cloned once per row of tokenValues[bindKey].rows, with row columns exposed as token keys inside the clone. Provide the row template as rowText (single paragraph) or rowBlocksJson (full persistence DocumentBlock array, e.g. paragraphs with item tokens).")]
    public static async Task<string> InsertRepeatingSection(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId,
        [Description("Collection token key the section binds to, e.g. items.")] string bindKey,
        [Description("Simple row template: a single paragraph with this text.")] string? rowText = null,
        [Description("Full row template: JSON array of persistence DocumentBlock payloads.")] string? rowBlocksJson = null,
        [Description("Body order value for the new section; omit to append at the end.")] double? order = null,
        [Description("Optional optimistic-concurrency token.")] string? expectedConcurrencyToken = null,
        [Description("Overwrite without concurrency token validation.")] bool force = false,
        IDocumentEditorMcpCollaborationBridge? collaborationBridge = null)
    {
        var load = await documents.LoadAsync(documentId, new DocumentEditorLoadOptions { IncludeDocument = true, IncludeJson = false });
        if (!load.Found || load.Document is null)
        {
            return DocumentEditorSemanticCore.DocumentNotFound(load, documentId);
        }

        if (McpConcurrency.TokenConflict(expectedConcurrencyToken, load.ConcurrencyToken, "document_editor_describe_document") is { } conflict)
        {
            return McpToolResults.Failure(McpToolResults.Conflict, conflict);
        }

        List<DocumentBlock> rowBlocks;
        if (!string.IsNullOrWhiteSpace(rowBlocksJson))
        {
            try
            {
                rowBlocks = JsonSerializer.Deserialize<List<DocumentBlock>>(rowBlocksJson, DocumentEditorJson.Options) ?? [];
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                return McpToolResults.Failure(McpToolResults.ValidationFailed, $"rowBlocksJson could not be parsed: {ex.Message}");
            }
        }
        else if (!string.IsNullOrWhiteSpace(rowText))
        {
            rowBlocks =
            [
                new DocumentBlock
                {
                    Type = DocumentBlockType.Paragraph,
                    Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = rowText }] }
                }
            ];
        }
        else
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "Pass rowText or rowBlocksJson as the row template.");
        }

        if (rowBlocks.Count == 0)
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "The row template must contain at least one block.");
        }

        var resolvedOrder = order ?? (load.Document.Blocks.Count == 0 ? 0 : load.Document.Blocks.Max(b => b.Order) + 1);
        var controlBlockId = Guid.NewGuid().ToString("N");
        var operation = new DocumentOperation
        {
            Type = DocumentOperationType.InsertBlock,
            Target = new DocumentOperationTarget { Order = resolvedOrder },
            Block = new DocumentBlock
            {
                Id = controlBlockId,
                Type = DocumentBlockType.ContentControl,
                Order = resolvedOrder,
                Content = new ContentControlBlockContent
                {
                    Control = DocumentAssemblyMetadata.CreateRepeatingSection(bindKey),
                    Blocks = rowBlocks
                }
            }
        };

        return await DocumentEditorSemanticCore.ApplyAsync(
            documents, documentId, load, [operation], expectedConcurrencyToken, force,
            _ => new Dictionary<string, object?> { ["controlBlockId"] = controlBlockId, ["bindKey"] = bindKey, ["order"] = resolvedOrder },
            collaborationBridge);
    }

    [McpServerTool(Name = "document_template_describe")]
    [Description("Describe the template structure of a DocumentEditor document for agents: aggregated tokens (key, displayName, expression, fallbackText, occurrences), conditional IF/ELSEIF/ELSE chains (group, branches with expressions and block counts) and repeating sections (bindKey, row template block count).")]
    public static async Task<string> TemplateDescribe(
        IDocumentEditorProvider documents,
        [Description("DocumentEditor document id.")] string documentId)
    {
        var load = await documents.LoadAsync(documentId, new DocumentEditorLoadOptions { IncludeDocument = true, IncludeJson = false });
        if (!load.Found || load.Document is null)
        {
            return DocumentEditorSemanticCore.DocumentNotFound(load, documentId);
        }

        var document = load.Document;
        var tokens = new List<(TokenRun Token, string BlockId)>();
        var conditionals = new List<(string BlockId, string Branch, string? Expression, string Group, int BlockCount, double Order)>();
        var repeats = new List<(string BlockId, string BindKey, int BlockCount)>();

        void Walk(DocumentBlock block)
        {
            switch (block.Content)
            {
                case ContentControlBlockContent control:
                    control.Control.Metadata.TryGetValue(DocumentAssemblyMetadata.BranchKey, out var branchKind);
                    control.Control.Metadata.TryGetValue(DocumentAssemblyMetadata.ExpressionKey, out var expr);
                    control.Control.Metadata.TryGetValue(DocumentAssemblyMetadata.GroupKey, out var group);
                    control.Control.Metadata.TryGetValue(DocumentAssemblyMetadata.BindKey, out var bind);
                    if (!string.IsNullOrEmpty(branchKind))
                    {
                        conditionals.Add((block.Id, branchKind!, expr, group ?? string.Empty, control.Blocks.Count, block.Order));
                    }
                    else if (!string.IsNullOrEmpty(bind))
                    {
                        repeats.Add((block.Id, bind!, control.Blocks.Count));
                    }

                    control.Blocks.ForEach(Walk);
                    break;

                case TableBlockContent table:
                    foreach (var nested in table.Rows.SelectMany(r => r.Cells).SelectMany(c => c.Blocks))
                    {
                        Walk(nested);
                    }

                    break;

                default:
                    var inlines = DocumentEditorSemanticCore.GetInlineList(block.Content);
                    if (inlines is not null)
                    {
                        tokens.AddRange(inlines.OfType<TokenRun>().Select(t => (t, block.Id)));
                    }

                    break;
            }
        }

        document.Blocks.ForEach(Walk);
        foreach (var hf in document.HeadersFooters)
        {
            hf.Blocks.ForEach(Walk);
        }

        return McpToolResults.Success(new
        {
            id = documentId,
            concurrencyToken = load.ConcurrencyToken,
            contentDigest = DocumentEditorDescribeTools.ComputeContentDigest(document),
            tokens = tokens
                .GroupBy(t => t.Token.Key, StringComparer.Ordinal)
                .Select(g => new
                {
                    key = g.Key,
                    displayName = g.First().Token.DisplayName,
                    tokenType = g.First().Token.TokenType,
                    expression = g.Select(t => t.Token.Expression).FirstOrDefault(e => !string.IsNullOrEmpty(e)),
                    fallbackText = g.Select(t => t.Token.FallbackText).FirstOrDefault(f => !string.IsNullOrEmpty(f)),
                    occurrences = g.Select(t => new { blockId = t.BlockId }).ToList()
                })
                .ToList(),
            conditionalChains = conditionals
                .GroupBy(c => c.Group, StringComparer.Ordinal)
                .Select(g => new
                {
                    groupId = g.Key,
                    branches = g.OrderBy(c => c.Order).Select(c => new
                    {
                        blockId = c.BlockId,
                        branch = c.Branch,
                        expression = c.Expression,
                        blockCount = c.BlockCount
                    }).ToList()
                })
                .ToList(),
            repeatingSections = repeats.Select(r => new
            {
                blockId = r.BlockId,
                bindKey = r.BindKey,
                rowBlockCount = r.BlockCount
            }).ToList()
        });
    }

    [McpServerTool(Name = "document_assemble_render")]
    [Description("Assemble a template with token values and render the result: IF/ELSEIF/ELSE chains evaluate, repeating sections expand per row, computed expressions calculate (TODAY over the injected clock). tokenValuesJson: {\"key\": \"scalar\", \"items\": {\"rows\": [{\"col\": \"val\"}]}, \"other\": {\"value\": \"v\", \"displayValue\": \"V\"}}. output png returns page previews, pdf returns base64 PDF (includeLayoutText adds the laid-out text for verification).")]
    public static async Task<string> AssembleRender(
        IDocumentEditorProvider documents,
        ITempoDocumentService renderer,
        ITempoDocumentMcpFontCatalog fontCatalog,
        TempoDocumentMcpRenderOptions renderOptions,
        [Description("DocumentEditor document id. Required when documentJson is omitted.")] string? documentId = null,
        [Description("Optional full template document JSON to assemble without loading from the provider.")] string? documentJson = null,
        [Description("Token values JSON object: scalars, {value, displayValue, tokenType} objects, or {rows: [...]} collections.")] string? tokenValuesJson = null,
        [Description("Output: png (page previews) or pdf.")] string output = "png",
        [Description("Raster DPI for png output.")] double dpi = 96,
        [Description("Maximum png pages returned; 0 uses the configured MaxPreviewPages.")] int maxPages = 0,
        [Description("pdf output: include the laid-out plain text (verification channel).")] bool includeLayoutText = false,
        CancellationToken cancellationToken = default)
    {
        var normalizedOutput = output.Trim().ToLowerInvariant();
        if (normalizedOutput is not ("png" or "pdf"))
        {
            return McpToolResults.Failure(McpToolResults.InvalidOperation, $"Output '{output}' is not supported; use png or pdf.");
        }

        DocumentEditorDocument document;
        if (!string.IsNullOrWhiteSpace(documentJson))
        {
            try
            {
                document = DocumentEditorJson.Deserialize(documentJson);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                return McpToolResults.Failure(McpToolResults.ValidationFailed, $"documentJson could not be parsed: {ex.Message}");
            }
        }
        else if (!string.IsNullOrWhiteSpace(documentId))
        {
            var load = await documents.LoadAsync(documentId, new DocumentEditorLoadOptions { IncludeDocument = true, IncludeJson = false });
            if (!load.Found || load.Document is null)
            {
                return DocumentEditorSemanticCore.DocumentNotFound(load, documentId);
            }

            document = load.Document;
        }
        else
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "Pass either documentId or documentJson.");
        }

        Dictionary<string, DocumentTokenValue>? tokenValues = null;
        if (!string.IsNullOrWhiteSpace(tokenValuesJson))
        {
            try
            {
                tokenValues = ParseTokenValues(tokenValuesJson);
            }
            catch (JsonException ex)
            {
                return McpToolResults.Failure(McpToolResults.ValidationFailed, $"tokenValuesJson could not be parsed: {ex.Message}");
            }
        }

        if (fontCatalog.Fonts.Count == 0)
        {
            return McpToolResults.Failure(
                McpToolResults.Unsupported,
                "No fonts are configured for headless rendering. Register fonts via AddTempoDocumentEditorMcpRendering(options => options.Fonts.Add(...)) or enable IncludeSystemFontFallback.");
        }

        var request = new TempoDocumentRenderRequest
        {
            Document = document,
            TokenValues = tokenValues,
            Fonts = fontCatalog.Fonts,
            DocumentId = document.DocumentId,
            ImageResolver = renderOptions.ImageResolver
        };

        try
        {
            if (normalizedOutput == "pdf")
            {
                var result = await renderer.RenderPdfAsync(request, cancellationToken);
                return McpToolResults.Success(new Dictionary<string, object?>
                {
                    ["id"] = document.DocumentId,
                    ["pageCount"] = result.PageCount,
                    ["contentType"] = "application/pdf",
                    ["base64"] = Convert.ToBase64String(result.PdfContent),
                    ["layoutText"] = includeLayoutText ? ExtractLayoutText(result.LayoutSnapshotJson) : null
                });
            }

            var pages = await renderer.RenderPageImagesAsync(request, dpi, cancellationToken);
            var cap = maxPages > 0 ? maxPages : renderOptions.MaxPreviewPages;
            return McpToolResults.Success(new
            {
                id = document.DocumentId,
                pageCount = pages.Count,
                truncated = pages.Count > cap,
                renderedPages = pages.Take(cap).Select(page => new
                {
                    pageNumber = page.PageIndex + 1,
                    width = page.Width,
                    height = page.Height,
                    contentType = "image/png",
                    base64 = Convert.ToBase64String(page.Png)
                }).ToList()
            });
        }
        catch (TempoDocumentLayoutException ex)
        {
            return McpToolResults.Failure(
                McpToolResults.InvalidOperation,
                $"{ex.Message} Configure the missing font faces via AddTempoDocumentEditorMcpRendering options.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return McpToolResults.Failure(McpToolResults.Error, $"The template could not be assembled/rendered: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- helpers

    private sealed class BranchSpec
    {
        public string Branch { get; set; } = string.Empty;
        public string? Expression { get; set; }
        public List<string> BlockIds { get; set; } = [];
    }

    private static string? ValidateBranches(List<BranchSpec> branches)
    {
        for (var index = 0; index < branches.Count; index++)
        {
            var spec = branches[index];
            var kind = spec.Branch.Trim().ToLowerInvariant();
            if (kind is not ("if" or "elseif" or "else"))
            {
                return $"Branch '{spec.Branch}' is invalid; use if, elseif, or else.";
            }

            if (index == 0 && kind != "if")
            {
                return "The first branch of a conditional chain must be 'if'.";
            }

            if (index > 0 && kind == "if")
            {
                return "Only the first branch may be 'if'; use elseif/else for later branches.";
            }

            if (kind is "if" or "elseif" && string.IsNullOrWhiteSpace(spec.Expression))
            {
                return $"Branch '{kind}' requires an expression.";
            }

            if (kind == "else" && index != branches.Count - 1)
            {
                return "'else' must be the last branch of the chain.";
            }

            if (spec.BlockIds.Count == 0)
            {
                return $"Branch '{kind}' must wrap at least one block.";
            }
        }

        return null;
    }

    private static async Task<string> UpdateConditionalAsync(
        IDocumentEditorProvider documents,
        string documentId,
        DocumentEditorLoadResult load,
        string controlBlockId,
        string? branch,
        string? expression,
        string? expectedConcurrencyToken,
        bool force,
        IDocumentEditorMcpCollaborationBridge? collaborationBridge)
    {
        var block = DocumentEditorSemanticCore.FindBlock(load.Document!, controlBlockId, tableCellId: null);
        if (block is null)
        {
            return DocumentEditorSemanticCore.BlockNotFound(controlBlockId, tableCellId: null);
        }

        if (block.Content is not ContentControlBlockContent control
            || !control.Control.Metadata.ContainsKey(DocumentAssemblyMetadata.BranchKey))
        {
            return McpToolResults.Failure(
                McpToolResults.InvalidOperation,
                $"Block '{controlBlockId}' is not a conditional content control. Use document_template_describe to list conditional chains.");
        }

        if (string.IsNullOrWhiteSpace(branch) && string.IsNullOrWhiteSpace(expression))
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, "Update mode: pass branch and/or expression to change.");
        }

        var newBranch = string.IsNullOrWhiteSpace(branch)
            ? control.Control.Metadata[DocumentAssemblyMetadata.BranchKey]!
            : branch.Trim().ToLowerInvariant();
        if (newBranch is not ("if" or "elseif" or "else"))
        {
            return McpToolResults.Failure(McpToolResults.ValidationFailed, $"Branch '{branch}' is invalid; use if, elseif, or else.");
        }

        var newExpression = expression ?? (control.Control.Metadata.TryGetValue(DocumentAssemblyMetadata.ExpressionKey, out var existing) ? existing : null);
        var groupId = control.Control.Metadata.TryGetValue(DocumentAssemblyMetadata.GroupKey, out var group) && !string.IsNullOrEmpty(group)
            ? group!
            : Guid.NewGuid().ToString("N");

        var replacement = McpJsonHelpers.Clone(block, DocumentEditorJson.Options);
        var replacementControl = ((ContentControlBlockContent)replacement.Content).Control;
        var patched = DocumentAssemblyMetadata.CreateConditionalBlock(newBranch, newExpression, groupId);
        replacementControl.Alias = patched.Alias;
        replacementControl.Metadata[DocumentAssemblyMetadata.BranchKey] = newBranch;
        replacementControl.Metadata[DocumentAssemblyMetadata.ExpressionKey] = newExpression;
        replacementControl.Metadata[DocumentAssemblyMetadata.GroupKey] = groupId;

        var operation = new DocumentOperation
        {
            Type = DocumentOperationType.UpdateBlock,
            Target = new DocumentOperationTarget { BlockId = controlBlockId },
            Block = replacement
        };

        return await DocumentEditorSemanticCore.ApplyAsync(
            documents, documentId, load, [operation], expectedConcurrencyToken, force,
            _ => new Dictionary<string, object?> { ["controlBlockId"] = controlBlockId, ["branch"] = newBranch, ["expression"] = newExpression },
            collaborationBridge);
    }

    private static void InsertInlineAtPlainOffset(List<InlineContent> inlines, int offset, InlineContent inline)
    {
        var plainStart = 0;
        for (var index = 0; index < inlines.Count; index++)
        {
            if (inlines[index] is not TextRun run)
            {
                continue;
            }

            var plainEnd = plainStart + run.Text.Length;
            if (offset <= plainEnd)
            {
                var local = offset - plainStart;
                if (local <= 0)
                {
                    inlines.Insert(index, inline);
                }
                else if (local >= run.Text.Length)
                {
                    inlines.Insert(index + 1, inline);
                }
                else
                {
                    var tail = new TextRun { Text = run.Text[local..], Marks = run.Marks.Select(m => McpJsonHelpers.Clone(m, DocumentEditorJson.Options)).ToList() };
                    run.Text = run.Text[..local];
                    inlines.Insert(index + 1, inline);
                    inlines.Insert(index + 2, tail);
                }

                return;
            }

            plainStart = plainEnd;
        }

        inlines.Add(inline);
    }

    private static Dictionary<string, DocumentTokenValue> ParseTokenValues(string tokenValuesJson)
    {
        using var parsed = JsonDocument.Parse(tokenValuesJson);
        if (parsed.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("tokenValuesJson must be a JSON object keyed by token key.");
        }

        var values = new Dictionary<string, DocumentTokenValue>(StringComparer.Ordinal);
        foreach (var property in parsed.RootElement.EnumerateObject())
        {
            values[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.Null => DocumentTokenValue.Missing(property.Name),
                JsonValueKind.Object => ParseTokenValueObject(property.Name, property.Value),
                _ => DocumentTokenValue.Resolved(property.Name, property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()
                    : property.Value.GetRawText())
            };
        }

        return values;
    }

    private static DocumentTokenValue ParseTokenValueObject(string key, JsonElement element)
    {
        var value = new DocumentTokenValue { Key = key };
        if (element.TryGetProperty("value", out var rawValue))
        {
            value.Value = rawValue.ValueKind == JsonValueKind.String ? rawValue.GetString() : rawValue.GetRawText();
        }

        value.DisplayValue = element.TryGetProperty("displayValue", out var display) && display.ValueKind == JsonValueKind.String
            ? display.GetString()
            : value.Value;
        if (element.TryGetProperty("tokenType", out var tokenType) && tokenType.ValueKind == JsonValueKind.String)
        {
            value.TokenType = tokenType.GetString();
        }

        if (element.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
        {
            value.Rows = rows.EnumerateArray()
                .Where(row => row.ValueKind == JsonValueKind.Object)
                .Select(row => row.EnumerateObject().ToDictionary(
                    p => p.Name,
                    p => (string?)(p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText())))
                .ToList();
        }

        value.HasValue = !string.IsNullOrWhiteSpace(value.DisplayValue ?? value.Value) || value.Rows is { Count: > 0 };
        return value;
    }

    private static string ExtractLayoutText(string layoutSnapshotJson)
    {
        using var snapshot = JsonDocument.Parse(layoutSnapshotJson);
        var builder = new StringBuilder();
        foreach (var page in snapshot.RootElement.GetProperty("pages").EnumerateArray())
        {
            foreach (var command in page.GetProperty("commands").EnumerateArray())
            {
                if (command.GetProperty("type").GetString() == "text" && command.TryGetProperty("text", out var text))
                {
                    builder.Append(text.GetString()).Append(' ');
                }
            }
        }

        return builder.ToString();
    }
}
