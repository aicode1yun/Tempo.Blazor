using FluentAssertions;
using Tempo.Blazor.Demo.Services;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

public class DocumentEditorProviderTests
{
    [Fact]
    public async Task Provider_LoadsDocumentAndRawJsonById()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("doc-1");

        var result = await provider.LoadAsync("doc-1");
        var rawJson = await provider.LoadJsonAsync("doc-1");

        result.Found.Should().BeTrue();
        result.Document!.DocumentId.Should().Be("doc-1");
        result.JsonSnapshot.Should().Contain("\"DocumentId\":\"doc-1\"");
        result.ConcurrencyToken.Should().NotBeNullOrWhiteSpace();
        rawJson.Should().Be(result.JsonSnapshot);
    }

    [Fact]
    public async Task Provider_SavesMaterializedDocumentAndReturnsNewConcurrencyToken()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("doc-1");
        var loaded = await provider.LoadAsync("doc-1");
        loaded.Document!.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Hello" }] }
        });

        var saved = await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = "doc-1",
            Document = loaded.Document,
            BaseConcurrencyToken = loaded.ConcurrencyToken
        });

        saved.Success.Should().BeTrue();
        saved.Conflict.Should().BeFalse();
        saved.ConcurrencyToken.Should().NotBe(loaded.ConcurrencyToken);
        saved.Document!.Blocks.Should().ContainSingle();
    }

    [Fact]
    public async Task Provider_Save_SanitizesDrawingRunUrlsAndPreservesPersistentImagePayload()
    {
        const string dataUrl = "data:image/png;base64,iVBORw0KGgo=";
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("doc-1");
        var loaded = await provider.LoadAsync("doc-1");
        var assetDrawing = CreateProviderDrawing("asset-image", DocumentImageSource.Asset, null, "asset-1", DocumentWrapMode.Square);
        assetDrawing.Url = "blob:https://app.test/asset-display";
        loaded.Document!.Blocks.Add(new DocumentBlock
        {
            Id = "paragraph-1",
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    assetDrawing,
                    CreateProviderDrawing("safe-url-image", DocumentImageSource.Url, "https://cdn.example.test/image.png", null, DocumentWrapMode.Inline),
                    CreateProviderDrawing("data-url-image", DocumentImageSource.Url, dataUrl, null, DocumentWrapMode.Inline),
                    CreateProviderDrawing("blob-url-image", DocumentImageSource.Url, "blob:https://app.test/view-only", null, DocumentWrapMode.TopBottom)
                ]
            }
        });

        var saved = await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = "doc-1",
            Document = loaded.Document,
            BaseConcurrencyToken = loaded.ConcurrencyToken
        });
        var reloaded = await provider.LoadAsync("doc-1");
        var drawings = DocumentImagePersistence.EnumerateDrawingRuns(reloaded.Document).ToArray();

        saved.Success.Should().BeTrue();
        saved.JsonSnapshot.Should().Contain("\"$type\":\"drawing\"");
        saved.JsonSnapshot.Should().NotContain("blob:");
        drawings.Should().HaveCount(4);
        drawings.Single(drawing => drawing.ObjectId == "asset-image").Should().Match<DocumentDrawingRun>(drawing =>
            drawing.AssetId == "asset-1" && drawing.Url == null);
        drawings.Single(drawing => drawing.ObjectId == "safe-url-image").Url.Should().Be("https://cdn.example.test/image.png");
        drawings.Single(drawing => drawing.ObjectId == "data-url-image").Url.Should().Be(dataUrl);
        drawings.Single(drawing => drawing.ObjectId == "blob-url-image").Url.Should().BeNull();
        drawings.Single(drawing => drawing.ObjectId == "asset-image").Caption.Should().Be("asset-image caption");
        drawings.Single(drawing => drawing.ObjectId == "asset-image").AltText.Should().Be("asset-image alt");
        drawings.Single(drawing => drawing.ObjectId == "asset-image").Layout.Anchor.BlockId.Should().Be("paragraph-1");
        drawings.Single(drawing => drawing.ObjectId == "asset-image").Layout.Transform.Width.Should().Be(240);
    }

    [Fact]
    public async Task Provider_Save_ConvertsLegacyImageBlocksToDrawingRunsAtBoundary()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("doc-1");
        var loaded = await provider.LoadAsync("doc-1");
        loaded.Document!.Blocks.Add(new DocumentBlock
        {
            Id = "legacy-image",
            Type = DocumentBlockType.Image,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Url,
                Url = "/favicon.png",
                AltText = "Legacy image",
                Caption = "Legacy caption",
                Size = new DocumentImageSize { Width = 140, Height = 90 },
                Layout = DocumentObjectLayout.Anchored(DocumentWrapMode.Square, DocumentImageHorizontalPosition.Left)
            }
        });

        var saved = await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = "doc-1",
            Document = loaded.Document,
            BaseConcurrencyToken = loaded.ConcurrencyToken
        });
        var reloaded = await provider.LoadAsync("doc-1");

        saved.JsonSnapshot.Should().Contain("\"$type\":\"drawing\"");
        saved.JsonSnapshot.Should().NotContain("\"$type\":\"image\"");
        reloaded.Document!.Blocks.Should().NotContain(block => block.Content is ImageBlockContent);
        var drawing = DocumentImagePersistence.EnumerateDrawingRuns(reloaded.Document).Single();
        drawing.ObjectId.Should().Be("legacy-image");
        drawing.Url.Should().Be("/favicon.png");
        drawing.AltText.Should().Be("Legacy image");
        drawing.Caption.Should().Be("Legacy caption");
        drawing.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
    }

    [Fact]
    public async Task Provider_Save_PreservesImageBlocksWhenCanvasBoundaryRequestsCanonicalBlocks()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("doc-1");
        var loaded = await provider.LoadAsync("doc-1");
        loaded.Document!.Blocks.Add(new DocumentBlock
        {
            Id = "canvas-image",
            Type = DocumentBlockType.Image,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Url,
                Url = "/canvas-image.png",
                AltText = "Canvas image",
                Caption = "Canvas image caption",
                Size = new DocumentImageSize { Width = 180, Height = 120 },
                Layout = DocumentObjectLayout.Inline()
            }
        });

        var saved = await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = "doc-1",
            Document = loaded.Document,
            BaseConcurrencyToken = loaded.ConcurrencyToken,
            PreserveImageBlocks = true
        });
        var reloaded = await provider.LoadAsync("doc-1");

        saved.Success.Should().BeTrue();
        saved.JsonSnapshot.Should().Contain("\"$type\":\"image\"");
        reloaded.Document!.Blocks.Should().ContainSingle(block => block.Content is ImageBlockContent);
        DocumentImagePersistence.EnumerateDrawingRuns(reloaded.Document).Should().BeEmpty();
        var image = reloaded.Document.Blocks.Select(block => block.Content).OfType<ImageBlockContent>().Single();
        image.Url.Should().Be("/canvas-image.png");
        image.AltText.Should().Be("Canvas image");
        image.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Inline);
    }

    [Fact]
    public async Task Provider_SavesNormalizedRawJsonAndRejectsInvalidConcurrencyToken()
    {
        var provider = new InMemoryDocumentEditorProvider();
        var document = provider.SeedEmptyDocument("doc-1");
        var rawJson = DocumentEditorJson.Serialize(document);

        var conflict = await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = "doc-1",
            JsonSnapshot = rawJson,
            BaseConcurrencyToken = "stale-token"
        });

        var loaded = await provider.LoadAsync("doc-1");
        var saved = await provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = "doc-1",
            JsonSnapshot = rawJson,
            BaseConcurrencyToken = loaded.ConcurrencyToken,
            NormalizeJson = true
        });

        conflict.Conflict.Should().BeTrue();
        saved.Success.Should().BeTrue();
        saved.JsonSnapshot.Should().Be(DocumentEditorJson.Normalize(rawJson));
    }

    [Fact]
    public async Task Provider_CreatesVersionsAndLoadsVersionHistory()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedContractDocument("doc-1");

        var version = await provider.CreateVersionAsync(new DocumentVersionCreateRequest
        {
            DocumentId = "doc-1",
            Kind = DocumentVersionKind.Major,
            Description = "Approved",
            Author = new DocumentEditorAuthor { Id = "author-1", DisplayName = "Author" }
        });

        var versions = await provider.GetVersionsAsync("doc-1");

        version.Kind.Should().Be(DocumentVersionKind.Major);
        version.Snapshot.Hash.Should().HaveLength(64);
        versions.Should().ContainSingle(item => item.Id == version.Id);
    }

    [Fact]
    public void Provider_OnlyOfficeParitySeed_UsesDrawingRunsInsteadOfTopLevelImageBlocks()
    {
        var provider = new InMemoryDocumentEditorProvider();

        var document = provider.SeedOnlyOfficeParityDocument();
        var drawingRuns = document.Blocks
            .Select(block => block.Content)
            .OfType<ParagraphBlockContent>()
            .SelectMany(content => content.Inlines)
            .OfType<DocumentDrawingRun>()
            .ToArray();
        var wrapModes = drawingRuns.Select(run => run.Layout.Wrap.Mode).ToArray();

        document.Blocks.Should().NotContain(block => block.Content is ImageBlockContent);
        drawingRuns.Should().Contain(run => run.ObjectId == "recovery-inline-image");
        drawingRuns.Should().Contain(run => run.ObjectId == "recovery-left-wrap-image");
        drawingRuns.Should().Contain(run => run.ObjectId == "recovery-top-bottom-image");
        drawingRuns.Should().Contain(run => run.ObjectId == "onlyoffice-behind-text-image");
        drawingRuns.Should().Contain(run => run.ObjectId == "onlyoffice-front-text-image");
        wrapModes.Should().Contain(DocumentWrapMode.Inline);
        wrapModes.Should().Contain(DocumentWrapMode.Square);
        wrapModes.Should().Contain(DocumentWrapMode.TopBottom);
        wrapModes.Should().Contain(DocumentWrapMode.BehindText);
        wrapModes.Should().Contain(DocumentWrapMode.InFrontOfText);
    }

    [Fact]
    public async Task DemoProvider_SeedDocuments_UseDrawingRunsInsteadOfTopLevelImageBlocks()
    {
        var provider = new DemoDocumentEditorProvider();

        foreach (var documentId in new[] { "contract-demo", "exhibits-demo", "table-demo" })
        {
            var loaded = await provider.LoadAsync(documentId);

            loaded.Found.Should().BeTrue(documentId);
            loaded.Document!.Blocks.Should().NotContain(block => block.Content is ImageBlockContent, documentId);
        }

        var contract = await provider.LoadAsync("contract-demo");
        DocumentImagePersistence.EnumerateDrawingRuns(contract.Document)
            .Should()
            .Contain(run => run.ObjectId == "contract-left-wrap-image")
            .And.Contain(run => run.ObjectId == "contract-top-bottom-image")
            .And.Contain(run => run.ObjectId == "contract-inline-image");
    }

    [Fact]
    public async Task Provider_CreatesLoadsAndResolvesComments()
    {
        var provider = new InMemoryDocumentEditorProvider();
        provider.SeedEmptyDocument("doc-1");

        var created = await provider.CreateCommentAsync("doc-1", new DocumentComment
        {
            Id = "comment-1",
            Anchor = new DocumentCommentAnchor { Type = DocumentCommentAnchorType.Block, BlockId = "block-1" },
            Entries =
            [
                new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor { Id = "author-1", DisplayName = "Author" },
                    Text = "Please review."
                }
            ]
        });

        var comments = await provider.GetCommentsAsync("doc-1");
        var resolved = await provider.ResolveCommentAsync(
            "doc-1",
            created.Id,
            new DocumentEditorAuthor { Id = "author-2", DisplayName = "Reviewer" });

        comments.Should().ContainSingle(item => item.Id == "comment-1");
        resolved.Status.Should().Be(DocumentCommentStatus.Resolved);
        resolved.ResolvedBy!.Id.Should().Be("author-2");
    }

    [Fact]
    public void Suggestion_JsonRoundtrip_PreservesReviewerHashAndOperations()
    {
        var suggestion = new DocumentSuggestion
        {
            Id = "suggestion-1",
            DocumentId = "doc-1",
            Type = DocumentSuggestionType.ReplaceText,
            BaseSnapshotHash = new string('a', 64),
            Reviewer = new DocumentEditorAuthor { Id = "reviewer-1", DisplayName = "Reviewer" },
            ReviewedAt = DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            Operations =
            [
                new DocumentOperation
                {
                    Type = DocumentOperationType.SetBlockAttribute,
                    Target = new DocumentOperationTarget { BlockId = "block-1" },
                    AttributeName = "text",
                    AttributeValueJson = System.Text.Json.JsonSerializer.Serialize("Reviewed text", DocumentEditorJson.Options)
                }
            ]
        };

        var json = System.Text.Json.JsonSerializer.Serialize(suggestion, DocumentEditorJson.Options);
        var roundtrip = System.Text.Json.JsonSerializer.Deserialize<DocumentSuggestion>(json, DocumentEditorJson.Options);

        roundtrip.Should().NotBeNull();
        roundtrip!.BaseSnapshotHash.Should().Be(suggestion.BaseSnapshotHash);
        roundtrip.Reviewer!.Id.Should().Be("reviewer-1");
        roundtrip.ReviewedAt.Should().Be(suggestion.ReviewedAt);
        roundtrip.Operations.Should().ContainSingle(operation =>
            operation.Type == DocumentOperationType.SetBlockAttribute
            && operation.Target.BlockId == "block-1"
            && operation.AttributeName == "text");
    }

    [Fact]
    public async Task SuggestionProvider_CreatesListsAndReviewsPendingSuggestions()
    {
        var provider = new InMemoryDocumentSuggestionProvider();

        var created = await provider.CreateSuggestionAsync(new DocumentSuggestion
        {
            DocumentId = "doc-1",
            Type = DocumentSuggestionType.InsertText,
            SuggestedText = "Inserted text",
            Author = new DocumentEditorAuthor { Id = "author-1", DisplayName = "Author" }
        });
        var pending = await provider.GetSuggestionsAsync(new DocumentSuggestionQuery
        {
            DocumentId = "doc-1",
            Status = DocumentSuggestionStatus.Pending
        });
        var reviewed = await provider.ReviewSuggestionAsync(new DocumentSuggestionReviewRequest
        {
            DocumentId = "doc-1",
            SuggestionId = created.Id,
            Status = DocumentSuggestionStatus.Accepted,
            Reviewer = new DocumentEditorAuthor { Id = "reviewer-1", DisplayName = "Reviewer" }
        });

        pending.Should().ContainSingle(item => item.Id == created.Id);
        reviewed.Status.Should().Be(DocumentSuggestionStatus.Accepted);
        reviewed.Reviewer!.Id.Should().Be("reviewer-1");
        reviewed.ReviewedAt.Should().NotBeNull();
        (await provider.GetSuggestionsAsync(new DocumentSuggestionQuery
        {
            DocumentId = "doc-1",
            Status = DocumentSuggestionStatus.Pending
        })).Should().BeEmpty();
    }

    [Fact]
    public void DocumentFormatProvider_ContractUsesBlazorFreeRequestAndResultModels()
    {
        typeof(IDocumentFormatProvider).GetMethod(nameof(IDocumentFormatProvider.ImportAsync))
            .Should().NotBeNull();
        typeof(IDocumentFormatProvider).GetMethod(nameof(IDocumentFormatProvider.ExportAsync))
            .Should().NotBeNull();

        var publicModelTypes = new[]
        {
            typeof(DocumentFormatImportProviderRequest),
            typeof(DocumentFormatImportProviderResult),
            typeof(DocumentFormatExportProviderRequest),
            typeof(DocumentFormatExportProviderResult),
            typeof(DocumentFormatProviderCapability),
            typeof(DocumentFormatProviderWarning)
        };

        publicModelTypes
            .SelectMany(type => type.GetProperties().Select(property => property.PropertyType))
            .Should()
            .OnlyContain(type => !IsBlazorType(type));
    }

    [Fact]
    public void DocumentFormatProvider_ModelsSupportDocxWarningsImportAndExport()
    {
        var capability = new DocumentFormatProviderCapability
        {
            Format = DocumentFormatProviderKind.Docx,
            CanImport = true,
            CanExport = true,
            FileExtensions = [".docx"]
        };
        var importRequest = new DocumentFormatImportProviderRequest
        {
            DocumentId = "doc-1",
            Format = DocumentFormatProviderKind.Docx,
            FileName = "source.docx",
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            Content = [1, 2, 3]
        };
        var exportRequest = new DocumentFormatExportProviderRequest
        {
            DocumentId = "doc-1",
            Format = DocumentFormatProviderKind.Docx,
            Document = DocumentEditorDocument.Empty(),
            FileName = "target"
        };
        var importResult = new DocumentFormatImportProviderResult
        {
            Document = DocumentEditorDocument.Empty(),
            Warnings =
            [
                new DocumentFormatProviderWarning
                {
                    Code = "docx.warning",
                    Message = "Compatibility warning",
                    Severity = DocumentFormatProviderWarningSeverity.Warning
                }
            ]
        };
        var exportResult = new DocumentFormatExportProviderResult
        {
            Content = [4, 5, 6],
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileName = "target.docx",
            Warnings = importResult.Warnings
        };

        capability.Format.Should().Be(DocumentFormatProviderKind.Docx);
        capability.CanImport.Should().BeTrue();
        capability.CanExport.Should().BeTrue();
        importRequest.Content.Should().Equal(1, 2, 3);
        exportRequest.Document.Should().NotBeNull();
        importResult.Warnings.Should().ContainSingle(warning => warning.Code == "docx.warning");
        exportResult.FileName.Should().EndWith(".docx");
    }

    [Fact]
    public void DocumentPdfExportRequest_IncludesOptionsForSuggestionsCommentsAndPageSetup()
    {
        var request = new DocumentPdfExportRequest
        {
            DocumentId = "doc-1",
            Document = DocumentEditorDocument.Empty("doc-1"),
            FileName = "contract",
            Options = new DocumentPdfExportOptions
            {
                IncludeSuggestions = false,
                IncludeComments = false,
                PageSetup = new DocumentPdfPageSetupOptions
                {
                    PageSize = DocumentPageSize.Letter,
                    Orientation = DocumentPdfPageOrientation.Landscape,
                    Margins = new DocumentPageMargins
                    {
                        Top = 36,
                        Right = 48,
                        Bottom = 36,
                        Left = 48
                    }
                }
            }
        };

        request.Options.IncludeSuggestions.Should().BeFalse();
        request.Options.IncludeComments.Should().BeFalse();
        request.Options.PageSetup.PageSize.Name.Should().Be("Letter");
        request.Options.PageSetup.Orientation.Should().Be(DocumentPdfPageOrientation.Landscape);
        request.Options.PageSetup.Margins.Left.Should().Be(48);
    }

    [Fact]
    public void DocumentComparisonProvider_ContractUsesCompareRequestAndResult()
    {
        typeof(IDocumentComparisonProvider).GetMethod(nameof(IDocumentComparisonProvider.CompareAsync))
            .Should().NotBeNull();

        var request = new DocumentCompareRequest
        {
            DocumentId = "doc-1",
            BaseSource = new DocumentCompareSource
            {
                Kind = DocumentCompareSourceKind.Current,
                Document = DocumentEditorDocument.Empty("doc-1")
            },
            CompareSource = new DocumentCompareSource
            {
                Kind = DocumentCompareSourceKind.DocumentId,
                DocumentId = "doc-2"
            },
            CurrentDocument = DocumentEditorDocument.Empty("doc-1")
        };
        var uploadSource = new DocumentCompareSource
        {
            Kind = DocumentCompareSourceKind.JsonSnapshot,
            JsonSnapshot = DocumentEditorJson.Serialize(DocumentEditorDocument.Empty("doc-upload")),
            Label = "upload.docx"
        };
        var result = new DocumentCompareResult
        {
            Summary = new DocumentCompareSummary
            {
                AddedBlocks = 1,
                RemovedBlocks = 2,
                ChangedBlocks = 3
            }
        };

        request.BaseSource.Kind.Should().Be(DocumentCompareSourceKind.Current);
        request.CompareSource.Kind.Should().Be(DocumentCompareSourceKind.DocumentId);
        uploadSource.Kind.Should().Be(DocumentCompareSourceKind.JsonSnapshot);
        result.Summary.AddedBlocks.Should().Be(1);
        result.Summary.RemovedBlocks.Should().Be(2);
        result.Summary.ChangedBlocks.Should().Be(3);
        result.Summary.HasChanges.Should().BeTrue();
    }

    [Fact]
    public async Task AuditSink_RecordsEvents()
    {
        IDocumentAuditSink provider = new InMemoryDocumentEditorProvider();

        await provider.RecordAsync(new DocumentEditorAuditEvent
        {
            DocumentId = "doc-1",
            Action = DocumentEditorAuditAction.Open
        });

        ((InMemoryDocumentEditorProvider)provider).AuditEvents.Should().ContainSingle(item =>
            item.Action == DocumentEditorAuditAction.Open);
    }

    private static bool IsBlazorType(Type type)
    {
        var candidates = new Queue<Type>();
        candidates.Enqueue(type);

        while (candidates.Count > 0)
        {
            var current = candidates.Dequeue();
            if (current.Namespace?.StartsWith("Microsoft.AspNetCore.Components", StringComparison.Ordinal) == true)
            {
                return true;
            }

            if (current.IsArray)
            {
                candidates.Enqueue(current.GetElementType()!);
            }

            if (current.IsGenericType)
            {
                foreach (var argument in current.GetGenericArguments())
                {
                    candidates.Enqueue(argument);
                }
            }
        }

        return false;
    }

    private static DocumentDrawingRun CreateProviderDrawing(
        string objectId,
        DocumentImageSource source,
        string? url,
        string? assetId,
        DocumentWrapMode wrapMode)
        => new()
        {
            Id = $"{objectId}-inline",
            ObjectId = objectId,
            Source = source,
            Url = url,
            AssetId = assetId,
            AltText = $"{objectId} alt",
            Caption = $"{objectId} caption",
            Size = new DocumentImageSize { Width = 240, Height = 120 },
            NaturalSize = new DocumentImageSize { Width = 480, Height = 240 },
            Layout = new DocumentObjectLayout
            {
                Kind = wrapMode == DocumentWrapMode.Inline ? DocumentObjectLayoutKind.Inline : DocumentObjectLayoutKind.Anchored,
                Anchor = new DocumentObjectAnchor
                {
                    BlockId = "paragraph-1",
                    Offset = 3,
                    MoveWithText = true
                },
                Wrap = new DocumentObjectWrap
                {
                    Mode = wrapMode,
                    DistanceLeft = 8,
                    DistanceRight = 10
                },
                Transform = new DocumentObjectTransform
                {
                    Width = 240,
                    Height = 120,
                    NaturalWidth = 480,
                    NaturalHeight = 240
                }
            }
        };
}
