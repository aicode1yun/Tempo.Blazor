using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Activity;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Components.DocumentEditor;

/// <summary>Editable renderer for a single document block.</summary>
public partial class TmDocumentEditableBlock : ComponentBase
{
    /// <summary>Document id used by provider-backed image resolution.</summary>
    [Parameter] public string DocumentId { get; set; } = string.Empty;

    /// <summary>Block to edit.</summary>
    [Parameter] public DocumentBlock? Block { get; set; }

    /// <summary>Whether the block is active.</summary>
    [Parameter] public bool IsActive { get; set; }

    /// <summary>Whether comment actions should be available for this block.</summary>
    [Parameter] public bool CanComment { get; set; }

    /// <summary>Selected table cell id for table editing.</summary>
    [Parameter] public string? SelectedCellId { get; set; }

    /// <summary>Optional resolver for provider-managed image assets.</summary>
    [Parameter] public IDocumentImageUrlResolver? ImageUrlResolver { get; set; }

    /// <summary>Optional token provider used by the {{ token autocomplete menu.</summary>
    [Parameter] public ITokenDataProvider? TokenProvider { get; set; }

    /// <summary>Raised when the block is activated.</summary>
    [Parameter] public EventCallback<string> OnActivate { get; set; }

    /// <summary>Raised when the selected table cell changes.</summary>
    [Parameter] public EventCallback<string?> SelectedCellIdChanged { get; set; }

    /// <summary>Raised when the block content changes.</summary>
    [Parameter] public EventCallback<DocumentEditorBlockChangedEventArgs> OnBlockChanged { get; set; }

    /// <summary>Raised when the block emits a structural command.</summary>
    [Parameter] public EventCallback<DocumentEditorBlockCommandRequest> OnCommand { get; set; }

    /// <summary>Raised when a comment is requested for this block.</summary>
    [Parameter] public EventCallback<DocumentCommentAnchor> OnCommentRequested { get; set; }

    private List<InlineContent>? _tokenTargetInlines;
    private List<TokenItem> _availableTokens = [];
    private string _tokenQuery = string.Empty;
    private int _tokenHighlightedIndex;
    private bool _tokenMenuOpen;
    private bool _tokenLoading;

    private string RootClass
    {
        get
        {
            var classes = new List<string> { "tm-document-editable-block" };
            if (IsActive)
            {
                classes.Add("tm-document-editable-block--active");
            }

            if (Block is not null)
            {
                classes.Add($"tm-document-editable-block--{Block.Type.ToString().ToLowerInvariant()}");
            }

            return string.Join(" ", classes);
        }
    }

    private async Task ActivateAsync()
    {
        if (Block is not null)
        {
            await OnActivate.InvokeAsync(Block.Id);
        }
    }

    private async Task SelectCellAsync(string cellId)
    {
        await ActivateAsync();
        await SelectedCellIdChanged.InvokeAsync(cellId);
    }

    private async Task RequestCommentAsync()
    {
        if (Block is null)
        {
            return;
        }

        await OnCommentRequested.InvokeAsync(new DocumentCommentAnchor
        {
            Type = DocumentCommentAnchorType.Block,
            BlockId = Block.Id
        });
    }

    private async Task ChangeInlineTextAsync(List<InlineContent> inlines, ChangeEventArgs args)
    {
        var original = GetInlineText(inlines);
        var text = args.Value?.ToString() ?? string.Empty;
        SetInlineText(inlines, text);
        await RefreshTokenMenuAsync(inlines, text);
        await NotifyChangedAsync(original, GetInlineText(inlines));
    }

    private async Task ChangeHeadingLevelAsync(HeadingBlockContent heading, ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), CultureInfo.InvariantCulture, out var level))
        {
            heading.Level = Math.Clamp(level, 1, 6);
            await NotifyChangedAsync(GetInlineText(heading.Inlines), GetInlineText(heading.Inlines), isFormattingChange: true);
        }
    }

    private async Task HandleTextKeyDownAsync(KeyboardEventArgs args)
    {
        if (await HandleTokenMenuKeyDownAsync(args))
        {
            return;
        }

        if (args.Key == "Enter")
        {
            await CommandAsync(DocumentEditorBlockCommand.InsertParagraphAfter);
        }
        else if (args.Key == "Backspace" && string.IsNullOrWhiteSpace(GetBlockText(Block)))
        {
            await CommandAsync(DocumentEditorBlockCommand.MergeWithPreviousIfEmpty);
        }
    }

    private async Task HandleListKeyDownAsync(KeyboardEventArgs args)
    {
        if (await HandleTokenMenuKeyDownAsync(args))
        {
            return;
        }

        if (args.Key == "Tab" && args.ShiftKey)
        {
            await CommandAsync(DocumentEditorBlockCommand.DecreaseIndent);
        }
        else if (args.Key == "Tab")
        {
            await CommandAsync(DocumentEditorBlockCommand.IncreaseIndent);
        }
        else if (args.Key == "Enter")
        {
            await CommandAsync(string.IsNullOrWhiteSpace(GetBlockText(Block))
                ? DocumentEditorBlockCommand.InsertParagraphAfter
                : DocumentEditorBlockCommand.InsertListAfter);
        }
    }

    private async Task CommandAsync(DocumentEditorBlockCommand command)
    {
        if (Block is null)
        {
            return;
        }

        await OnCommand.InvokeAsync(new DocumentEditorBlockCommandRequest
        {
            BlockId = Block.Id,
            Command = command,
            CellId = SelectedCellId
        });
    }

    private async Task ChangeCellTextAsync(TableCellContent cell, ChangeEventArgs args)
    {
        var original = GetCellText(cell);
        cell.Blocks.Clear();
        cell.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Order = 10,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = args.Value?.ToString() ?? string.Empty }]
            }
        });

        await SelectedCellIdChanged.InvokeAsync(cell.Id);
        await NotifyChangedAsync(original, GetCellText(cell));
    }

    private async Task RefreshTokenMenuAsync(List<InlineContent> inlines, string text)
    {
        if (TokenProvider is null)
        {
            CloseTokenMenu();
            return;
        }

        var triggerIndex = text.LastIndexOf("{{", StringComparison.Ordinal);
        if (triggerIndex < 0)
        {
            CloseTokenMenu();
            return;
        }

        var query = text[(triggerIndex + 2)..];
        if (query.Contains('}') || query.Contains(Environment.NewLine, StringComparison.Ordinal) || query.Any(char.IsWhiteSpace))
        {
            CloseTokenMenu();
            return;
        }

        _tokenTargetInlines = inlines;
        _tokenQuery = query;
        _tokenMenuOpen = true;
        _tokenLoading = true;
        _tokenHighlightedIndex = 0;

        try
        {
            var tokens = await TokenProvider.SearchTokensAsync(query);
            _availableTokens = tokens.Select(ToTokenItem).ToList();
        }
        catch
        {
            _availableTokens = [];
        }
        finally
        {
            _tokenLoading = false;
        }
    }

    private async Task<bool> HandleTokenMenuKeyDownAsync(KeyboardEventArgs args)
    {
        if (!_tokenMenuOpen)
        {
            return false;
        }

        switch (args.Key)
        {
            case "Escape":
                CloseTokenMenu();
                return true;
            case "ArrowDown":
                _tokenHighlightedIndex = Math.Min(_availableTokens.Count - 1, _tokenHighlightedIndex + 1);
                return true;
            case "ArrowUp":
                _tokenHighlightedIndex = Math.Max(0, _tokenHighlightedIndex - 1);
                return true;
            case "Enter":
                if (_availableTokens.Count > 0)
                {
                    await InsertTokenAsync(_availableTokens[Math.Clamp(_tokenHighlightedIndex, 0, _availableTokens.Count - 1)]);
                }

                return true;
            default:
                return false;
        }
    }

    private Task SetTokenHighlightAsync(int index)
    {
        _tokenHighlightedIndex = index;
        return Task.CompletedTask;
    }

    private async Task InsertTokenAsync(TokenItem token)
    {
        if (_tokenTargetInlines is null)
        {
            return;
        }

        var inlines = GetCurrentEditableInlines() ?? _tokenTargetInlines;
        if (inlines is null)
        {
            return;
        }

        var original = GetInlineText(inlines);
        var text = original;
        var triggerIndex = text.LastIndexOf("{{", StringComparison.Ordinal);
        if (triggerIndex < 0)
        {
            return;
        }

        var queryStart = triggerIndex + 2;
        var queryEnd = Math.Min(text.Length, queryStart + _tokenQuery.Length);
        var before = text[..triggerIndex];
        var after = text[queryEnd..];

        inlines.Clear();
        if (!string.IsNullOrEmpty(before))
        {
            inlines.Add(new TextRun { Text = before });
        }

        inlines.Add(new TokenRun
        {
            Key = token.Key,
            DisplayName = token.DisplayName,
            Description = token.Description,
            ColorClass = token.ColorClass,
            TypeLabel = token.TypeLabel,
            TokenType = NormalizeTokenType(token.TypeLabel)
        });

        if (!string.IsNullOrEmpty(after))
        {
            inlines.Add(new TextRun { Text = after });
        }

        CloseTokenMenu();
        await NotifyChangedAsync(original, GetInlineText(inlines));
    }

    private async Task DeleteTokenAsync(List<InlineContent> inlines, int index)
    {
        if (index < 0 || index >= inlines.Count || inlines[index] is not TokenRun)
        {
            return;
        }

        var original = GetInlineText(inlines);
        inlines.RemoveAt(index);
        MergeAdjacentTextRuns(inlines);
        await NotifyChangedAsync(original, GetInlineText(inlines));
    }

    private void CloseTokenMenu()
    {
        _tokenMenuOpen = false;
        _tokenLoading = false;
        _tokenQuery = string.Empty;
        _availableTokens = [];
        _tokenTargetInlines = null;
        _tokenHighlightedIndex = 0;
    }

    private async Task HandleTableCellKeyDownAsync(TableBlockContent table, int rowIndex, int columnIndex, KeyboardEventArgs args)
    {
        if (args.Key != "Tab" || table.Rows.Count == 0)
        {
            return;
        }

        var nextRow = rowIndex;
        var nextColumn = args.ShiftKey ? columnIndex - 1 : columnIndex + 1;
        if (nextColumn >= table.Rows[rowIndex].Cells.Count)
        {
            nextColumn = 0;
            nextRow = Math.Min(table.Rows.Count - 1, rowIndex + 1);
        }
        else if (nextColumn < 0)
        {
            nextRow = Math.Max(0, rowIndex - 1);
            nextColumn = table.Rows[nextRow].Cells.Count - 1;
        }

        var next = table.Rows[nextRow].Cells[Math.Clamp(nextColumn, 0, table.Rows[nextRow].Cells.Count - 1)];
        await SelectedCellIdChanged.InvokeAsync(next.Id);
    }

    private async Task ChangeImageUrlAsync(ImageBlockContent image, ChangeEventArgs args)
    {
        image.Url = args.Value?.ToString();
        await NotifyChangedAsync(null, image.Url);
    }

    private async Task ChangeImageAltAsync(ImageBlockContent image, ChangeEventArgs args)
    {
        image.AltText = args.Value?.ToString();
        await NotifyChangedAsync(null, image.AltText);
    }

    private async Task ChangeImageCaptionAsync(ImageBlockContent image, ChangeEventArgs args)
    {
        image.Caption = args.Value?.ToString();
        await NotifyChangedAsync(null, image.Caption);
    }

    private async Task ChangeImageWidthAsync(ImageBlockContent image, ChangeEventArgs args)
    {
        if (double.TryParse(args.Value?.ToString(), CultureInfo.InvariantCulture, out var width))
        {
            image.Size.Width = Math.Max(1, width);
            await NotifyChangedAsync(null, width.ToString(CultureInfo.InvariantCulture), isFormattingChange: true);
        }
    }

    private async Task SetImageAlignmentAsync(ImageBlockContent image, DocumentImageAlignment alignment)
    {
        image.Alignment = alignment;
        await NotifyChangedAsync(null, alignment.ToString(), isFormattingChange: true);
    }

    private async Task NotifyChangedAsync(string? original, string? current, bool isFormattingChange = false)
    {
        if (Block is null)
        {
            return;
        }

        await OnBlockChanged.InvokeAsync(new DocumentEditorBlockChangedEventArgs
        {
            Block = Block,
            OriginalText = original,
            NewText = current,
            IsFormattingChange = isFormattingChange
        });
    }

    private static string GetCellText(TableCellContent cell)
    {
        return string.Join(
            Environment.NewLine,
            cell.Blocks.OrderBy(block => block.Order).Select(GetBlockText));
    }

    private static string GetBlockText(DocumentBlock? block)
    {
        return block?.Content switch
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

    private static void SetInlineText(List<InlineContent> inlines, string text)
    {
        inlines.Clear();
        inlines.Add(new TextRun { Text = text });
    }

    private static bool HasTokens(List<InlineContent> inlines)
    {
        return inlines.Any(inline => inline is TokenRun);
    }

    private List<InlineContent>? GetCurrentEditableInlines()
    {
        return Block?.Content switch
        {
            ParagraphBlockContent paragraph => paragraph.Inlines,
            HeadingBlockContent heading => heading.Inlines,
            ListBlockContent list => list.Inlines,
            QuoteBlockContent quote => quote.Inlines,
            _ => null
        };
    }

    private static IEnumerable<(int Index, TokenRun Run)> GetTokensWithIndexes(List<InlineContent> inlines)
    {
        return inlines
            .Select((inline, index) => (Inline: inline, Index: index))
            .Where(item => item.Inline is TokenRun)
            .Select(item => (item.Index, (TokenRun)item.Inline));
    }

    private static string GetTokenLabel(TokenRun token)
    {
        return string.IsNullOrWhiteSpace(token.DisplayName) ? token.Key : token.DisplayName;
    }

    private static TokenItem ToTokenItem(IToken token)
    {
        return new TokenItem
        {
            Key = token.Key,
            DisplayName = token.DisplayName,
            Description = token.Description,
            Category = token.Category,
            Icon = token.Icon,
            ColorClass = token.ColorClass,
            TypeLabel = token.TypeLabel
        };
    }

    private static string? NormalizeTokenType(string? typeLabel)
    {
        return string.IsNullOrWhiteSpace(typeLabel)
            ? null
            : typeLabel.Trim().ToLowerInvariant().Replace(' ', '-');
    }

    private static void MergeAdjacentTextRuns(List<InlineContent> inlines)
    {
        for (var i = inlines.Count - 2; i >= 0; i--)
        {
            if (inlines[i] is TextRun left && inlines[i + 1] is TextRun right)
            {
                left.Text += right.Text;
                inlines.RemoveAt(i + 1);
            }
        }
    }
}
