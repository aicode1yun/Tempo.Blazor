using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Tempo.Blazor.Abstractions.Interfaces;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Components.Files;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Files;

/// <summary>K4: chunked upload wiring, scan-hook gating, and full versioning UI.</summary>
public class TmFileUploadK4Tests : LocalizationTestBase
{
    public sealed record DocMeta(string Note = "");

    // ── TmFileUploadProgress ─────────────────────────────────────

    [Fact]
    public void UploadProgress_RendersStatesAndActions()
    {
        var uploads = new List<TmUploadItem>
        {
            new() { FileName = "a.bin", Percent = 40, State = TmUploadState.Uploading },
            new() { FileName = "b.bin", Percent = 100, State = TmUploadState.Cancelled },
            new() { FileName = "c.bin", Percent = 100, State = TmUploadState.Completed },
            new() { FileName = "d.exe", Percent = 100, State = TmUploadState.Blocked },
        };

        var cut = Render<TmFileUploadProgress>(p => p.Add(c => c.Uploads, uploads));

        cut.FindAll("[data-testid='upload-item']").Should().HaveCount(4);
        // Uploading → cancel available
        cut.FindAll("[data-state='uploading'] [data-testid='upload-cancel']").Should().ContainSingle();
        // Cancelled → resume available
        cut.FindAll("[data-state='cancelled'] [data-testid='upload-resume']").Should().ContainSingle();
        // Completed / Blocked → dismiss available
        cut.FindAll("[data-state='completed'] [data-testid='upload-dismiss']").Should().ContainSingle();
        cut.FindAll("[data-state='blocked'] [data-testid='upload-dismiss']").Should().ContainSingle();
    }

    [Fact]
    public void UploadProgress_Callbacks_Fire()
    {
        TmUploadItem? cancelled = null, resumed = null, dismissed = null;
        var uploads = new List<TmUploadItem>
        {
            new() { FileName = "up.bin", Percent = 10, State = TmUploadState.Uploading },
            new() { FileName = "cx.bin", Percent = 10, State = TmUploadState.Failed },
            new() { FileName = "done.bin", Percent = 100, State = TmUploadState.Completed },
        };

        var cut = Render<TmFileUploadProgress>(p => p
            .Add(c => c.Uploads, uploads)
            .Add(c => c.OnCancel, (TmUploadItem i) => cancelled = i)
            .Add(c => c.OnResume, (TmUploadItem i) => resumed = i)
            .Add(c => c.OnDismiss, (TmUploadItem i) => dismissed = i));

        cut.Find("[data-state='uploading'] [data-testid='upload-cancel']").Click();
        cut.Find("[data-state='failed'] [data-testid='upload-resume']").Click();
        cut.Find("[data-state='completed'] [data-testid='upload-dismiss']").Click();

        cancelled!.FileName.Should().Be("up.bin");
        resumed!.FileName.Should().Be("cx.bin");
        dismissed!.FileName.Should().Be("done.bin");
    }

    // ── TmFileVersionHistory ─────────────────────────────────────

    [Fact]
    public void VersionHistory_Empty_ShowsEmptyState()
    {
        var cut = Render<TmFileVersionHistory>(p => p
            .Add(c => c.ItemId, "item-1")
            .Add(c => c.Hook, new FakeVersioningHook()));

        cut.Find("[data-testid='version-empty']").Should().NotBeNull();
    }

    [Fact]
    public void VersionHistory_ListsVersions_CurrentBadgeOnLatest()
    {
        var hook = new FakeVersioningHook();
        hook.Seed("item-1", ("v1 line", 1), ("v1 line\nv2 line", 2), ("v1 line\nv2 line\nv3 line", 3));

        var cut = Render<TmFileVersionHistory>(p => p
            .Add(c => c.ItemId, "item-1")
            .Add(c => c.Hook, hook));

        cut.FindAll("[data-testid='version-item']").Should().HaveCount(3);
        cut.FindAll("[data-testid='version-current']").Should().ContainSingle();
        // newest first: first rendered item is v3 and is current
        cut.FindAll("[data-testid='version-item']")[0].GetAttribute("data-version").Should().Be("3");
    }

    [Fact]
    public async Task VersionHistory_Restore_CallsHookAndRaisesEvent()
    {
        var hook = new FakeVersioningHook();
        hook.Seed("item-1", ("old", 1), ("new", 2));
        TmFileVersion? restored = null;

        var cut = Render<TmFileVersionHistory>(p => p
            .Add(c => c.ItemId, "item-1")
            .Add(c => c.Hook, hook)
            .Add(c => c.OnRestored, (TmFileVersion v) => restored = v));

        // The non-current (older) version exposes a restore button.
        cut.Find("[data-testid='version-restore']").Click();
        await cut.InvokeAsync(() => { });

        hook.RestoreCalled.Should().BeTrue();
        restored.Should().NotBeNull();
        // A restore adds a new current version.
        cut.FindAll("[data-testid='version-item']").Should().HaveCount(3);
    }

    [Fact]
    public void VersionHistory_Compare_ShowsTextDiff()
    {
        var hook = new FakeVersioningHook();
        hook.Seed("item-1", ("line1\nline2", 1), ("line1\nline2\nline3", 2));

        var cut = Render<TmFileVersionHistory>(p => p
            .Add(c => c.ItemId, "item-1")
            .Add(c => c.Hook, hook));

        cut.Find("[data-testid='version-compare']").Click();

        cut.Find("[data-testid='version-diff']").Should().NotBeNull();
        cut.Find("[data-testid='diff-added']").TextContent.Should().Contain("1"); // line3 added
    }

    // ── Chunked upload: TmDocumentManager ────────────────────────

    [Fact]
    public async Task DocumentManager_ChunkedUpload_SplitsIntoChunksAndCreatesItem()
    {
        var provider = new ChunkingDocProvider();
        var cut = Render<TmDocumentManager<DocMeta>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.MaxUploadSize, 50L * 1024 * 1024));

        var big = new string('x', 300 * 1024); // ~1.2 chunks → 2 chunks
        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText(big, "large.txt", contentType: "text/plain"));
        await cut.InvokeAsync(() => { });

        provider.ChunkCount.Should().BeGreaterThan(1);
        provider.LastCompleted.Should().BeTrue();
        cut.Markup.Should().Contain("large.txt");
    }

    [Fact]
    public async Task DocumentManager_ScanBlocked_MarksItemUnavailable()
    {
        var provider = new ChunkingDocProvider();
        var cut = Render<TmDocumentManager<DocMeta>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.ScanHook, new BlockingScanHook()));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("payload", "virus.exe", contentType: "application/octet-stream"));
        await cut.InvokeAsync(() => { });

        // Blocked badge shown; the file is treated as unavailable.
        cut.FindAll("[data-testid='file-scan-blocked']").Should().ContainSingle();

        // Select the blocked file → no Download toolbar button offered.
        var item = cut.FindAll(".tm-file-manager__item").First(e => e.TextContent.Contains("virus.exe"));
        item.Click();
        cut.FindAll(".tm-file-manager__toolbar-button")
            .Any(b => b.TextContent.Contains("Download")).Should().BeFalse();
    }

    [Fact]
    public async Task DocumentManager_ScanBlocked_SurvivesProviderReturningFreshItems()
    {
        // Regression: a provider that returns NEW item instances on every reload must still
        // show the file as Blocked (the scan gate is re-applied from the recorded verdict).
        var provider = new ChunkingDocProvider { CloneOnRead = true };
        var cut = Render<TmDocumentManager<DocMeta>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.ScanHook, new BlockingScanHook()));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("payload", "virus.exe", contentType: "application/octet-stream"));
        await cut.InvokeAsync(() => { });

        cut.FindAll("[data-testid='file-scan-blocked']").Should().ContainSingle();

        // Force another reload (fresh instances) — the badge must persist.
        await cut.InvokeAsync(async () =>
        {
            var reload = typeof(TmDocumentManager<DocMeta>)
                .GetMethod("LoadDataAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            await (System.Threading.Tasks.Task)reload!.Invoke(cut.Instance, null)!;
        });
        cut.Render();

        cut.FindAll("[data-testid='file-scan-blocked']").Should().ContainSingle();
    }

    // ── Chunked upload: TmFileManager ────────────────────────────

    [Fact]
    public async Task FileManager_ChunkedUpload_UsesChunkSink()
    {
        var provider = new ChunkingFileProvider();
        var cut = Render<TmFileManager>(p => p
            .Add(c => c.DataProvider, provider));

        var big = new string('y', 300 * 1024);
        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText(big, "movie.bin", contentType: "application/octet-stream"));
        await cut.InvokeAsync(() => { });

        provider.ChunkCount.Should().BeGreaterThan(1);
        cut.Markup.Should().Contain("movie.bin");
    }

    [Fact]
    public void FileManager_BlockedItem_RendersBadge()
    {
        var provider = new ChunkingFileProvider();
        provider.AddExisting(new FileManagerItem
        {
            Id = "/f.bin", Name = "f.bin", Path = "/f.bin",
            ScanStatus = FileScanStatus.Blocked, ScanMessage = "Threat"
        });

        var cut = Render<TmFileManager>(p => p.Add(c => c.DataProvider, provider));

        cut.FindAll("[data-testid='file-scan-blocked']").Should().ContainSingle();
    }

    // ── Chunked upload: TmAttachmentManager ──────────────────────

    [Fact]
    public async Task AttachmentManager_ChunkedUpload_UsesChunkProvider()
    {
        var provider = new ChunkingAttachmentProvider();
        var cut = Render<TmAttachmentManager>(p => p
            .Add(c => c.AttachmentProvider, provider)
            .Add(c => c.FileProvider, provider)
            .Add(c => c.EntityId, "e-1"));

        var big = new string('z', 300 * 1024);
        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText(big, "clip.bin", contentType: "application/octet-stream"));
        await cut.InvokeAsync(() => { });

        provider.ChunkCount.Should().BeGreaterThan(1);
        cut.Markup.Should().Contain("clip.bin");
    }

    [Fact]
    public void AttachmentManager_BlockedAttachment_HidesDownload()
    {
        var att = new TmAttachment
        {
            Id = "a1",
            EntityRef = TmEntityRef.Create("attachment-manager-entity", "e-1"),
            FileName = "bad.exe",
            SizeBytes = 10,
            CanDownload = true,
            ScanStatus = FileScanStatus.Blocked
        };
        var provider = new ChunkingAttachmentProvider([att]);

        var cut = Render<TmAttachmentManager>(p => p
            .Add(c => c.AttachmentProvider, provider)
            .Add(c => c.FileProvider, provider)
            .Add(c => c.EntityId, "e-1"));

        cut.FindAll("[data-testid='attachment-scan-blocked']").Should().ContainSingle();
        cut.FindAll(".tm-attachment-download").Should().BeEmpty();
    }

    // ── Fakes ────────────────────────────────────────────────────

    private sealed class BlockingScanHook : IFileScanHook
    {
        public Task<FileScanResult> ScanAsync(FileScanRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(request.FileName.Contains("virus", StringComparison.OrdinalIgnoreCase)
                ? FileScanResult.BlockedBy("EICAR-Test", "Blocked by scan")
                : FileScanResult.Clean());
    }

    private sealed class FakeVersioningHook : IFileVersioningHook
    {
        private readonly Dictionary<string, List<(TmFileVersion v, string content)>> _store = new();
        public bool RestoreCalled { get; private set; }

        public void Seed(string itemId, params (string content, int number)[] versions)
        {
            var list = new List<(TmFileVersion, string)>();
            foreach (var (content, number) in versions)
            {
                list.Add((new TmFileVersion
                {
                    VersionId = $"{itemId}-{number}",
                    ItemId = itemId,
                    VersionNumber = number,
                    FileName = $"{itemId}.txt",
                    SizeBytes = content.Length,
                    IsCurrent = number == versions.Max(x => x.number)
                }, content));
            }
            _store[itemId] = list;
        }

        public Task<TmFileVersion> CreateVersionAsync(FileVersionRequest request, CancellationToken cancellationToken = default)
        {
            var list = _store.TryGetValue(request.ItemId, out var l) ? l : _store[request.ItemId] = [];
            foreach (var e in list) e.v.IsCurrent = false;
            var v = new TmFileVersion
            {
                ItemId = request.ItemId,
                VersionNumber = list.Count + 1,
                FileName = request.FileName,
                SizeBytes = request.SizeBytes,
                IsCurrent = true
            };
            list.Add((v, ""));
            return Task.FromResult(v);
        }

        public Task<IReadOnlyList<TmFileVersion>> GetVersionsAsync(string itemId, CancellationToken cancellationToken = default)
        {
            var list = _store.TryGetValue(itemId, out var l)
                ? l.Select(e => e.v).OrderByDescending(v => v.VersionNumber).ToList()
                : [];
            return Task.FromResult<IReadOnlyList<TmFileVersion>>(list);
        }

        public Task<TmFileVersion> RestoreVersionAsync(string itemId, string versionId, CancellationToken cancellationToken = default)
        {
            RestoreCalled = true;
            var list = _store[itemId];
            var src = list.First(e => e.v.VersionId == versionId);
            foreach (var e in list) e.v.IsCurrent = false;
            var restored = new TmFileVersion
            {
                ItemId = itemId,
                VersionNumber = list.Count + 1,
                FileName = src.v.FileName,
                SizeBytes = src.v.SizeBytes,
                IsCurrent = true
            };
            list.Add((restored, src.content));
            return Task.FromResult(restored);
        }

        public Task<TmFileVersionDiff> DiffAsync(string itemId, string fromVersionId, string toVersionId, CancellationToken cancellationToken = default)
        {
            var list = _store[itemId];
            var from = list.First(e => e.v.VersionId == fromVersionId);
            var to = list.First(e => e.v.VersionId == toVersionId);
            var lines = TmTextLineDiff.Compute(from.content, to.content);
            return Task.FromResult(new TmFileVersionDiff
            {
                ItemId = itemId,
                FromVersionId = fromVersionId,
                ToVersionId = toVersionId,
                IsTextDiff = true,
                Lines = lines,
                SizeDelta = to.v.SizeBytes - from.v.SizeBytes,
                FromFileName = from.v.FileName,
                ToFileName = to.v.FileName
            });
        }
    }

    private sealed class ChunkingDocProvider : IDocumentManagerDataProvider<DocMeta>, ITmChunkedFileProvider
    {
        private readonly List<DocumentManagerItem<DocMeta>> _items = [];
        private readonly Dictionary<string, List<byte>> _buffers = new();
        public int ChunkCount { get; private set; }
        public bool LastCompleted { get; private set; }

        /// <summary>When true, returns fresh item instances per read (simulates an HTTP/DB provider).</summary>
        public bool CloneOnRead { get; init; }

        private static DocumentManagerItem<DocMeta> Clone(DocumentManagerItem<DocMeta> i) => new()
        {
            Id = i.Id, Name = i.Name, Path = i.Path, IsDirectory = i.IsDirectory,
            Size = i.Size, Extension = i.Extension, ModifiedDate = i.ModifiedDate
        };

        public Task<IReadOnlyList<DocumentManagerItem<DocMeta>>> GetFolderContentsAsync(string? folderPath = null, CancellationToken ct = default)
        {
            var items = _items.Where(i => i.Path.StartsWith("/"));
            if (CloneOnRead) items = items.Select(Clone);
            return Task.FromResult<IReadOnlyList<DocumentManagerItem<DocMeta>>>(items.ToList());
        }
        public Task<IReadOnlyList<DocumentManagerItem<DocMeta>>> GetFolderTreeAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DocumentManagerItem<DocMeta>>>([]);
        public Task<DocumentManagerItem<DocMeta>> GetItemDetailAsync(string itemId, CancellationToken ct = default)
            => Task.FromResult(_items.First(i => i.Id == itemId));
        public Task<DocumentManagerItem<DocMeta>> CreateFolderAsync(string parentPath, string folderName, DocMeta? metadata = null, CancellationToken ct = default)
            => Task.FromResult(new DocumentManagerItem<DocMeta> { Id = folderName, Name = folderName, Path = $"/{folderName}", IsDirectory = true });
        public Task<DocumentManagerItem<DocMeta>> RenameAsync(string itemId, string newName, CancellationToken ct = default)
            => Task.FromResult(_items.First(i => i.Id == itemId));
        public Task DeleteAsync(IReadOnlyList<string> itemIds, CancellationToken ct = default) { _items.RemoveAll(i => itemIds.Contains(i.Id)); return Task.CompletedTask; }
        public Task<IReadOnlyList<DocumentManagerItem<DocMeta>>> UploadAsync(string folderPath, IReadOnlyList<FileUploadInfo> files, DocMeta? metadata = null, string? name = null, IProgress<int>? progress = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DocumentManagerItem<DocMeta>>>([]);
        public Task<Stream> DownloadAsync(string fileId, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream());
        public Task<DocumentManagerItem<DocMeta>> UpdateMetadataAsync(string itemId, DocMeta metadata, CancellationToken ct = default)
            => Task.FromResult(_items.First(i => i.Id == itemId));
        public Task<DocumentManagerItem<DocMeta>> MoveAsync(string itemId, string targetFolderPath, CancellationToken ct = default)
            => Task.FromResult(_items.First(i => i.Id == itemId));
        public Task<DocumentManagerItem<DocMeta>> CopyAsync(string itemId, string targetFolderPath, CancellationToken ct = default)
            => Task.FromResult(_items.First(i => i.Id == itemId));
        public Task<IReadOnlyList<TmAttachment>> GetAttachmentsAsync(string itemId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TmAttachment>>([]);
        public Task<IReadOnlyList<TmAttachment>> AddAttachmentsAsync(string itemId, IReadOnlyList<FileUploadInfo> files, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TmAttachment>>([]);
        public Task RemoveAttachmentAsync(string itemId, string attachmentId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Stream> DownloadAttachmentAsync(string itemId, string attachmentId, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream());
        public Task<Stream> DownloadAllAttachmentsAsync(string itemId, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream());

        public Task<TmFileUploadResult> UploadChunkAsync(TmFileChunk chunk, CancellationToken cancellationToken = default)
        {
            ChunkCount++;
            var key = chunk.UploadSessionId ?? chunk.FileName;
            if (!_buffers.TryGetValue(key, out var buf)) buf = _buffers[key] = [];
            buf.AddRange(chunk.Data);
            if (chunk.IsLast)
            {
                LastCompleted = true;
                _items.Add(new DocumentManagerItem<DocMeta>
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = chunk.FileName,
                    Path = $"/{chunk.FileName}",
                    Size = buf.Count,
                    Extension = Path.GetExtension(chunk.FileName)
                });
            }
            return Task.FromResult(new TmFileUploadResult
            {
                Success = true,
                IsComplete = chunk.IsLast,
                UploadSessionId = key,
                AssetId = chunk.IsLast ? key : null,
                FileName = chunk.FileName
            });
        }
    }

    private sealed class ChunkingFileProvider : IFileManagerDataProvider, ITmChunkedFileProvider
    {
        private readonly List<FileManagerItem> _items = [];
        public int ChunkCount { get; private set; }
        public void AddExisting(FileManagerItem item) => _items.Add(item);

        public Task<IReadOnlyList<FileManagerItem>> GetFolderContentsAsync(string? folderPath = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<FileManagerItem>>(_items.ToList());
        public Task<IReadOnlyList<FileManagerItem>> GetFolderTreeAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<FileManagerItem>>([]);
        public Task<FileManagerItem> CreateFolderAsync(string parentPath, string folderName, CancellationToken ct = default)
            => Task.FromResult(new FileManagerItem { Id = folderName, Name = folderName, Path = $"/{folderName}", IsDirectory = true });
        public Task<FileManagerItem> RenameAsync(string itemPath, string newName, CancellationToken ct = default)
            => Task.FromResult(_items.First(i => i.Path == itemPath));
        public Task DeleteAsync(IReadOnlyList<string> itemPaths, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<FileManagerItem>> UploadAsync(string folderPath, IReadOnlyList<FileUploadInfo> files, IProgress<int>? progress = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<FileManagerItem>>([]);
        public Task<Stream> DownloadAsync(string filePath, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream());

        public Task<TmFileUploadResult> UploadChunkAsync(TmFileChunk chunk, CancellationToken cancellationToken = default)
        {
            ChunkCount++;
            if (chunk.IsLast)
            {
                _items.Add(new FileManagerItem
                {
                    Id = $"/{chunk.FileName}", Name = chunk.FileName, Path = $"/{chunk.FileName}",
                    Size = chunk.TotalSizeBytes, Extension = Path.GetExtension(chunk.FileName)
                });
            }
            return Task.FromResult(new TmFileUploadResult { Success = true, IsComplete = chunk.IsLast, UploadSessionId = chunk.FileName });
        }
    }

    private sealed class ChunkingAttachmentProvider(IReadOnlyList<TmAttachment>? seed = null)
        : ITmAttachmentProvider, ITmFileProvider, ITmChunkedFileProvider
    {
        private readonly List<TmAttachment> _attachments = seed?.ToList() ?? [];
        public int ChunkCount { get; private set; }

        TmAttachmentProviderCapabilities ITmAttachmentProvider.Capabilities
            => TmAttachmentProviderCapabilities.Read | TmAttachmentProviderCapabilities.Add | TmAttachmentProviderCapabilities.Remove;
        TmAttachmentProviderCapabilities ITmCapabilityProvider<TmAttachmentProviderCapabilities>.Capabilities
            => TmAttachmentProviderCapabilities.Read | TmAttachmentProviderCapabilities.Add | TmAttachmentProviderCapabilities.Remove;
        TmFileProviderCapabilities ITmFileProvider.Capabilities
            => TmFileProviderCapabilities.Upload | TmFileProviderCapabilities.ChunkUpload;
        TmFileProviderCapabilities ITmCapabilityProvider<TmFileProviderCapabilities>.Capabilities
            => TmFileProviderCapabilities.Upload | TmFileProviderCapabilities.ChunkUpload;

        public Task<IReadOnlyList<TmAttachment>> GetForEntityAsync(TmEntityRef entityRef, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TmAttachment>>(_attachments.ToList());
        public Task<TmAttachment> AddAsync(TmAttachment attachment, CancellationToken ct = default) { _attachments.Add(attachment); return Task.FromResult(attachment); }
        public Task RemoveAsync(TmEntityRef entityRef, string attachmentId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<TmFileUploadResult> UploadAsync(TmFileUploadRequest request, Stream content, CancellationToken ct = default)
            => Task.FromResult(new TmFileUploadResult { Success = true, IsComplete = true, AssetId = Guid.NewGuid().ToString("N"), FileName = request.FileName });
        public Task<TmFileResolveResult> ResolveAsync(TmFileResolveRequest request, CancellationToken ct = default)
            => Task.FromResult(new TmFileResolveResult { Success = true, AssetId = request.AssetId, Url = "https://x/y" });
        public Task DeleteAsync(string assetId, CancellationToken ct = default) => Task.CompletedTask;

        public Task<TmFileUploadResult> UploadChunkAsync(TmFileChunk chunk, CancellationToken cancellationToken = default)
        {
            ChunkCount++;
            return Task.FromResult(new TmFileUploadResult
            {
                Success = true, IsComplete = chunk.IsLast, UploadSessionId = chunk.FileName,
                AssetId = chunk.IsLast ? Guid.NewGuid().ToString("N") : null, FileName = chunk.FileName,
                SizeBytes = chunk.TotalSizeBytes
            });
        }
    }

}

/// <summary>Unit tests for the reusable LCS line-diff utility.</summary>
public class TmTextLineDiffTests
{
    [Fact]
    public void Diff_AddedLine_IsDetected()
    {
        var diff = TmTextLineDiff.Compute("a\nb", "a\nb\nc");
        diff.Count(l => l.Kind == TmFileVersionDiffKind.Added).Should().Be(1);
        diff.Count(l => l.Kind == TmFileVersionDiffKind.Removed).Should().Be(0);
        diff.Last().Text.Should().Be("c");
        diff.Last().NewLineNumber.Should().Be(3);
    }

    [Fact]
    public void Diff_RemovedAndChanged_AreDetected()
    {
        // "b" replaced by "B": one removed + one added; "a" and "c" unchanged.
        var diff = TmTextLineDiff.Compute("a\nb\nc", "a\nB\nc");
        diff.Count(l => l.Kind == TmFileVersionDiffKind.Added).Should().Be(1);
        diff.Count(l => l.Kind == TmFileVersionDiffKind.Removed).Should().Be(1);
        diff.Count(l => l.Kind == TmFileVersionDiffKind.Unchanged).Should().Be(2);
    }

    [Fact]
    public void Diff_Identical_AllUnchanged()
    {
        var diff = TmTextLineDiff.Compute("x\ny", "x\ny");
        diff.Should().OnlyContain(l => l.Kind == TmFileVersionDiffKind.Unchanged);
    }

    [Fact]
    public void Diff_EmptyOld_AllAdded()
    {
        var diff = TmTextLineDiff.Compute("", "one\ntwo");
        diff.Should().OnlyContain(l => l.Kind == TmFileVersionDiffKind.Added);
        diff.Should().HaveCount(2);
    }
}
