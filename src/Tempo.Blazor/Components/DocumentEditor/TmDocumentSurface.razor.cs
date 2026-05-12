using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.DocumentEditor.Commands;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Components.DocumentEditor;

/// <summary>Read-only document surface that renders document editor blocks onto a page-like canvas.</summary>
public partial class TmDocumentSurface : ComponentBase, IAsyncDisposable
{
    private ElementReference _rootElement;
    private DotNetObjectReference<TmDocumentSurface>? _dotNetReference;
    private bool _pasteHookAttached;
    private bool _insertPanelOpen;
    private bool _imageDialogOpen;
    private bool _pendingImageUpload;
    private string? _clipboardPasteError;
    private string? _pendingImageUrl;
    private string? _pendingImageAltText;

    /// <summary>JavaScript runtime for clipboard paste interop.</summary>
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>Document to render.</summary>
    [Parameter] public DocumentEditorDocument? Document { get; set; }

    /// <summary>Whether the surface should be exposed as read-only.</summary>
    [Parameter] public bool ReadOnly { get; set; } = true;

    /// <summary>Page display mode.</summary>
    [Parameter] public DocumentSurfacePageMode PageMode { get; set; } = DocumentSurfacePageMode.Page;

    /// <summary>CSS width for the rendered surface.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>CSS max-width for the rendered surface.</summary>
    [Parameter] public string? MaxWidth { get; set; }

    /// <summary>Optional resolver for provider-managed document image assets.</summary>
    [Parameter] public IDocumentImageUrlResolver? ImageUrlResolver { get; set; }

    /// <summary>Optional provider used for image uploads.</summary>
    [Parameter] public IDocumentImageProvider? ImageProvider { get; set; }

    /// <summary>Optional token provider used by the editable text token menu.</summary>
    [Parameter] public ITokenDataProvider? TokenProvider { get; set; }

    /// <summary>Image validation options used by upload and paste flows.</summary>
    [Parameter] public DocumentImageValidationOptions ImageValidation { get; set; } = new();

    /// <summary>Whether clipboard images can be kept as local draft assets when no image provider is available.</summary>
    [Parameter] public bool AllowOfflineClipboardImages { get; set; }

    /// <summary>Whether tracked changes are enabled.</summary>
    [Parameter] public bool TrackChangesEnabled { get; set; }

    /// <summary>Whether comment actions should be available on the surface.</summary>
    [Parameter] public bool CanComment { get; set; }

    /// <summary>Optional undo/redo command stack used by the owning editor.</summary>
    [Parameter] public DocumentEditorCommandStack? CommandStack { get; set; }

    /// <summary>Current transient selection state.</summary>
    [Parameter] public DocumentEditorSelectionState Selection { get; set; } = new();

    /// <summary>Raised when the selection state changes.</summary>
    [Parameter] public EventCallback<DocumentEditorSelectionState> SelectionChanged { get; set; }

    /// <summary>Raised when the document is changed locally.</summary>
    [Parameter] public EventCallback<DocumentEditorDocument> DocumentChanged { get; set; }

    /// <summary>Raised when the user requests a comment for a block or text range.</summary>
    [Parameter] public EventCallback<DocumentCommentAnchor> CommentRequested { get; set; }

    /// <summary>Raised when a rendered inline comment anchor is selected.</summary>
    [Parameter] public EventCallback<string> CommentSelected { get; set; }

    /// <summary>Additional CSS class for the surface root.</summary>
    [Parameter] public string? Class { get; set; }

    private IEnumerable<DocumentBlock> OrderedBlocks => Document?.Blocks.OrderBy(block => block.Order)
        ?? Enumerable.Empty<DocumentBlock>();

    private DocumentSectionProperties CurrentSectionProperties =>
        Document?.Sections.OrderBy(section => section.Order).FirstOrDefault()?.Properties
        ?? new DocumentSectionProperties();

    private string RootClass
    {
        get
        {
            var classes = new List<string>
            {
                "tm-document-surface",
                PageMode == DocumentSurfacePageMode.Continuous
                    ? "tm-document-surface--continuous"
                    : "tm-document-surface--page"
            };

            if (ReadOnly)
            {
                classes.Add("tm-document-surface--readonly");
            }
            else
            {
                classes.Add("tm-document-surface--editable");
            }

            if (!string.IsNullOrWhiteSpace(Class))
            {
                classes.Add(Class!);
            }

            return string.Join(" ", classes);
        }
    }

    private string? SurfaceStyle
    {
        get
        {
            var styles = new List<string>();
            if (!string.IsNullOrWhiteSpace(Width))
            {
                styles.Add($"width: {Width}");
            }

            if (!string.IsNullOrWhiteSpace(MaxWidth))
            {
                styles.Add($"max-width: {MaxWidth}");
            }

            return styles.Count == 0 ? null : string.Join("; ", styles);
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !ReadOnly)
        {
            await AttachPasteHookAsync();
        }
    }

    private async Task AttachPasteHookAsync()
    {
        if (_pasteHookAttached)
        {
            return;
        }

        _dotNetReference = DotNetObjectReference.Create(this);
        try
        {
            await JS.InvokeVoidAsync(
                "tmDocumentEditor.attachPaste",
                _rootElement,
                _dotNetReference,
                ImageValidation.MaxFileSizeBytes,
                ImageValidation.AllowedContentTypes.ToArray());
            _pasteHookAttached = true;
        }
        catch
        {
            _dotNetReference.Dispose();
            _dotNetReference = null;
        }
    }

    /// <summary>Opens or closes the insert panel from a parent toolbar.</summary>
    public async Task ToggleInsertPanelAsync()
    {
        _insertPanelOpen = !_insertPanelOpen;
        _imageDialogOpen = false;
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Opens the image insert dialog from a parent toolbar.</summary>
    public async Task OpenImageDialogAsync()
    {
        _insertPanelOpen = false;
        _imageDialogOpen = true;
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Closes any floating document editor panels.</summary>
    public async Task ClosePanelsAsync()
    {
        _insertPanelOpen = false;
        _imageDialogOpen = false;
        _clipboardPasteError = null;
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Captures the current textarea text selection as a document comment anchor when possible.</summary>
    public async Task<DocumentCommentAnchor?> CaptureTextSelectionAnchorAsync()
    {
        try
        {
            return await JS.InvokeAsync<DocumentCommentAnchor?>(
                "tmDocumentEditor.getTextSelectionAnchor",
                _rootElement);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Toggles an inline mark across the active text block.</summary>
    public Task ToggleInlineMarkAsync(InlineMarkType markType)
    {
        return UpdateActiveInlineContentAsync($"Toggle {markType}", content =>
        {
            var inlines = GetInlineList(content);
            if (inlines is null)
            {
                return false;
            }

            EnsureEditableTextRun(inlines);
            var hasMarkOnEveryRun = inlines.Count > 0
                && inlines.All(inline => inline.Marks.Any(mark => mark.Type == markType));
            foreach (var inline in inlines)
            {
                if (hasMarkOnEveryRun)
                {
                    inline.Marks.RemoveAll(mark => mark.Type == markType);
                }
                else if (!inline.Marks.Any(mark => mark.Type == markType))
                {
                    inline.Marks.Add(new InlineMark { Type = markType });
                }
            }

            return true;
        });
    }

    /// <summary>Applies a hyperlink mark across the active text block.</summary>
    public Task ApplyLinkAsync(string href)
    {
        if (!TmDocumentInlineRenderer.IsSafeLinkUrl(href))
        {
            return Task.CompletedTask;
        }

        return UpdateActiveInlineContentAsync("Apply link", content =>
        {
            var inlines = GetInlineList(content);
            if (inlines is null)
            {
                return false;
            }

            EnsureEditableTextRun(inlines);
            foreach (var inline in inlines)
            {
                inline.Marks.RemoveAll(mark => mark.Type == InlineMarkType.Link);
                inline.Marks.Add(new InlineMark
                {
                    Type = InlineMarkType.Link,
                    Link = new LinkMarkData { Href = href }
                });
            }

            return true;
        });
    }

    /// <summary>Removes inline formatting from the active text block.</summary>
    public Task ClearInlineFormattingAsync()
    {
        return UpdateActiveInlineContentAsync("Clear formatting", content =>
        {
            var inlines = GetInlineList(content);
            if (inlines is null)
            {
                return false;
            }

            foreach (var inline in inlines)
            {
                inline.Marks.Clear();
            }

            return true;
        });
    }

    /// <summary>Handles a pasted clipboard image from JavaScript interop.</summary>
    [JSInvokable]
    public async Task OnClipboardImagePasted(string contentType, string fileName, long sizeBytes, string base64)
    {
        if (Document is null)
        {
            return;
        }

        if (!ImageValidation.IsAllowed(contentType, sizeBytes))
        {
            _clipboardPasteError = Loc["TmDocumentEditor_ImagePasteRejected"];
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (ImageProvider is null)
        {
            if (AllowOfflineClipboardImages)
            {
                await InsertOfflineClipboardImageAsync(contentType, fileName, sizeBytes, base64);
                return;
            }

            _clipboardPasteError = Loc["TmDocumentEditor_ImageProviderMissing"];
            await InvokeAsync(StateHasChanged);
            return;
        }

        _pendingImageUpload = true;
        _clipboardPasteError = null;
        var bytes = Convert.FromBase64String(base64);
        await using var stream = new MemoryStream(bytes);
        try
        {
            var result = await ImageProvider.UploadAsync(new DocumentImageUploadRequest
            {
                DocumentId = Document.DocumentId,
                FileName = string.IsNullOrWhiteSpace(fileName) ? "clipboard-image" : fileName,
                ContentType = contentType,
                SizeBytes = sizeBytes
            }, stream);

            if (!result.Success || string.IsNullOrWhiteSpace(result.AssetId))
            {
                _clipboardPasteError = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? Loc["TmDocumentEditor_ImageUploadFailed"]
                    : result.ErrorMessage;
                return;
            }

            await InsertBlockAfterActiveAsync(new DocumentBlock
            {
                Type = DocumentBlockType.Image,
                Content = new ImageBlockContent
                {
                    Source = DocumentImageSource.Asset,
                    AssetId = result.AssetId,
                    Url = result.Url,
                    AltText = fileName
                }
            });
        }
        finally
        {
            _pendingImageUpload = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task InsertOfflineClipboardImageAsync(string contentType, string fileName, long sizeBytes, string base64)
    {
        if (Document is null)
        {
            return;
        }

        var assetId = Guid.NewGuid().ToString("N");
        var safeFileName = string.IsNullOrWhiteSpace(fileName) ? "clipboard-image" : fileName;
        var dataUrl = $"data:{contentType};base64,{base64}";
        Document.Assets.Add(new DocumentImageAsset
        {
            Id = assetId,
            DocumentId = Document.DocumentId,
            Source = DocumentImageSource.Clipboard,
            Url = dataUrl,
            ContentType = contentType,
            FileName = safeFileName,
            SizeBytes = sizeBytes,
            AltText = safeFileName,
            IsLocalDraft = true
        });

        await InsertBlockAfterActiveAsync(new DocumentBlock
        {
            Type = DocumentBlockType.Image,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Clipboard,
                AssetId = assetId,
                Url = dataUrl,
                AltText = safeFileName
            }
        });
    }

    private async Task ActivateBlockAsync(string blockId)
    {
        if (ReadOnly)
        {
            return;
        }

        Selection.ActiveBlockId = blockId;
        Selection.FocusedInlineRange = new DocumentEditorInlineRange { BlockId = blockId };
        await SelectionChanged.InvokeAsync(Selection);
    }

    private async Task SetActiveTableCellAsync(string? cellId)
    {
        Selection.ActiveTableCellId = cellId;
        await SelectionChanged.InvokeAsync(Selection);
    }

    private Task HandleCommentRequestedAsync(DocumentCommentAnchor anchor)
    {
        return CommentRequested.InvokeAsync(anchor);
    }

    private async Task ClearSelectionAsync()
    {
        if (ReadOnly)
        {
            return;
        }

        Selection.Clear();
        await SelectionChanged.InvokeAsync(Selection);
    }

    private async Task HandleBlockChangedAsync(DocumentEditorBlockChangedEventArgs args)
    {
        if (CommandStack is not null && Document is not null)
        {
            var after = DocumentEditorCommandCloner.CloneContent(args.Block.Content);
            var before = DocumentEditorCommandCloner.CloneContent(args.Block.Content);
            if (args.OriginalText is not null)
            {
                SetContentText(before, args.OriginalText);
            }

            args.Block.Content = before;
            await CommandStack.PushAsync(new UpdateDocumentBlockCommand(Document, args.Block.Id, before, after));
        }

        if (TrackChangesEnabled)
        {
            var type = args.IsFormattingChange
                ? DocumentRevisionType.Formatting
                : (args.NewText?.Length ?? 0) < (args.OriginalText?.Length ?? 0)
                    ? DocumentRevisionType.Deletion
                    : DocumentRevisionType.Insertion;
            AddRevision(type, args.Block.Id);
        }

        await NotifyDocumentChangedAsync();
    }

    private async Task HandleCommandAsync(DocumentEditorBlockCommandRequest request)
    {
        if (Document is null)
        {
            return;
        }

        var block = Document.Blocks.FirstOrDefault(item => item.Id == request.BlockId);
        if (block is null)
        {
            return;
        }

        switch (request.Command)
        {
            case DocumentEditorBlockCommand.InsertParagraphAfter:
                await InsertBlockAfterAsync(block, CreateParagraphBlock(string.Empty));
                break;
            case DocumentEditorBlockCommand.InsertListAfter:
                if (block.Content is ListBlockContent sourceList)
                {
                    await InsertBlockAfterAsync(block, new DocumentBlock
                    {
                        Type = DocumentBlockType.List,
                        Content = new ListBlockContent
                        {
                            Ordered = sourceList.Ordered,
                            IndentLevel = sourceList.IndentLevel,
                            Inlines = [new TextRun { Text = string.Empty }]
                        }
                    });
                }
                break;
            case DocumentEditorBlockCommand.MergeWithPreviousIfEmpty:
                await MergeWithPreviousIfEmptyAsync(block);
                break;
            case DocumentEditorBlockCommand.IncreaseIndent:
                if (block.Content is ListBlockContent increase)
                {
                    await UpdateBlockContentAsync(block, "Increase indent", content =>
                    {
                        ((ListBlockContent)content).IndentLevel = Math.Min(8, increase.IndentLevel + 1);
                    });
                }
                break;
            case DocumentEditorBlockCommand.DecreaseIndent:
                if (block.Content is ListBlockContent decrease)
                {
                    await UpdateBlockContentAsync(block, "Decrease indent", content =>
                    {
                        ((ListBlockContent)content).IndentLevel = Math.Max(0, decrease.IndentLevel - 1);
                    });
                }
                break;
            case DocumentEditorBlockCommand.AddTableRow:
                await UpdateBlockContentAsync(block, "Add table row", content => AddTableRow((TableBlockContent)content));
                break;
            case DocumentEditorBlockCommand.AddTableColumn:
                await UpdateBlockContentAsync(block, "Add table column", content => AddTableColumn((TableBlockContent)content));
                break;
            case DocumentEditorBlockCommand.DeleteTableRow:
                await UpdateBlockContentAsync(block, "Delete table row", content => DeleteTableRow((TableBlockContent)content, request.CellId));
                break;
            case DocumentEditorBlockCommand.DeleteTableColumn:
                await UpdateBlockContentAsync(block, "Delete table column", content => DeleteTableColumn((TableBlockContent)content, request.CellId));
                break;
            case DocumentEditorBlockCommand.MergeCellRight:
                await UpdateBlockContentAsync(block, "Merge table cells", content => MergeCellRight((TableBlockContent)content, request.CellId));
                break;
            case DocumentEditorBlockCommand.SplitCell:
                await UpdateBlockContentAsync(block, "Split table cell", content => SplitCell((TableBlockContent)content, request.CellId));
                break;
            case DocumentEditorBlockCommand.DeleteBlock:
                await DeleteBlockAsync(block);
                break;
            case DocumentEditorBlockCommand.InsertFootnote:
                await InsertNoteAsync(DocumentNoteType.Footnote);
                return;
            case DocumentEditorBlockCommand.InsertEndnote:
                await InsertNoteAsync(DocumentNoteType.Endnote);
                return;
            case DocumentEditorBlockCommand.ToggleFloatingImage:
                await ExecuteDocumentMutationAsync("Toggle floating image", () => ToggleFloatingImage(block));
                break;
        }

        await NotifyDocumentChangedAsync();
    }

    private async Task InsertParagraphAsync()
    {
        await InsertBlockAfterActiveAsync(CreateParagraphBlock(string.Empty));
        _insertPanelOpen = false;
    }

    private async Task InsertHeadingAsync()
    {
        await InsertBlockAfterActiveAsync(new DocumentBlock
        {
            Type = DocumentBlockType.Heading,
            Content = new HeadingBlockContent { Level = 1, Inlines = [new TextRun { Text = string.Empty }] }
        });
        _insertPanelOpen = false;
    }

    private async Task InsertTableAsync()
    {
        await InsertBlockAfterActiveAsync(new DocumentBlock
        {
            Type = DocumentBlockType.Table,
            Content = new TableBlockContent
            {
                Rows =
                [
                    new TableRowContent { Cells = [CreateCell(), CreateCell()] },
                    new TableRowContent { Cells = [CreateCell(), CreateCell()] }
                ]
            }
        });
        _insertPanelOpen = false;
    }

    private async Task InsertImageUrlAsync()
    {
        await InsertBlockAfterActiveAsync(new DocumentBlock
        {
            Type = DocumentBlockType.Image,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Url,
                Url = _pendingImageUrl,
                AltText = _pendingImageAltText
            }
        });
        _imageDialogOpen = false;
    }

    private async Task InsertProviderImageAsync()
    {
        if (Document is null || ImageProvider is null)
        {
            return;
        }

        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
        await using var stream = new MemoryStream(bytes);
        var result = await ImageProvider.UploadAsync(new DocumentImageUploadRequest
        {
            DocumentId = Document.DocumentId,
            FileName = "demo-image.png",
            ContentType = "image/png",
            SizeBytes = bytes.Length
        }, stream);

        if (!result.Success || string.IsNullOrWhiteSpace(result.AssetId))
        {
            return;
        }

        await InsertBlockAfterActiveAsync(new DocumentBlock
        {
            Type = DocumentBlockType.Image,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Asset,
                AssetId = result.AssetId,
                Url = result.Url,
                AltText = _pendingImageAltText
            }
        });
        _imageDialogOpen = false;
    }

    private async Task InsertNoteAsync(DocumentNoteType noteType)
    {
        if (Document is null)
        {
            return;
        }

        var note = new DocumentNote
        {
            Type = noteType,
            Marker = (Document.Notes.Count(item => item.Type == noteType) + 1).ToString()
        };
        note.Blocks.Add(CreateParagraphBlock(string.Empty));
        Document.Notes.Add(note);

        var block = Document.Blocks.FirstOrDefault(item => item.Id == Selection.ActiveBlockId)
            ?? Document.Blocks.OrderBy(item => item.Order).LastOrDefault();
        if (block?.Content is ParagraphBlockContent paragraph)
        {
            paragraph.Inlines.Add(new DocumentNoteReferenceRun
            {
                NoteId = note.Id,
                NoteType = note.Type,
                DisplayMarker = note.Marker
            });
            note.ReferenceIds.Add(note.Id);
        }

        await NotifyDocumentChangedAsync();
    }

    private async Task ChangeNoteTextAsync(DocumentNote note, string text)
    {
        note.Blocks.Clear();
        note.Blocks.Add(CreateParagraphBlock(text));
        await NotifyDocumentChangedAsync();
    }

    private async Task ChangeHeaderFooterTextAsync(DocumentHeaderFooterType type, string text)
    {
        if (Document is null)
        {
            return;
        }

        var item = EnsureHeaderFooter(type);
        item.Blocks.Clear();
        item.Blocks.Add(CreateParagraphBlock(text));
        await NotifyDocumentChangedAsync();
    }

    private async Task ChangeDifferentFirstPageAsync(bool value)
    {
        if (Document?.Sections.OrderBy(section => section.Order).FirstOrDefault()?.Properties is { } properties)
        {
            properties.DifferentFirstPage = value;
            await NotifyDocumentChangedAsync();
        }
    }

    private async Task ChangeDifferentOddEvenAsync(bool value)
    {
        if (Document?.Sections.OrderBy(section => section.Order).FirstOrDefault()?.Properties is { } properties)
        {
            properties.DifferentOddAndEvenPages = value;
            await NotifyDocumentChangedAsync();
        }
    }

    private async Task ReviewRevisionAsync(DocumentRevision revision, DocumentRevisionAction action)
    {
        revision.Action = action;
        await NotifyDocumentChangedAsync();
    }

    private Task InsertBlockAfterActiveAsync(DocumentBlock block)
    {
        if (Document is null)
        {
            return Task.CompletedTask;
        }

        var active = Document.Blocks.FirstOrDefault(item => item.Id == Selection.ActiveBlockId)
            ?? Document.Blocks.OrderBy(item => item.Order).LastOrDefault();
        return InsertBlockAfterAsync(active, block);
    }

    private async Task InsertBlockAfterAsync(DocumentBlock? after, DocumentBlock block)
    {
        if (Document is null)
        {
            return;
        }

        if (CommandStack is null)
        {
            InsertBlockAfterDirect(after, block);
        }
        else
        {
            await CommandStack.PushAsync(new InsertDocumentBlockCommand(Document, block, after?.Id));
        }

        Selection.ActiveBlockId = block.Id;
        await NotifyDocumentChangedAsync();
    }

    private void InsertBlockAfterDirect(DocumentBlock? after, DocumentBlock block)
    {
        if (Document is null)
        {
            return;
        }

        var ordered = Document.Blocks.OrderBy(item => item.Order).ToList();
        var index = after is null ? ordered.Count - 1 : ordered.FindIndex(item => item.Id == after.Id);
        var nextOrder = index >= 0 && index + 1 < ordered.Count ? ordered[index + 1].Order : (after?.Order ?? 0) + 20;
        block.Order = after is null ? 10 : ((after.Order + nextOrder) / 2);
        Document.Blocks.Add(block);
    }

    private async Task MergeWithPreviousIfEmptyAsync(DocumentBlock block)
    {
        if (Document is null || !string.IsNullOrWhiteSpace(GetBlockText(block)))
        {
            return;
        }

        var ordered = Document.Blocks.OrderBy(item => item.Order).ToList();
        var index = ordered.FindIndex(item => item.Id == block.Id);
        if (index > 0)
        {
            Selection.ActiveBlockId = ordered[index - 1].Id;
            await DeleteBlockAsync(block);
        }
    }

    private async Task DeleteBlockAsync(DocumentBlock block)
    {
        if (Document is null)
        {
            return;
        }

        if (CommandStack is null)
        {
            Document.Blocks.Remove(block);
        }
        else
        {
            await CommandStack.PushAsync(new DeleteDocumentBlockCommand(Document, block.Id));
        }

        await NotifyDocumentChangedAsync();
    }

    private async Task UpdateBlockContentAsync(DocumentBlock block, string description, Action<DocumentBlockContent> update)
    {
        if (Document is null)
        {
            return;
        }

        if (CommandStack is null)
        {
            update(block.Content);
            return;
        }

        var before = DocumentEditorCommandCloner.CloneContent(block.Content);
        var after = DocumentEditorCommandCloner.CloneContent(block.Content);
        update(after);
        await CommandStack.PushAsync(new UpdateDocumentBlockCommand(Document, block.Id, before, after, description));
    }

    private async Task ExecuteDocumentMutationAsync(string description, Action update)
    {
        if (Document is null)
        {
            return;
        }

        if (CommandStack is null)
        {
            update();
            return;
        }

        var before = DocumentEditorCommandCloner.Clone(Document);
        update();
        var after = DocumentEditorCommandCloner.Clone(Document);
        await CommandStack.PushAsync(new DocumentEditorSnapshotCommand(Document, before, after, description));
    }

    private static void AddTableRow(TableBlockContent table)
    {
        var width = table.Rows.Select(row => row.Cells.Count).DefaultIfEmpty(1).Max();
        table.Rows.Add(new TableRowContent { Cells = Enumerable.Range(0, width).Select(_ => CreateCell()).ToList() });
    }

    private static void AddTableColumn(TableBlockContent table)
    {
        if (table.Rows.Count == 0)
        {
            table.Rows.Add(new TableRowContent());
        }

        foreach (var row in table.Rows)
        {
            row.Cells.Add(CreateCell());
        }
    }

    private static void DeleteTableRow(TableBlockContent table, string? cellId)
    {
        if (table.Rows.Count <= 1)
        {
            return;
        }

        var row = table.Rows.FirstOrDefault(row => row.Cells.Any(cell => cell.Id == cellId)) ?? table.Rows.Last();
        table.Rows.Remove(row);
    }

    private static void DeleteTableColumn(TableBlockContent table, string? cellId)
    {
        var columnIndex = FindCell(table, cellId).ColumnIndex;
        foreach (var row in table.Rows)
        {
            if (row.Cells.Count > 1 && columnIndex >= 0 && columnIndex < row.Cells.Count)
            {
                row.Cells.RemoveAt(columnIndex);
            }
        }
    }

    private static void MergeCellRight(TableBlockContent table, string? cellId)
    {
        var (row, rowIndex, columnIndex, cell) = FindCell(table, cellId);
        if (row is null || cell is null || columnIndex < 0 || columnIndex + 1 >= row.Cells.Count)
        {
            return;
        }

        var right = row.Cells[columnIndex + 1];
        if (!right.Merge.IsOrigin)
        {
            return;
        }

        cell.ColumnSpan += right.ColumnSpan;
        right.Merge.IsOrigin = false;
        right.Merge.OriginCellId = cell.Id;
    }

    private static void SplitCell(TableBlockContent table, string? cellId)
    {
        var (row, _, columnIndex, cell) = FindCell(table, cellId);
        if (row is null || cell is null || cell.ColumnSpan <= 1)
        {
            return;
        }

        var span = cell.ColumnSpan;
        cell.ColumnSpan = 1;
        for (var i = 1; i < span; i++)
        {
            row.Cells.Insert(columnIndex + i, CreateCell());
        }
    }

    private static (TableRowContent? Row, int RowIndex, int ColumnIndex, TableCellContent? Cell) FindCell(TableBlockContent table, string? cellId)
    {
        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            var columnIndex = string.IsNullOrWhiteSpace(cellId)
                ? 0
                : row.Cells.FindIndex(cell => cell.Id == cellId);
            if (columnIndex >= 0 && columnIndex < row.Cells.Count)
            {
                return (row, rowIndex, columnIndex, row.Cells[columnIndex]);
            }
        }

        return (null, -1, -1, null);
    }

    private void ToggleFloatingImage(DocumentBlock block)
    {
        if (block.Content is not ImageBlockContent image)
        {
            return;
        }

        image.FloatingLayout ??= new DocumentFloatingLayout();
        image.FloatingLayout.Inline = !image.FloatingLayout.Inline;
        image.FloatingLayout.WrapMode = image.FloatingLayout.Inline
            ? DocumentWrapMode.Inline
            : DocumentWrapMode.Square;
        image.FloatingLayout.X = image.FloatingLayout.Inline ? 0 : 24;
        image.FloatingLayout.Y = image.FloatingLayout.Inline ? 0 : 24;

        var anchor = Document?.Anchors.FirstOrDefault(anchor => anchor.BlockId == block.Id && anchor.Type == DocumentAnchorType.FloatingObject);
        if (Document is not null && anchor is null)
        {
            Document.Anchors.Add(new DocumentAnchor
            {
                Type = DocumentAnchorType.FloatingObject,
                BlockId = block.Id,
                FloatingLayout = image.FloatingLayout
            });
        }
    }

    private DocumentHeaderFooter EnsureHeaderFooter(DocumentHeaderFooterType type)
    {
        var item = Document!.HeadersFooters.FirstOrDefault(item =>
            item.Type == type && item.Scope == DocumentHeaderFooterScope.Primary);
        if (item is not null)
        {
            return item;
        }

        item = new DocumentHeaderFooter
        {
            Type = type,
            Scope = DocumentHeaderFooterScope.Primary,
            SectionId = Document.Sections.OrderBy(section => section.Order).FirstOrDefault()?.Id
        };
        Document.HeadersFooters.Add(item);
        return item;
    }

    private string GetHeaderFooterText(DocumentHeaderFooterType type)
    {
        if (Document is null)
        {
            return string.Empty;
        }

        var item = Document.HeadersFooters.FirstOrDefault(item =>
            item.Type == type && item.Scope == DocumentHeaderFooterScope.Primary);
        return item is null ? string.Empty : string.Join(Environment.NewLine, item.Blocks.Select(GetBlockText));
    }

    private static string GetNoteText(DocumentNote note)
    {
        return string.Join(Environment.NewLine, note.Blocks.Select(GetBlockText));
    }

    private static DocumentBlock CreateParagraphBlock(string text)
    {
        return new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = text }] }
        };
    }

    private static TableCellContent CreateCell()
    {
        return new TableCellContent
        {
            Blocks = [CreateParagraphBlock(string.Empty)]
        };
    }

    private static string GetBlockText(DocumentBlock block)
    {
        return block.Content switch
        {
            ParagraphBlockContent paragraph => GetInlineText(paragraph.Inlines),
            HeadingBlockContent heading => GetInlineText(heading.Inlines),
            ListBlockContent list => GetInlineText(list.Inlines),
            QuoteBlockContent quote => GetInlineText(quote.Inlines),
            _ => string.Empty
        };
    }

    private static string GetInlineText(List<InlineContent> inlines)
    {
        return string.Concat(inlines.Select(inline => inline switch
        {
            TextRun text => text.Text,
            TokenRun token => string.IsNullOrWhiteSpace(token.DisplayName) ? token.Key : token.DisplayName,
            DocumentNoteReferenceRun note => string.IsNullOrWhiteSpace(note.DisplayMarker) ? note.NoteId : note.DisplayMarker,
            _ => string.Empty
        }));
    }

    private async Task UpdateActiveInlineContentAsync(string description, Func<DocumentBlockContent, bool> update)
    {
        if (Document is null)
        {
            return;
        }

        var block = Document.Blocks.FirstOrDefault(item => item.Id == Selection.ActiveBlockId)
            ?? Document.Blocks.OrderBy(item => item.Order).FirstOrDefault(item => GetInlineList(item.Content) is not null);
        if (block is null || GetInlineList(block.Content) is null)
        {
            return;
        }

        var before = DocumentEditorCommandCloner.CloneContent(block.Content);
        var after = DocumentEditorCommandCloner.CloneContent(block.Content);
        if (!update(after))
        {
            return;
        }

        if (CommandStack is null)
        {
            block.Content = after;
        }
        else
        {
            await CommandStack.PushAsync(new UpdateDocumentBlockCommand(Document, block.Id, before, after, description));
        }

        if (TrackChangesEnabled)
        {
            AddRevision(DocumentRevisionType.Formatting, block.Id);
        }

        await NotifyDocumentChangedAsync();
    }

    private static List<InlineContent>? GetInlineList(DocumentBlockContent content)
    {
        return content switch
        {
            ParagraphBlockContent paragraph => paragraph.Inlines,
            HeadingBlockContent heading => heading.Inlines,
            ListBlockContent list => list.Inlines,
            QuoteBlockContent quote => quote.Inlines,
            _ => null
        };
    }

    private static void EnsureEditableTextRun(List<InlineContent> inlines)
    {
        if (inlines.Count == 0)
        {
            inlines.Add(new TextRun());
        }
    }

    private static void SetContentText(DocumentBlockContent content, string text)
    {
        var inlines = GetInlineList(content);
        if (inlines is null)
        {
            return;
        }

        inlines.Clear();
        inlines.Add(new TextRun { Text = text });
    }

    private void AddRevision(DocumentRevisionType type, string? blockId)
    {
        if (Document is null)
        {
            return;
        }

        Document.Revisions.Add(new DocumentRevision
        {
            Type = type,
            Range = new DocumentRevisionRange { BlockId = blockId },
            Author = new DocumentRevisionAuthor { Id = "local", DisplayName = "Local editor" }
        });
    }

    private Task NotifyDocumentChangedAsync()
    {
        return Document is null ? Task.CompletedTask : DocumentChanged.InvokeAsync(Document);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_pasteHookAttached)
        {
            try
            {
                await JS.InvokeVoidAsync("tmDocumentEditor.detachPaste", _rootElement);
            }
            catch
            {
                // Best-effort JS cleanup.
            }
        }

        _dotNetReference?.Dispose();
    }
}
