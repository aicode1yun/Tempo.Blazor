using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Abstractions.Interfaces;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Files;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Files;

public class TmDocumentManagerTests : LocalizationTestBase
{
    private sealed record TestMetadata(string Category = "", string Description = "");

    private static MockDocumentManagerDataProvider CreateMockProvider(
        bool withPermissions = false,
        bool hideDocs = false)
    {
        var items = new List<DocumentManagerItem<TestMetadata>>
        {
            new()
            {
                Id = "docs", Name = "Documents", Path = "/Documents", IsDirectory = true,
                Metadata = new TestMetadata("General", "Document folder")
            },
            new()
            {
                Id = "pics", Name = "Pictures", Path = "/Pictures", IsDirectory = true,
                Metadata = new TestMetadata("Media", "Image folder")
            },
            new()
            {
                Id = "rootfile", Name = "Readme.txt", Path = "/Readme.txt",
                IsDirectory = false, Size = 100, Extension = ".txt",
                Metadata = new TestMetadata("General", "Root readme")
            },
            new()
            {
                Id = "file1", Name = "Report.pdf", Path = "/Documents/Report.pdf",
                IsDirectory = false, Size = 1024, Extension = ".pdf",
                Metadata = new TestMetadata("Finance", "Annual report")
            },
            new()
            {
                Id = "file2", Name = "Data.xlsx", Path = "/Documents/Data.xlsx",
                IsDirectory = false, Size = 2048, Extension = ".xlsx",
                Metadata = new TestMetadata("Finance", "Q2 data")
            },
        };

        if (withPermissions)
        {
            items[0].Permissions = new DocumentManagerPermission { CanDelete = false, CanRename = false };
            items[1].Permissions = new DocumentManagerPermission { CanDelete = true, CanRename = true };
            items[3].Permissions = new DocumentManagerPermission { CanDelete = true, CanRename = true };
            items[4].Permissions = new DocumentManagerPermission { CanDelete = false, CanRename = true };
        }

        if (hideDocs)
        {
            items[0].Permissions = new DocumentManagerPermission { CanRead = false };
        }

        return new MockDocumentManagerDataProvider(items);
    }

    private RenderFragment<NewFolderContext<TestMetadata>> NewFolderForm => ctx => builder =>
    {
        builder.OpenComponent<TestNewFolderForm>(0);
        builder.AddAttribute(1, "Context", ctx);
        builder.CloseComponent();
    };

    private RenderFragment<DeleteContext<TestMetadata>> DeleteForm => ctx => builder =>
    {
        builder.OpenComponent<TestDeleteForm>(0);
        builder.AddAttribute(1, "Context", ctx);
        builder.CloseComponent();
    };

    private RenderFragment<EditContext<TestMetadata>> EditForm => ctx => builder =>
    {
        builder.OpenComponent<TestEditForm>(0);
        builder.AddAttribute(1, "Context", ctx);
        builder.CloseComponent();
    };

    private RenderFragment<DetailContext<TestMetadata>> DetailPanel => ctx => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "test-detail-panel");
        builder.AddContent(2, ctx.Item.Name);
        builder.CloseElement();
    };

    private RenderFragment<ContextMenuContext<TestMetadata>> ContextMenu => ctx => builder =>
    {
        builder.OpenComponent<TestContextMenuForm>(0);
        builder.AddAttribute(1, "Context", ctx);
        builder.CloseComponent();
    };

    private RenderFragment<DocumentManagerItem<TestMetadata>> MetaTemplate => item => builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "test-meta-template");
        builder.AddContent(2, item.Metadata?.Category ?? "—");
        builder.CloseElement();
    };

    private sealed class TestNewFolderForm : ComponentBase
    {
        [Parameter] public NewFolderContext<TestMetadata> Context { get; set; } = null!;
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "test-new-folder-form");
            builder.OpenElement(2, "button");
            builder.AddAttribute(3, "class", "test-new-folder-submit");
            builder.AddAttribute(4, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, _ => Context.OnSubmit?.Invoke() ?? Task.CompletedTask));
            builder.AddContent(5, "Create");
            builder.CloseElement();
            builder.CloseElement();
        }
    }

    private sealed class TestDeleteForm : ComponentBase
    {
        [Parameter] public DeleteContext<TestMetadata> Context { get; set; } = null!;
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "test-delete-form");
            builder.OpenElement(2, "button");
            builder.AddAttribute(3, "class", "test-delete-confirm");
            builder.AddAttribute(4, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, _ => Context.OnConfirm?.Invoke() ?? Task.CompletedTask));
            builder.AddContent(5, "Confirm Delete");
            builder.CloseElement();
            builder.CloseElement();
        }
    }

    private sealed class TestEditForm : ComponentBase
    {
        [Parameter] public EditContext<TestMetadata> Context { get; set; } = null!;
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "test-edit-form");
            builder.OpenElement(2, "button");
            builder.AddAttribute(3, "class", "test-edit-submit");
            builder.AddAttribute(4, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, _ => Context.OnSubmit?.Invoke() ?? Task.CompletedTask));
            builder.AddContent(5, "Save");
            builder.CloseElement();
            builder.CloseElement();
        }
    }

    private sealed class TestContextMenuForm : ComponentBase
    {
        [Parameter] public ContextMenuContext<TestMetadata> Context { get; set; } = null!;
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "test-context-menu");
            foreach (var action in Context.AvailableActions)
            {
                builder.OpenElement(2, "button");
                builder.AddAttribute(3, "class", $"test-ctx-action-{action}");
                builder.AddAttribute(4, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, _ => Context.OnActionSelected?.Invoke(action) ?? Task.CompletedTask));
                builder.AddContent(5, action);
                builder.CloseElement();
            }
            builder.CloseElement();
        }
    }

    // ── Rendering tests ──────────────────────────────────────────

    [Fact]
    public void DocumentManager_Renders_Container()
    {
        var cut = RenderComponent<TmDocumentManager<TestMetadata>>(p => p
            .Add(c => c.DataProvider, CreateMockProvider()));

        cut.Find(".tm-file-manager").Should().NotBeNull();
    }

    [Fact]
    public void DocumentManager_Renders_With_Custom_Meta_Template()
    {
        var cut = RenderComponent<TmDocumentManager<TestMetadata>>(p => p
            .Add(c => c.DataProvider, CreateMockProvider())
            .Add(c => c.ItemMetaTemplate, MetaTemplate));

        cut.FindAll(".test-meta-template").Count.Should().BeGreaterThanOrEqualTo(2);
        cut.Markup.Should().Contain("General");
    }

    [Fact]
    public void DocumentManager_Permissions_Hide_Delete_Button()
    {
        var cut = RenderComponent<TmDocumentManager<TestMetadata>>(p => p
            .Add(c => c.DataProvider, CreateMockProvider(withPermissions: true))
            .Add(c => c.RespectPermissions, true));

        // Select Documents folder (CanDelete = false)
        var docs = cut.FindAll(".tm-file-manager__item").First(e => e.TextContent.Contains("Documents"));
        docs.Click();

        // Delete button should be hidden
        cut.FindAll(".tm-file-manager__toolbar-button").Any(b => b.TextContent.Contains("Delete")).Should().BeFalse();
    }

    [Fact]
    public void DocumentManager_Permissions_Hide_Rename_Button()
    {
        var cut = RenderComponent<TmDocumentManager<TestMetadata>>(p => p
            .Add(c => c.DataProvider, CreateMockProvider(withPermissions: true))
            .Add(c => c.RespectPermissions, true));

        // Select Documents folder (CanRename = false)
        var docs = cut.FindAll(".tm-file-manager__item").First(e => e.TextContent.Contains("Documents"));
        docs.Click();

        // Rename button should be hidden
        cut.FindAll(".tm-file-manager__toolbar-button").Any(b => b.TextContent.Contains("Rename")).Should().BeFalse();
    }

    [Fact]
    public void DocumentManager_Permissions_Hide_Item_With_CanRead_False()
    {
        var cut = RenderComponent<TmDocumentManager<TestMetadata>>(p => p
            .Add(c => c.DataProvider, CreateMockProvider(hideDocs: true))
            .Add(c => c.RespectPermissions, true));

        var mainItems = cut.FindAll(".tm-file-manager__item");
        // Documents folder has CanRead = false — should not appear in main content area
        mainItems.Any(i => i.TextContent.Contains("Documents")).Should().BeFalse();
        // Pictures should still be visible in main content area
        mainItems.Any(i => i.TextContent.Contains("Pictures")).Should().BeTrue();
    }

    // ── Custom form tests ────────────────────────────────────────

    [Fact]
    public async Task DocumentManager_NewFolder_Custom_Form_Submits()
    {
        var provider = CreateMockProvider();
        var cut = RenderComponent<TmDocumentManager<TestMetadata>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.NewFolderForm, NewFolderForm));

        // Click New Folder button
        var newFolderBtn = cut.FindAll(".tm-file-manager__toolbar-button")
            .First(b => b.TextContent.Contains("New Folder"));
        newFolderBtn.Click();

        // Custom form should appear
        cut.Find(".test-new-folder-form").Should().NotBeNull();

        // Submit custom form directly via context
        var formComponent = cut.FindComponent<TestNewFolderForm>();
        await cut.InvokeAsync(() => formComponent.Instance.Context.OnSubmit!());
        cut.Render();

        // Form should close
        cut.FindAll(".test-new-folder-form").Should().BeEmpty();
    }

    [Fact]
    public async Task DocumentManager_Delete_Custom_Form_Confirms()
    {
        var provider = CreateMockProvider();
        var cut = RenderComponent<TmDocumentManager<TestMetadata>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.DeleteForm, DeleteForm));

        // Select and delete
        var item = cut.FindAll(".tm-file-manager__item").First(e => e.TextContent.Contains("Documents"));
        item.Click();

        var deleteBtn = cut.FindAll(".tm-file-manager__toolbar-button")
            .First(b => b.TextContent.Contains("Delete"));
        deleteBtn.Click();

        // Custom delete form should appear
        cut.Find(".test-delete-form").Should().NotBeNull();

        // Confirm directly via context
        var formComponent = cut.FindComponent<TestDeleteForm>();
        await cut.InvokeAsync(() => formComponent.Instance.Context.OnConfirm!());
        cut.Render();

        // Form should close
        cut.FindAll(".test-delete-form").Should().BeEmpty();
        cut.Markup.Should().NotContain("Documents");
    }

    [Fact]
    public async Task DocumentManager_Edit_Form_Submits_Metadata()
    {
        var provider = CreateMockProvider();
        var cut = RenderComponent<TmDocumentManager<TestMetadata>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.EditForm, EditForm));

        // Select and edit
        var item = cut.FindAll(".tm-file-manager__item").First(e => e.TextContent.Contains("Documents"));
        item.Click();

        var editBtn = cut.FindAll(".tm-file-manager__toolbar-button")
            .First(b => b.TextContent.Contains("Edit"));
        editBtn.Click();

        // Edit form should appear
        cut.Find(".test-edit-form").Should().NotBeNull();

        // Submit directly via context
        var formComponent = cut.FindComponent<TestEditForm>();
        await cut.InvokeAsync(() => formComponent.Instance.Context.OnSubmit!());
        cut.Render();

        // Form should close
        cut.FindAll(".test-edit-form").Should().BeEmpty();

        // UpdateMetadataAsync should have been called
        provider.UpdateMetadataCalled.Should().BeTrue();
    }

    // ── Detail panel & context menu ──────────────────────────────

    [Fact]
    public void DocumentManager_DetailPanel_Renders()
    {
        var provider = CreateMockProvider();
        var cut = RenderComponent<TmDocumentManager<TestMetadata>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.DetailPanel, DetailPanel));

        // Select an item
        var item = cut.FindAll(".tm-file-manager__item").First(e => e.TextContent.Contains("Documents"));
        item.Click();

        // Click detail button
        var detailBtn = cut.FindAll(".tm-file-manager__toolbar-button")
            .First(b => b.TextContent.Contains("Detail"));
        detailBtn.Click();

        // Detail panel should appear
        cut.Find(".test-detail-panel").Should().NotBeNull();
        cut.Find(".test-detail-panel").TextContent.Should().Contain("Documents");
    }

    [Fact]
    public void DocumentManager_ContextMenu_Renders()
    {
        var cut = RenderComponent<TmDocumentManager<TestMetadata>>(p => p
            .Add(c => c.DataProvider, CreateMockProvider())
            .Add(c => c.ItemContextMenu, ContextMenu));

        // Right-click on an item
        var item = cut.FindAll(".tm-file-manager__item").First(e => e.TextContent.Contains("Documents"));
        item.ContextMenu();

        // Context menu should appear
        cut.Find(".test-context-menu").Should().NotBeNull();
    }

    // ── Keyboard & Id test ───────────────────────────────────────

    [Fact]
    public void DocumentManager_Keyboard_Enter_Navigates_Folder()
    {
        var cut = RenderComponent<TmDocumentManager<TestMetadata>>(p => p
            .Add(c => c.DataProvider, CreateMockProvider()));

        var wrapper = cut.Find(".tm-file-manager");
        // focusedIndex starts at 0 = Documents
        wrapper.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        // Should navigate into Documents and show Report.pdf
        cut.Markup.Should().Contain("Report.pdf");
    }

    [Fact]
    public void DocumentManager_Id_Used_Not_Path()
    {
        var provider = CreateMockProvider();
        var cut = RenderComponent<TmDocumentManager<TestMetadata>>(p => p
            .Add(c => c.DataProvider, provider));

        // Select Documents folder
        var item = cut.FindAll(".tm-file-manager__item").First(e => e.TextContent.Contains("Documents"));
        item.Click();

        // Start rename
        var renameBtn = cut.FindAll(".tm-file-manager__toolbar-button")
            .First(b => b.TextContent.Contains("Rename"));
        renameBtn.Click();

        // Type new name and press Enter
        var input = cut.Find(".tm-file-manager__item-rename-input");
        input.Input("RenamedDocs");
        input.KeyDown("Enter");

        // Verify that RenameAsync received the Id, not the Path
        provider.LastRenamedId.Should().Be("docs");
        provider.LastRenamedNewName.Should().Be("RenamedDocs");
    }

    // ── Mock Provider ────────────────────────────────────────────

    private sealed class MockDocumentManagerDataProvider : IDocumentManagerDataProvider<TestMetadata>
    {
        private readonly List<DocumentManagerItem<TestMetadata>> _allItems;

        public MockDocumentManagerDataProvider(List<DocumentManagerItem<TestMetadata>> items)
        {
            _allItems = items;
        }

        public string? LastRenamedId { get; private set; }
        public string? LastRenamedNewName { get; private set; }
        public bool UpdateMetadataCalled { get; private set; }

        public Task<IReadOnlyList<DocumentManagerItem<TestMetadata>>> GetFolderContentsAsync(
            string? folderPath = null, CancellationToken cancellationToken = default)
        {
            var path = folderPath ?? "/";
            var normalizedPath = path.TrimEnd('/');
            if (string.IsNullOrEmpty(normalizedPath)) normalizedPath = "/";

            var children = _allItems
                .Where(i => i.Path != path && GetParentPath(i.Path) == normalizedPath)
                .ToList();
            return Task.FromResult<IReadOnlyList<DocumentManagerItem<TestMetadata>>>(children);
        }

        public Task<IReadOnlyList<DocumentManagerItem<TestMetadata>>> GetFolderTreeAsync(
            CancellationToken cancellationToken = default)
        {
            var folders = _allItems.Where(i => i.IsDirectory).ToList();
            return Task.FromResult<IReadOnlyList<DocumentManagerItem<TestMetadata>>>(folders);
        }

        public Task<DocumentManagerItem<TestMetadata>> GetItemDetailAsync(
            string itemId, CancellationToken cancellationToken = default)
        {
            var item = _allItems.First(i => i.Id == itemId);
            return Task.FromResult(item);
        }

        public Task<DocumentManagerItem<TestMetadata>> CreateFolderAsync(
            string parentPath, string folderName, TestMetadata? metadata = null,
            CancellationToken cancellationToken = default)
        {
            var item = new DocumentManagerItem<TestMetadata>
            {
                Id = Guid.NewGuid().ToString(),
                Name = folderName,
                Path = $"{parentPath.TrimEnd('/')}/{folderName}",
                IsDirectory = true,
                Metadata = metadata
            };
            _allItems.Add(item);
            return Task.FromResult(item);
        }

        public Task<DocumentManagerItem<TestMetadata>> RenameAsync(
            string itemId, string newName, CancellationToken cancellationToken = default)
        {
            LastRenamedId = itemId;
            LastRenamedNewName = newName;

            var item = _allItems.First(i => i.Id == itemId);
            item.Name = newName;
            var parent = GetParentPath(item.Path);
            item.Path = $"{parent}/{newName}";
            return Task.FromResult(item);
        }

        public Task DeleteAsync(
            IReadOnlyList<string> itemIds, CancellationToken cancellationToken = default)
        {
            _allItems.RemoveAll(i => itemIds.Contains(i.Id));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DocumentManagerItem<TestMetadata>>> UploadAsync(
            string folderPath, IReadOnlyList<FileUploadInfo> files,
            IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            foreach (var file in files)
            {
                var path = $"{folderPath.TrimEnd('/')}/{file.FileName}";
                _allItems.Add(new DocumentManagerItem<TestMetadata>
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = file.FileName,
                    Path = path,
                    IsDirectory = false,
                    Size = file.Size,
                    Extension = System.IO.Path.GetExtension(file.FileName)
                });
                file.Stream.Dispose();
            }
            return Task.FromResult<IReadOnlyList<DocumentManagerItem<TestMetadata>>>(
                _allItems.Where(i => files.Any(f => f.FileName == i.Name)).ToList());
        }

        public Task<Stream> DownloadAsync(string fileId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Stream>(new System.IO.MemoryStream());
        }

        public Task<DocumentManagerItem<TestMetadata>> UpdateMetadataAsync(
            string itemId, TestMetadata metadata, CancellationToken cancellationToken = default)
        {
            UpdateMetadataCalled = true;
            var item = _allItems.First(i => i.Id == itemId);
            item.Metadata = metadata;
            return Task.FromResult(item);
        }

        public Task<DocumentManagerItem<TestMetadata>> MoveAsync(
            string itemId, string targetFolderPath, CancellationToken cancellationToken = default)
        {
            var item = _allItems.First(i => i.Id == itemId);
            item.Path = $"{targetFolderPath.TrimEnd('/')}/{item.Name}";
            return Task.FromResult(item);
        }

        public Task<DocumentManagerItem<TestMetadata>> CopyAsync(
            string itemId, string targetFolderPath, CancellationToken cancellationToken = default)
        {
            var original = _allItems.First(i => i.Id == itemId);
            var copy = new DocumentManagerItem<TestMetadata>
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Copy of {original.Name}",
                Path = $"{targetFolderPath.TrimEnd('/')}/Copy of {original.Name}",
                IsDirectory = original.IsDirectory,
                Size = original.Size,
                Extension = original.Extension,
                Metadata = original.Metadata
            };
            _allItems.Add(copy);
            return Task.FromResult(copy);
        }

        private static string GetParentPath(string itemPath)
        {
            itemPath = itemPath.TrimEnd('/');
            var lastSlash = itemPath.LastIndexOf('/');
            if (lastSlash <= 0) return "/";
            return itemPath.Substring(0, lastSlash);
        }
    }
}
