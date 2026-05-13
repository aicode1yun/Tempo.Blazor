using FluentAssertions;
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
}
