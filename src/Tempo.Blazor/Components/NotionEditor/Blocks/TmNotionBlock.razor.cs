using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks;

/// <summary>
/// Dispatcher for a single page block — renders the correct markup per BlockType,
/// manages contenteditable lifecycle via JS interop, and surfaces block events to the parent.
/// </summary>
public partial class TmNotionBlock : ComponentBase
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Cascaded context ─────────────────────────────────────────────────────

    [CascadingParameter]
    private NotionEditorContext Context { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter, EditorRequired]
    public IPageBlock Block { get; set; } = default!;

    [Parameter] public bool ReadOnly { get; set; }

    [Parameter] public int BlockIndex { get; set; }

    [Parameter] public bool IsFocused { get; set; }

    [Parameter] public bool IsSelected { get; set; }

    /// <summary>1-based ordinal for NumberedList blocks, pre-computed by TmNotionBlockList.</summary>
    [Parameter] public int NumberedListNumber { get; set; }

    [Parameter] public EventCallback OnFocused { get; set; }

    [Parameter] public EventCallback OnDeleted { get; set; }

    [Parameter] public EventCallback<IPageBlock> OnUpdated { get; set; }

    [Parameter] public EventCallback<BlockType> OnConvertTo { get; set; }

    [Parameter] public EventCallback<(BlockType Type, string? InitialHtml)> OnAddAfter { get; set; }

    [Parameter] public EventCallback<IPageBlock> OnDuplicate { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private IPageBlock?       _lastBlock;

    private ElementReference  _blockRef;
    private ElementReference  _equationRef;

    // ── Computed ─────────────────────────────────────────────────────────────

    private string _blockMods => string.Concat(
        IsFocused  ? " tm-notion-block--focused"  : string.Empty,
        IsSelected ? " tm-notion-block--selected" : string.Empty
    ).TrimStart();

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        _lastBlock = Block;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Block.Type == BlockType.Equation)
        {
            var expr = (Block.Content as IEquationBlockContent)?.Expression;
            if (!string.IsNullOrWhiteSpace(expr))
            {
                try { await JS.InvokeVoidAsync("tmNotionEditor.renderEquation", _equationRef, expr); }
                catch { }
            }
        }
    }

    private async Task OnFocusedAsync() => await OnFocused.InvokeAsync();

    // ── Todo (TmNotionTodoBlock) callbacks ───────────────────────────────────

    private async Task HandleTodoCheckedChangedAsync(bool isChecked)
    {
        if (Block.Content is not ITodoBlockContent todo) return;
        var updated = BuildBlockWithContent(Block, new TodoBlockContent
        {
            IsChecked       = isChecked,
            Html            = todo.Html,
            BackgroundColor = todo.BackgroundColor,
            TextColor       = todo.TextColor,
            Alignment       = todo.Alignment
        });
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
        }
        catch { }
    }

    private async Task HandleTodoContentSavedAsync(string html)
    {
        var updated = BuildBlockWithHtml(Block, html);
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
        }
        catch { }
    }

    private async Task HandleTodoEnterSplitAsync(string afterHtml) =>
        await OnAddAfter.InvokeAsync((BlockType.TodoItem, afterHtml));

    private async Task HandleTodoMarkdownShortcutAsync(string shortcut)
    {
        var newType = shortcut switch
        {
            "paragraph" => BlockType.Paragraph,
            "heading1"  => BlockType.Heading1,
            "heading2"  => BlockType.Heading2,
            "heading3"  => BlockType.Heading3,
            "bullet"    => BlockType.BulletList,
            "numbered"  => BlockType.NumberedList,
            "quote"     => BlockType.Quote,
            "code"      => BlockType.Code,
            "divider"   => BlockType.Divider,
            _           => Block.Type
        };
        if (newType != Block.Type)
            await OnConvertTo.InvokeAsync(newType);
    }

    private Task HandleTodoSlashAsync((double Top, double Left) _) => Task.CompletedTask;
    private Task HandleTodoMentionAsync((double Top, double Left) _) => Task.CompletedTask;
    private Task HandleTodoPageLinkAsync((double Top, double Left) _) => Task.CompletedTask;

    // ── Toggle (TmNotionToggleBlock) callbacks ────────────────────────────────

    private async Task HandleToggleOpenChangedAsync(bool isOpen)
    {
        if (Block.Content is not IToggleBlockContent toggle) return;
        var updated = BuildBlockWithContent(Block, new ToggleBlockContent
        {
            IsOpen          = isOpen,
            Html            = toggle.Html,
            BackgroundColor = toggle.BackgroundColor,
            TextColor       = toggle.TextColor,
            Alignment       = toggle.Alignment
        });
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
        }
        catch { }
    }

    private async Task HandleToggleContentSavedAsync(string html)
    {
        var updated = BuildBlockWithHtml(Block, html);
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
        }
        catch { }
    }

    private async Task HandleToggleEnterSplitAsync(string afterHtml) =>
        await OnAddAfter.InvokeAsync((BlockType.Paragraph, afterHtml));

    private async Task HandleToggleMarkdownShortcutAsync(string shortcut)
    {
        var newType = shortcut switch
        {
            "paragraph" => BlockType.Paragraph,
            "heading1"  => BlockType.Heading1,
            "heading2"  => BlockType.Heading2,
            "heading3"  => BlockType.Heading3,
            "bullet"    => BlockType.BulletList,
            "numbered"  => BlockType.NumberedList,
            "todo"      => BlockType.TodoItem,
            "quote"     => BlockType.Quote,
            "code"      => BlockType.Code,
            "divider"   => BlockType.Divider,
            _           => Block.Type
        };
        if (newType != Block.Type)
            await OnConvertTo.InvokeAsync(newType);
    }

    private Task HandleToggleSlashAsync((double Top, double Left) _) => Task.CompletedTask;
    private Task HandleToggleMentionAsync((double Top, double Left) _) => Task.CompletedTask;
    private Task HandleTogglePageLinkAsync((double Top, double Left) _) => Task.CompletedTask;

    // ── Code (TmNotionCodeBlock) callbacks ───────────────────────────────────

    private async Task HandleCodeSavedAsync(string code)
    {
        if (Block.Content is not ICodeBlockContent cc) return;
        var updated = BuildBlockWithContent(Block, new CodeBlockContent
        {
            Code            = code,
            Language        = cc.Language,
            Caption         = cc.Caption,
            ShowLineNumbers = cc.ShowLineNumbers,
            WrapLines       = cc.WrapLines
        });
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
        }
        catch { }
    }

    private async Task HandleCodeLanguageChangedAsync(string? language)
    {
        if (Block.Content is not ICodeBlockContent cc) return;
        var updated = BuildBlockWithContent(Block, new CodeBlockContent
        {
            Code            = cc.Code,
            Language        = language,
            Caption         = cc.Caption,
            ShowLineNumbers = cc.ShowLineNumbers,
            WrapLines       = cc.WrapLines
        });
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
        }
        catch { }
    }

    private async Task HandleCodeCaptionSavedAsync(string? caption)
    {
        if (Block.Content is not ICodeBlockContent cc) return;
        var updated = BuildBlockWithContent(Block, new CodeBlockContent
        {
            Code            = cc.Code,
            Language        = cc.Language,
            Caption         = caption,
            ShowLineNumbers = cc.ShowLineNumbers,
            WrapLines       = cc.WrapLines
        });
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
        }
        catch { }
    }

    // ── Navigation helpers ────────────────────────────────────────────────────

    private void OnNavigateToPage(Guid? pageId)
    {
        if (pageId.HasValue)
            _ = Context.DataProvider.GetPageAsync(pageId.Value.ToString());
    }

    private void OnPageLinkKeyDown(KeyboardEventArgs e, Guid? pageId)
    {
        if (e.Key == "Enter") OnNavigateToPage(pageId);
    }

    // ── Handle button ─────────────────────────────────────────────────────────

    private Task HandleAddClickedAsync() =>
        OnAddAfter.InvokeAsync((BlockType.Paragraph, null));

    private Task HandleDividerAddAfterAsync() =>
        OnAddAfter.InvokeAsync((BlockType.Paragraph, null));

    // ── Paragraph (TmNotionTextBlock) callbacks ───────────────────────────────

    private async Task HandleParagraphContentSavedAsync(string html)
    {
        var updated = BuildBlockWithHtml(Block, html);
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
        }
        catch { }
    }

    private async Task HandleParagraphEnterPressedAsync(string afterHtml) =>
        await OnAddAfter.InvokeAsync((BlockType.Paragraph, afterHtml));

    private Task HandleParagraphTabAsync(bool _) => Task.CompletedTask;

    private async Task HandleParagraphMarkdownShortcutAsync(string shortcut)
    {
        var newType = shortcut switch
        {
            "heading1"  => BlockType.Heading1,
            "heading2"  => BlockType.Heading2,
            "heading3"  => BlockType.Heading3,
            "bullet"    => BlockType.BulletList,
            "numbered"  => BlockType.NumberedList,
            "todo"      => BlockType.TodoItem,
            "todoDone"  => BlockType.TodoItem,
            "quote"     => BlockType.Quote,
            "code"      => BlockType.Code,
            "divider"   => BlockType.Divider,
            _           => Block.Type
        };
        if (newType != Block.Type)
            await OnConvertTo.InvokeAsync(newType);
    }

    private Task HandleParagraphSlashAsync((double Top, double Left) _) => Task.CompletedTask;
    private Task HandleParagraphMentionAsync((double Top, double Left) _) => Task.CompletedTask;
    private Task HandleParagraphPageLinkAsync((double Top, double Left) _) => Task.CompletedTask;

    // ── Heading (TmNotionHeadingBlock) callbacks ──────────────────────────────

    private async Task HandleHeadingContentSavedAsync(string html)
    {
        var updated = BuildBlockWithHtml(Block, html);
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
        }
        catch { }
    }

    private async Task HandleHeadingEnterPressedAsync(string afterHtml) =>
        await OnAddAfter.InvokeAsync((BlockType.Paragraph, afterHtml));

    private Task HandleHeadingTabAsync(bool _) => Task.CompletedTask;

    private async Task HandleHeadingMarkdownShortcutAsync(string shortcut)
    {
        var newType = shortcut switch
        {
            "heading1"  => BlockType.Heading1,
            "heading2"  => BlockType.Heading2,
            "heading3"  => BlockType.Heading3,
            "bullet"    => BlockType.BulletList,
            "numbered"  => BlockType.NumberedList,
            "todo"      => BlockType.TodoItem,
            "quote"     => BlockType.Quote,
            "code"      => BlockType.Code,
            "divider"   => BlockType.Divider,
            _           => Block.Type
        };
        if (newType != Block.Type)
            await OnConvertTo.InvokeAsync(newType);
    }

    private Task HandleHeadingSlashAsync((double Top, double Left) _) => Task.CompletedTask;
    private Task HandleHeadingMentionAsync((double Top, double Left) _) => Task.CompletedTask;
    private Task HandleHeadingPageLinkAsync((double Top, double Left) _) => Task.CompletedTask;
    private Task HandleHeadingToggleAsync(bool _) => Task.CompletedTask;

    // ── Quote (TmNotionQuoteBlock) callbacks ──────────────────────────────────

    private async Task HandleQuoteContentSavedAsync(string html)
    {
        var updated = BuildBlockWithHtml(Block, html);
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
        }
        catch { }
    }

    private async Task HandleQuoteEnterPressedAsync(string afterHtml) =>
        await OnAddAfter.InvokeAsync((BlockType.Quote, afterHtml));

    private Task HandleQuoteTabAsync(bool _) => Task.CompletedTask;

    private async Task HandleQuoteMarkdownShortcutAsync(string shortcut)
    {
        var newType = shortcut switch
        {
            "heading1"  => BlockType.Heading1,
            "heading2"  => BlockType.Heading2,
            "heading3"  => BlockType.Heading3,
            "bullet"    => BlockType.BulletList,
            "numbered"  => BlockType.NumberedList,
            "todo"      => BlockType.TodoItem,
            "code"      => BlockType.Code,
            "divider"   => BlockType.Divider,
            _           => Block.Type
        };
        if (newType != Block.Type)
            await OnConvertTo.InvokeAsync(newType);
    }

    private Task HandleQuoteSlashAsync((double Top, double Left) _) => Task.CompletedTask;
    private Task HandleQuoteMentionAsync((double Top, double Left) _) => Task.CompletedTask;
    private Task HandleQuotePageLinkAsync((double Top, double Left) _) => Task.CompletedTask;

    // ── Callout (TmNotionCalloutBlock) callbacks ──────────────────────────────

    private async Task HandleCalloutContentSavedAsync(string html)
    {
        var updated = BuildBlockWithHtml(Block, html);
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
        }
        catch { }
    }

    private async Task HandleCalloutEnterPressedAsync(string afterHtml) =>
        await OnAddAfter.InvokeAsync((BlockType.Callout, afterHtml));

    private Task HandleCalloutTabAsync(bool _) => Task.CompletedTask;

    private async Task HandleCalloutMarkdownShortcutAsync(string shortcut)
    {
        var newType = shortcut switch
        {
            "heading1" => BlockType.Heading1,
            "heading2" => BlockType.Heading2,
            "heading3" => BlockType.Heading3,
            "bullet"   => BlockType.BulletList,
            "numbered" => BlockType.NumberedList,
            "todo"     => BlockType.TodoItem,
            "quote"    => BlockType.Quote,
            "code"     => BlockType.Code,
            "divider"  => BlockType.Divider,
            _          => Block.Type
        };
        if (newType != Block.Type)
            await OnConvertTo.InvokeAsync(newType);
    }

    private Task HandleCalloutSlashAsync((double Top, double Left) _) => Task.CompletedTask;
    private Task HandleCalloutMentionAsync((double Top, double Left) _) => Task.CompletedTask;
    private Task HandleCalloutPageLinkAsync((double Top, double Left) _) => Task.CompletedTask;

    private async Task HandleCalloutEmojiChangedAsync(string? emoji)
    {
        if (Block.Content is not ICalloutBlockContent cc) return;
        var updated = BuildBlockWithContent(Block, new CalloutBlockContent
        {
            Html            = cc.Html,
            IconEmoji       = emoji,
            IconImageUrl    = cc.IconImageUrl,
            BackgroundColor = cc.BackgroundColor,
            TextColor       = cc.TextColor,
            Alignment       = cc.Alignment
        });
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
        }
        catch { }
    }

    // ── BulletList (TmNotionBulletListBlock) callbacks ───────────────────────

    private async Task HandleBulletContentSavedAsync(string html)
    {
        var updated = BuildBlockWithHtml(Block, html);
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
        }
        catch { }
    }

    private async Task HandleBulletEnterSplitAsync(string afterHtml) =>
        await OnAddAfter.InvokeAsync((BlockType.BulletList, afterHtml));

    private async Task HandleBulletTabAsync(bool shiftKey)
    {
        if (Block.Content is not IListBlockContent list) return;
        var newIndent = Math.Clamp(list.IndentLevel + (shiftKey ? -1 : 1), 0, 3);
        if (newIndent == list.IndentLevel) return;
        var updated = BuildBlockWithContent(Block, new ListBlockContent
        {
            IndentLevel     = newIndent,
            Html            = list.Html,
            BackgroundColor = list.BackgroundColor,
            TextColor       = list.TextColor,
            Alignment       = list.Alignment
        });
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
        }
        catch { }
    }

    private async Task HandleBulletMarkdownShortcutAsync(string shortcut)
    {
        var newType = shortcut switch
        {
            "paragraph" => BlockType.Paragraph,
            "heading1"  => BlockType.Heading1,
            "heading2"  => BlockType.Heading2,
            "heading3"  => BlockType.Heading3,
            "numbered"  => BlockType.NumberedList,
            "todo"      => BlockType.TodoItem,
            "quote"     => BlockType.Quote,
            "code"      => BlockType.Code,
            "divider"   => BlockType.Divider,
            _           => Block.Type
        };
        if (newType != Block.Type)
            await OnConvertTo.InvokeAsync(newType);
    }

    private Task HandleBulletSlashAsync((double Top, double Left) _) => Task.CompletedTask;
    private Task HandleBulletMentionAsync((double Top, double Left) _) => Task.CompletedTask;
    private Task HandleBulletPageLinkAsync((double Top, double Left) _) => Task.CompletedTask;

    // ── NumberedList (TmNotionNumberedListBlock) callbacks ───────────────────

    private async Task HandleNumberedContentSavedAsync(string html)
    {
        var updated = BuildBlockWithHtml(Block, html);
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
        }
        catch { }
    }

    private async Task HandleNumberedEnterSplitAsync(string afterHtml) =>
        await OnAddAfter.InvokeAsync((BlockType.NumberedList, afterHtml));

    private async Task HandleNumberedTabAsync(bool shiftKey)
    {
        if (Block.Content is not IListBlockContent list) return;
        var newIndent = Math.Clamp(list.IndentLevel + (shiftKey ? -1 : 1), 0, 3);
        if (newIndent == list.IndentLevel) return;
        var updated = BuildBlockWithContent(Block, new ListBlockContent
        {
            IndentLevel     = newIndent,
            Html            = list.Html,
            BackgroundColor = list.BackgroundColor,
            TextColor       = list.TextColor,
            Alignment       = list.Alignment
        });
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
        }
        catch { }
    }

    private async Task HandleNumberedMarkdownShortcutAsync(string shortcut)
    {
        var newType = shortcut switch
        {
            "paragraph" => BlockType.Paragraph,
            "heading1"  => BlockType.Heading1,
            "heading2"  => BlockType.Heading2,
            "heading3"  => BlockType.Heading3,
            "bullet"    => BlockType.BulletList,
            "todo"      => BlockType.TodoItem,
            "quote"     => BlockType.Quote,
            "code"      => BlockType.Code,
            "divider"   => BlockType.Divider,
            _           => Block.Type
        };
        if (newType != Block.Type)
            await OnConvertTo.InvokeAsync(newType);
    }

    private Task HandleNumberedSlashAsync((double Top, double Left) _) => Task.CompletedTask;
    private Task HandleNumberedMentionAsync((double Top, double Left) _) => Task.CompletedTask;
    private Task HandleNumberedPageLinkAsync((double Top, double Left) _) => Task.CompletedTask;

    // ── Handle context menu actions ───────────────────────────────────────────

    private async Task HandleDuplicateAsync() => await OnDuplicate.InvokeAsync(Block);

    private Task HandleMoveToAsync() => Task.CompletedTask;

    private async Task HandleCopyLinkAsync()
    {
        try
        {
            var fragment = $"#{Block.Id}";
            await JS.InvokeVoidAsync("tmNotionEditor.copyBlockLink", fragment);
        }
        catch { }
    }

    private Task HandleCommentAsync() => Task.CompletedTask;

    private async Task HandleTextColorChangeAsync(string? color)
    {
        if (Block.Content is not ITextBlockContent tc) return;
        var updated = BuildBlockWithContent(Block, UpdateTextBlockColor(tc, textColor: color));
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
        }
        catch { }
    }

    private async Task HandleBackgroundColorChangeAsync(string? color)
    {
        if (Block.Content is not ITextBlockContent tc) return;
        var updated = BuildBlockWithContent(Block, UpdateTextBlockColor(tc, backgroundColor: color));
        try
        {
            await Context.BlockProvider.UpdateBlockAsync(updated);
            await OnUpdated.InvokeAsync(updated);
        }
        catch { }
    }

    private static IBlockContent UpdateTextBlockColor(ITextBlockContent src,
        string? textColor = null, string? backgroundColor = null) => src switch
    {
        IHeadingBlockContent hc  => new HeadingBlockContent
        {
            Html = hc.Html, Level = hc.Level, IsToggleable = hc.IsToggleable,
            TextColor = textColor ?? hc.TextColor,
            BackgroundColor = backgroundColor ?? hc.BackgroundColor,
            Alignment = hc.Alignment
        },
        ICalloutBlockContent cc  => new CalloutBlockContent
        {
            Html = cc.Html, IconEmoji = cc.IconEmoji, IconImageUrl = cc.IconImageUrl,
            TextColor = textColor ?? cc.TextColor,
            BackgroundColor = backgroundColor ?? cc.BackgroundColor,
            Alignment = cc.Alignment
        },
        IListBlockContent lc     => new ListBlockContent
        {
            Html = lc.Html, IndentLevel = lc.IndentLevel,
            TextColor = textColor ?? lc.TextColor,
            BackgroundColor = backgroundColor ?? lc.BackgroundColor,
            Alignment = lc.Alignment
        },
        ITodoBlockContent tc2    => new TodoBlockContent
        {
            Html = tc2.Html, IsChecked = tc2.IsChecked,
            TextColor = textColor ?? tc2.TextColor,
            BackgroundColor = backgroundColor ?? tc2.BackgroundColor,
            Alignment = tc2.Alignment
        },
        IToggleBlockContent tg   => new ToggleBlockContent
        {
            Html = tg.Html, IsOpen = tg.IsOpen,
            TextColor = textColor ?? tg.TextColor,
            BackgroundColor = backgroundColor ?? tg.BackgroundColor,
            Alignment = tg.Alignment
        },
        _                        => new TextBlockContent
        {
            Html = src.Html,
            TextColor = textColor ?? src.TextColor,
            BackgroundColor = backgroundColor ?? src.BackgroundColor,
            Alignment = src.Alignment
        }
    };

    // ── CSS helpers ───────────────────────────────────────────────────────────

    private static string AlignClass(ITextBlockContent? c) => c?.Alignment switch
    {
        TextAlignment.Center => "tm-notion-align-center",
        TextAlignment.Right  => "tm-notion-align-right",
        _                    => string.Empty
    };

    private static string BgClass(ITextBlockContent? c) =>
        string.IsNullOrEmpty(c?.BackgroundColor) ? string.Empty : $"tm-notion-bg-{c.BackgroundColor}";

    // ── Block content helpers ─────────────────────────────────────────────────


    private static PageBlock BuildBlockWithHtml(IPageBlock src, string html)
    {
        var content = src.Content switch
        {
            IHeadingBlockContent hc => (IBlockContent)new HeadingBlockContent
            {
                Html = html, Level = hc.Level, IsToggleable = hc.IsToggleable,
                BackgroundColor = hc.BackgroundColor, TextColor = hc.TextColor, Alignment = hc.Alignment
            },
            ICalloutBlockContent cc => new CalloutBlockContent
            {
                Html = html, IconEmoji = cc.IconEmoji, IconImageUrl = cc.IconImageUrl,
                BackgroundColor = cc.BackgroundColor, TextColor = cc.TextColor, Alignment = cc.Alignment
            },
            IListBlockContent lc => new ListBlockContent
            {
                Html = html, IndentLevel = lc.IndentLevel,
                BackgroundColor = lc.BackgroundColor, TextColor = lc.TextColor, Alignment = lc.Alignment
            },
            ITodoBlockContent tc => new TodoBlockContent
            {
                Html = html, IsChecked = tc.IsChecked,
                BackgroundColor = tc.BackgroundColor, TextColor = tc.TextColor, Alignment = tc.Alignment
            },
            IToggleBlockContent tg => new ToggleBlockContent
            {
                Html = html, IsOpen = tg.IsOpen,
                BackgroundColor = tg.BackgroundColor, TextColor = tg.TextColor, Alignment = tg.Alignment
            },
            ITextBlockContent tc => new TextBlockContent
            {
                Html = html,
                BackgroundColor = tc.BackgroundColor, TextColor = tc.TextColor, Alignment = tc.Alignment
            },
            _ => src.Content
        };
        return BuildBlockWithContent(src, content);
    }

    private static PageBlock BuildBlockWithContent(IPageBlock src, IBlockContent content) => new()
    {
        Id            = src.Id,
        PageId        = src.PageId,
        ParentBlockId = src.ParentBlockId,
        Type          = src.Type,
        Order         = src.Order,
        Content       = content,
        CreatedAt     = src.CreatedAt,
        LastEditedAt  = DateTime.UtcNow
    };

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }

}

