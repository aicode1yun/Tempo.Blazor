using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Abstractions.Interfaces;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Files;

/// <summary>
/// A full-featured file manager component with folder tree navigation,
/// list/grid views, toolbar actions, and breadcrumb path display.
/// Supports keyboard navigation like Windows Explorer (arrow keys, Enter,
/// Delete, F2, Ctrl+A, Backspace, Ctrl+Click, Shift+Click).
/// </summary>
public partial class TmFileManager
{
    private string _currentPath = "/";
    private List<FileManagerItem> _items = [];
    private List<FileManagerItem>? _folderTree;
    private readonly List<FileManagerItem> _selectedItems = [];
    private FileManagerViewMode _viewMode = FileManagerViewMode.List;
    private FileManagerItem? _renamingItem;
    private string _renameValue = string.Empty;
    private ElementReference _renameInputRef;
    private bool _shouldFocusRenameInput;
    private bool _showDeleteDialog;
    private readonly List<FileManagerItem> _itemsToDelete = [];

    // ── Keyboard navigation state ────────────────────────────────
    private int _focusedIndex = -1;   // index of the keyboard-focused item
    private int _anchorIndex = -1;    // anchor for Shift+selection
    private int _gridColumnCount = 1; // columns in grid view (detected from JS)
    private ElementReference _contentRef;

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────

    /// <summary>Data provider for file system operations.</summary>
    [Parameter] public IFileManagerDataProvider? DataProvider { get; set; }

    /// <summary>Current folder path. Two-way bindable.</summary>
    [Parameter] public string? CurrentPath { get; set; }

    /// <summary>Event fired when the current path changes.</summary>
    [Parameter] public EventCallback<string> CurrentPathChanged { get; set; }

    /// <summary>Display mode (List or Grid). Default is <see cref="FileManagerViewMode.List"/>.</summary>
    [Parameter] public FileManagerViewMode ViewMode { get; set; } = FileManagerViewMode.List;

    /// <summary>Event fired when the view mode changes.</summary>
    [Parameter] public EventCallback<FileManagerViewMode> ViewModeChanged { get; set; }

    /// <summary>When true, disables all user interactions.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Whether to show the folder tree sidebar. Default is <c>true</c>.</summary>
    [Parameter] public bool ShowFolderTree { get; set; } = true;

    /// <summary>Whether to show the New Folder button. Default is <c>true</c>.</summary>
    [Parameter] public bool ShowNewFolderButton { get; set; } = true;

    /// <summary>Whether to show the Upload button. Default is <c>true</c>.</summary>
    [Parameter] public bool ShowUploadButton { get; set; } = true;

    /// <summary>Whether to show the Delete button. Default is <c>true</c>.</summary>
    [Parameter] public bool ShowDeleteButton { get; set; } = true;

    /// <summary>Whether to show the Rename button. Default is <c>true</c>.</summary>
    [Parameter] public bool ShowRenameButton { get; set; } = true;

    /// <summary>Event fired when an item is opened (double-clicked).</summary>
    [Parameter] public EventCallback<FileManagerItem> OnItemOpen { get; set; }

    /// <summary>Event fired when the selection changes.</summary>
    [Parameter] public EventCallback<IReadOnlyList<FileManagerItem>> OnSelectionChanged { get; set; }

    /// <summary>Additional CSS class for the wrapper element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional attributes spread onto the wrapper element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    // ── Lifecycle ────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!string.IsNullOrEmpty(CurrentPath) && CurrentPath != _currentPath)
        {
            _currentPath = CurrentPath;
        }
        _viewMode = ViewMode;
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    // ── Data loading ─────────────────────────────────────────────

    private async Task LoadDataAsync()
    {
        if (DataProvider is null) return;

        try
        {
            _items = (await DataProvider.GetFolderContentsAsync(_currentPath)).ToList();
            _folderTree = (await DataProvider.GetFolderTreeAsync()).ToList();
        }
        catch
        {
            _items = [];
        }

        // Reset keyboard focus when folder content changes
        _focusedIndex = _items.Count > 0 ? 0 : -1;
        _anchorIndex = _focusedIndex;
    }

    // ── Navigation ───────────────────────────────────────────────

    private async Task NavigateTo(string path)
    {
        if (Disabled) return;
        _currentPath = path;
        _selectedItems.Clear();
        await CurrentPathChanged.InvokeAsync(_currentPath);
        await OnSelectionChanged.InvokeAsync(_selectedItems);
        await LoadDataAsync();
    }

    private async Task NavigateToRoot()
    {
        await NavigateTo("/");
    }

    private async Task OnItemDoubleClickAsync(FileManagerItem item)
    {
        if (Disabled) return;
        if (item.IsDirectory)
        {
            await NavigateTo(item.Path);
        }
        else
        {
            await OnItemOpen.InvokeAsync(item);
        }
    }

    private List<BreadcrumbSegment> _breadcrumbSegments => BuildBreadcrumb(_currentPath);

    private static List<BreadcrumbSegment> BuildBreadcrumb(string path)
    {
        var segments = new List<BreadcrumbSegment>();
        if (string.IsNullOrEmpty(path) || path == "/") return segments;

        var parts = path.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = "";
        foreach (var part in parts)
        {
            current += "/" + part;
            segments.Add(new BreadcrumbSegment(part, current));
        }
        return segments;
    }

    private sealed record BreadcrumbSegment(string Name, string Path);

    // ── Selection ────────────────────────────────────────────────

    private async Task HandleItemClickAsync(FileManagerItem item, MouseEventArgs e)
    {
        if (Disabled) return;

        var index = _items.IndexOf(item);
        if (index < 0) return;

        if (e.CtrlKey)
        {
            // Toggle selection
            if (_selectedItems.Contains(item))
                _selectedItems.Remove(item);
            else
                _selectedItems.Add(item);

            _focusedIndex = index;
            _anchorIndex = index;
        }
        else if (e.ShiftKey && _anchorIndex >= 0 && _anchorIndex < _items.Count)
        {
            // Range selection
            var start = Math.Min(_anchorIndex, index);
            var end = Math.Max(_anchorIndex, index);
            _selectedItems.Clear();
            for (int i = start; i <= end; i++)
                _selectedItems.Add(_items[i]);

            _focusedIndex = index;
        }
        else
        {
            // Normal single selection
            _selectedItems.Clear();
            _selectedItems.Add(item);
            _focusedIndex = index;
            _anchorIndex = index;
        }

        await OnSelectionChanged.InvokeAsync(_selectedItems);
    }

    // ── View mode ────────────────────────────────────────────────

    private async Task SetViewMode(FileManagerViewMode mode)
    {
        if (Disabled) return;
        _viewMode = mode;
        _gridColumnCount = mode == FileManagerViewMode.Grid ? 4 : 1;
        await ViewModeChanged.InvokeAsync(mode);
    }

    // ── Keyboard handling ────────────────────────────────────────

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (Disabled || _renamingItem is not null) return;

        // Ctrl+A — Select All
        if ((e.CtrlKey || e.MetaKey) && e.Key.Equals("a", StringComparison.OrdinalIgnoreCase))
        {
            SelectAll();
            return;
        }

        switch (e.Key)
        {
            case "ArrowUp":
            case "ArrowDown":
            case "ArrowLeft":
            case "ArrowRight":
                await HandleArrowKeyAsync(e);
                break;

            case "Enter":
                if (_focusedIndex >= 0 && _focusedIndex < _items.Count)
                    await OnItemDoubleClickAsync(_items[_focusedIndex]);
                break;

            case "Delete":
                if (_selectedItems.Count > 0)
                    DeleteSelectedAsync();
                break;

            case "F2":
                if (_selectedItems.Count == 1)
                    StartRenameAsync();
                break;

            case "Backspace":
                if (_currentPath != "/")
                {
                    var parent = GetParentPath(_currentPath);
                    await NavigateTo(parent);
                }
                break;
        }
    }

    private async Task HandleArrowKeyAsync(KeyboardEventArgs e)
    {
        if (_items.Count == 0) return;

        if (_focusedIndex < 0 || _focusedIndex >= _items.Count)
            _focusedIndex = 0;

        var columns = _viewMode == FileManagerViewMode.Grid ? Math.Max(1, _gridColumnCount) : 1;
        var newIndex = _focusedIndex;

        switch (e.Key)
        {
            case "ArrowUp":
                newIndex = _viewMode == FileManagerViewMode.Grid
                    ? _focusedIndex - columns
                    : _focusedIndex - 1;
                break;

            case "ArrowDown":
                newIndex = _viewMode == FileManagerViewMode.Grid
                    ? _focusedIndex + columns
                    : _focusedIndex + 1;
                break;

            case "ArrowLeft":
                newIndex = _focusedIndex - 1;
                break;

            case "ArrowRight":
                newIndex = _focusedIndex + 1;
                break;
        }

        // Clamp to valid range
        if (newIndex < 0) newIndex = 0;
        if (newIndex >= _items.Count) newIndex = _items.Count - 1;

        if (newIndex == _focusedIndex) return;

        if (e.ShiftKey && _anchorIndex >= 0 && _anchorIndex < _items.Count)
        {
            // Extend/shrink range selection
            var start = Math.Min(_anchorIndex, newIndex);
            var end = Math.Max(_anchorIndex, newIndex);
            _selectedItems.Clear();
            for (int i = start; i <= end; i++)
                _selectedItems.Add(_items[i]);
        }
        else if (!e.CtrlKey)
        {
            // Normal: move selection to new item
            _selectedItems.Clear();
            _selectedItems.Add(_items[newIndex]);
            _anchorIndex = newIndex;
        }
        // Ctrl+arrow: just move focus without changing selection (not implemented — could be added)

        _focusedIndex = newIndex;
        await OnSelectionChanged.InvokeAsync(_selectedItems);
    }

    private void SelectAll()
    {
        _selectedItems.Clear();
        _selectedItems.AddRange(_items);
        _focusedIndex = _items.Count > 0 ? 0 : -1;
        _anchorIndex = _focusedIndex;
        OnSelectionChanged.InvokeAsync(_selectedItems);
    }

    // ── Toolbar actions ──────────────────────────────────────────

    private async Task CreateFolderAsync()
    {
        if (Disabled || DataProvider is null) return;
        var folderName = "New Folder";
        var newItem = await DataProvider.CreateFolderAsync(_currentPath, folderName);
        await LoadDataAsync();

        // Find the newly created folder in the refreshed list and start inline rename
        var createdItem = _items.FirstOrDefault(i => i.Id == newItem.Id);
        if (createdItem is not null)
        {
            _selectedItems.Clear();
            _selectedItems.Add(createdItem);
            await OnSelectionChanged.InvokeAsync(_selectedItems);

            _renamingItem = createdItem;
            _renameValue = createdItem.Name;
            _shouldFocusRenameInput = true;
        }
    }

    private void DeleteSelectedAsync()
    {
        if (Disabled || _selectedItems.Count == 0) return;
        _itemsToDelete.Clear();
        _itemsToDelete.AddRange(_selectedItems);
        _showDeleteDialog = true;
    }

    private async Task ConfirmDeleteAsync()
    {
        if (DataProvider is null || _itemsToDelete.Count == 0) return;
        var paths = _itemsToDelete.Select(i => i.Path).ToList();
        await DataProvider.DeleteAsync(paths);
        _itemsToDelete.Clear();
        _selectedItems.Clear();
        _showDeleteDialog = false;
        await OnSelectionChanged.InvokeAsync(_selectedItems);
        await LoadDataAsync();
    }

    private void CancelDelete()
    {
        _itemsToDelete.Clear();
        _showDeleteDialog = false;
    }

    private async Task HandleDeleteDialogResult(bool? result)
    {
        if (result == true)
        {
            await ConfirmDeleteAsync();
        }
        else
        {
            CancelDelete();
        }
    }

    private void StartRenameAsync()
    {
        if (Disabled || _selectedItems.Count != 1) return;
        _renamingItem = _selectedItems[0];
        _renameValue = _renamingItem.Name;
        _shouldFocusRenameInput = true;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_shouldFocusRenameInput)
        {
            _shouldFocusRenameInput = false;
            await _renameInputRef.FocusAsync();
        }

        // Detect grid column count from rendered DOM
        if (_viewMode == FileManagerViewMode.Grid && _contentRef.Id is not null)
        {
            try
            {
                var cols = await JSRuntime.InvokeAsync<int>("TempoFileManager.getGridColumnCount", _contentRef);
                if (cols > 0)
                    _gridColumnCount = cols;
            }
            catch
            {
                // JS not available (e.g. bUnit) — keep fallback
            }
        }
    }

    private async Task CommitRenameAsync()
    {
        if (_renamingItem is null || DataProvider is null) return;

        var newName = _renameValue.Trim();
        if (!string.IsNullOrEmpty(newName) && newName != _renamingItem.Name)
        {
            await DataProvider.RenameAsync(_renamingItem.Path, newName);
            await LoadDataAsync();
        }

        _renamingItem = null;
        _renameValue = string.Empty;
    }

    private void CancelRename()
    {
        _renamingItem = null;
        _renameValue = string.Empty;
    }

    private async Task HandleRenameKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await CommitRenameAsync();
        }
        else if (e.Key == "Escape")
        {
            CancelRename();
        }
    }

    private async Task HandleFileSelectedAsync(InputFileChangeEventArgs e)
    {
        if (Disabled || DataProvider is null) return;

        var files = new List<FileUploadInfo>();
        foreach (var file in e.GetMultipleFiles())
        {
            files.Add(new FileUploadInfo
            {
                FileName = file.Name,
                Size = file.Size,
                ContentType = file.ContentType,
                Stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024) // 10 MB limit
            });
        }

        await DataProvider.UploadAsync(_currentPath, files);
        await LoadDataAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────

    private string GetFolderClass(FileManagerItem folder)
    {
        var cls = "tm-file-manager__folder-item";
        if (folder.Path == _currentPath) cls += " tm-file-manager__folder-item--active";
        return cls;
    }

    private string GetItemClass(FileManagerItem item)
    {
        var cls = "";
        if (_selectedItems.Contains(item)) cls += " tm-file-manager__item--selected";
        if (item.IsDirectory) cls += " tm-file-manager__item--folder";
        return cls.Trim();
    }

    private static string GetItemIcon(FileManagerItem item)
    {
        if (!string.IsNullOrEmpty(item.IconName)) return item.IconName;
        if (item.IsDirectory) return "folder";
        return item.Extension.ToLowerInvariant() switch
        {
            ".pdf" => "file-text",
            ".doc" or ".docx" => "file-text",
            ".xls" or ".xlsx" => "table",
            ".ppt" or ".pptx" => "presentation",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".svg" => "image",
            ".mp3" or ".wav" or ".ogg" => "music",
            ".mp4" or ".avi" or ".mov" => "video",
            ".zip" or ".rar" or ".7z" => "archive",
            ".txt" or ".md" or ".json" or ".xml" or ".csv" => "file-text",
            _ => "file"
        };
    }

    private static string FormatSize(long? bytes)
    {
        if (!bytes.HasValue) return "—";
        var b = bytes.Value;
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        return b switch
        {
            >= GB => $"{b / (double)GB:F2} GB",
            >= MB => $"{b / (double)MB:F2} MB",
            >= KB => $"{b / (double)KB:F2} KB",
            _ => $"{b} B"
        };
    }

    private static string GetParentPath(string itemPath)
    {
        itemPath = itemPath.TrimEnd('/');
        var lastSlash = itemPath.LastIndexOf('/');
        if (lastSlash <= 0) return "/";
        return itemPath.Substring(0, lastSlash);
    }
}
