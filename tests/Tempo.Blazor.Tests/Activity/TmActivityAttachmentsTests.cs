using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Components.Activity;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Activity;

public class TmActivityAttachmentsTests : LocalizationTestBase
{
    private static List<TmAttachment> SampleAttachments() =>
    [
        new()
        {
            Id = "a1",
            EntityRef = Entity("entity-1"),
            FileName = "photo.png",
            ContentType = "image/png",
            SizeBytes = 1_024,
            UploadedAt = DateTimeOffset.Now.AddDays(-1),
            UploadedBy = new TmUserRef { Id = "alice", DisplayName = "Alice" },
            CanDelete = true,
            CanDownload = true
        },
        new()
        {
            Id = "a2",
            EntityRef = Entity("entity-1"),
            FileName = "report.pdf",
            ContentType = "application/pdf",
            SizeBytes = 512 * 1_024,
            UploadedAt = DateTimeOffset.Now.AddDays(-2),
            UploadedBy = new TmUserRef { Id = "bob", DisplayName = "Bob" },
            CanDelete = false,
            CanDownload = true
        },
    ];

    private static ITmAttachmentProvider BuildProvider(
        IReadOnlyList<TmAttachment>? list = null)
    {
        var p = Substitute.For<ITmAttachmentProvider, ITmFileProvider, ITmChunkedFileProvider>();
        p.Capabilities.Returns(TmAttachmentProviderCapabilities.Read | TmAttachmentProviderCapabilities.Add | TmAttachmentProviderCapabilities.Remove);
        p.GetForEntityAsync(Arg.Any<TmEntityRef>(), Arg.Any<CancellationToken>())
         .Returns(Task.FromResult(list ?? (IReadOnlyList<TmAttachment>)SampleAttachments()));
        p.AddAsync(Arg.Any<TmAttachment>(), Arg.Any<CancellationToken>())
         .Returns(call => Task.FromResult(call.Arg<TmAttachment>()));
        p.RemoveAsync(Arg.Any<TmEntityRef>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
         .Returns(Task.CompletedTask);

        var fileProvider = (ITmFileProvider)p;
        fileProvider.Capabilities.Returns(
            TmFileProviderCapabilities.Upload
            | TmFileProviderCapabilities.Resolve
            | TmFileProviderCapabilities.Delete
            | TmFileProviderCapabilities.ChunkUpload);
        fileProvider.ResolveAsync(Arg.Any<TmFileResolveRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TmFileResolveResult
            {
                Success = true,
                Url = "https://cdn.example.com/file"
            }));

        var chunkedProvider = (ITmChunkedFileProvider)p;
        chunkedProvider.UploadChunkAsync(Arg.Any<TmFileChunk>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new TmFileUploadResult
            {
                Success = true,
                IsComplete = call.Arg<TmFileChunk>().IsLast,
                AssetId = call.Arg<TmFileChunk>().IsLast ? "asset-1" : null,
                UploadSessionId = "session-1"
            }));

        return p;
    }

    [Fact]
    public void Attachments_RendersFileList()
    {
        var cut = Render<TmActivityAttachments>(p => p
            .Add(c => c.Attachments, SampleAttachments()));

        cut.FindAll(".tm-attach-item").Count.Should().Be(2);
    }

    [Fact]
    public void Attachments_Empty_RendersEmptyState()
    {
        var cut = Render<TmActivityAttachments>(p => p
            .Add(c => c.Attachments, Array.Empty<TmAttachment>()));

        cut.FindAll(".tm-attach-item").Should().BeEmpty();
        cut.FindAll(".tm-empty-state, .tm-attach-empty").Should().NotBeEmpty();
    }

    [Fact]
    public void Attachments_FileIcon_ByMimeType()
    {
        var cut = Render<TmActivityAttachments>(p => p
            .Add(c => c.Attachments, SampleAttachments()));

        var items = cut.FindAll(".tm-attach-item");
        // photo.png → image icon
        items[0].QuerySelectorAll(".tm-attach-icon-image").Length.Should().Be(1);
        // report.pdf → pdf icon
        items[1].QuerySelectorAll(".tm-attach-icon-pdf").Length.Should().Be(1);
    }

    [Fact]
    public void Attachments_FormatSize_BytesKBMB()
    {
        var list = new[]
        {
            Attachment("a1", "tiny.txt", "text/plain", 512),
            Attachment("a2", "mid.zip", "application/zip", 2 * 1024),
            Attachment("a3", "big.mp4", "video/mp4", 3 * 1024 * 1024),
        };
        var cut = Render<TmActivityAttachments>(p => p.Add(c => c.Attachments, list));

        var sizes = cut.FindAll(".tm-attach-size");
        sizes[0].TextContent.Should().Contain("B");
        sizes[1].TextContent.Should().Contain("KB");
        sizes[2].TextContent.Should().Contain("MB");
    }

    [Fact]
    public void Attachments_DeleteButton_ShowsWhenCanDelete()
    {
        var cut = Render<TmActivityAttachments>(p => p
            .Add(c => c.Attachments, SampleAttachments()));

        var items = cut.FindAll(".tm-attach-item");
        items[0].QuerySelectorAll(".tm-attach-delete-btn").Length.Should().Be(1);  // CanDelete=true
        items[1].QuerySelectorAll(".tm-attach-delete-btn").Length.Should().Be(0);  // CanDelete=false
    }

    [Fact]
    public async Task Attachments_Delete_CallsProvider()
    {
        var provider = BuildProvider();
        var cut = Render<TmActivityAttachments>(p => p
            .Add(c => c.Attachments, SampleAttachments())
            .Add(c => c.AttachmentProvider, provider)
            .Add(c => c.EntityId, "entity-1"));

        cut.Find(".tm-attach-delete-btn").Click();
        await cut.InvokeAsync(() => { });

        await provider.Received(1).RemoveAsync(
            Arg.Is<TmEntityRef>(e => e.EntityId == "entity-1"),
            "a1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Attachments_Upload_ChunksFile_256KB()
    {
        const int ChunkSize = 256 * 1024;
        var provider = BuildProvider(new List<TmAttachment>());
        var cut = Render<TmActivityAttachments>(p => p
            .Add(c => c.AttachmentProvider, provider)
            .Add(c => c.FileProvider, (ITmFileProvider)provider)
            .Add(c => c.EntityId, "entity-1")
            .Add(c => c.AllowUpload, true));

        // 512KB file → exactly 2 chunks of 256KB
        var content = new byte[2 * ChunkSize];
        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromBinary(content, "large.bin", contentType: "application/octet-stream"));

        await cut.InvokeAsync(() => { });

        await ((ITmChunkedFileProvider)provider).Received(2).UploadChunkAsync(
            Arg.Any<TmFileChunk>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Attachments_UploadProgress_Displayed()
    {
        var blocker = new TaskCompletionSource<TmFileUploadResult>();
        var chunkCalled = new SemaphoreSlim(0, 1);
        var provider = BuildProvider(new List<TmAttachment>());
        ((ITmChunkedFileProvider)provider).UploadChunkAsync(Arg.Any<TmFileChunk>(), Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    chunkCalled.Release();
                    return blocker.Task;
                });

        var cut = Render<TmActivityAttachments>(p => p
            .Add(c => c.AttachmentProvider, provider)
            .Add(c => c.FileProvider, (ITmFileProvider)provider)
            .Add(c => c.EntityId, "entity-1")
            .Add(c => c.AllowUpload, true));

        // Kick off upload in background
        _ = Task.Run(() =>
            cut.FindComponent<InputFile>().UploadFiles(
                InputFileContent.CreateFromBinary(new byte[100], "f.bin", contentType: "application/octet-stream")));

        // Wait until the component has called UploadChunkAsync (_uploading=true and StateHasChanged already ran)
        await chunkCalled.WaitAsync(TimeSpan.FromSeconds(5));
        cut.Render();

        cut.FindAll(".tm-attach-progress").Should().NotBeEmpty();

        blocker.SetResult(new TmFileUploadResult { Success = true, IsComplete = true, AssetId = "asset-1" });
    }

    [Fact]
    public async Task Attachments_Upload_Complete_RefreshesListAsync()
    {
        var provider = BuildProvider(new List<TmAttachment>());
        var cut = Render<TmActivityAttachments>(p => p
            .Add(c => c.AttachmentProvider, provider)
            .Add(c => c.FileProvider, (ITmFileProvider)provider)
            .Add(c => c.EntityId, "entity-1")
            .Add(c => c.AllowUpload, true));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromBinary(new byte[100], "f.txt", contentType: "text/plain"));
        await cut.InvokeAsync(() => { });

        await provider.Received(1).GetForEntityAsync(
            Arg.Is<TmEntityRef>(e => e.EntityId == "entity-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Attachments_DuplicateFile_Rejected()
    {
        var cut = Render<TmActivityAttachments>(p => p
            .Add(c => c.Attachments, SampleAttachments())
            .Add(c => c.AllowUpload, true));

        // photo.png already in the list
        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("ignored", "photo.png", contentType: "image/png"));

        cut.FindAll(".tm-attach-error").Should().NotBeEmpty();
    }

    private static TmAttachment Attachment(string id, string fileName, string contentType, long sizeBytes)
        => new()
        {
            Id = id,
            EntityRef = Entity("entity-1"),
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            CanDownload = true
        };

    private static TmEntityRef Entity(string entityId)
        => TmEntityRef.Create("activity-entity", entityId);
}
