using System.Security.Cryptography;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>In-memory document editor provider intended for tests and demos.</summary>
public class InMemoryDocumentEditorProvider : IDocumentEditorProvider, IDocumentAuditSink
{
    /// <summary>Stable document id for the 2026-05-23 Google Docs engine recovery baseline.</summary>
    public const string Recovery20260523DocumentId = "recovery-2026-05-23";

    /// <summary>Stable document id for the 2026-05-24 ONLYOFFICE parity baseline.</summary>
    public const string OnlyOfficeParity20260524DocumentId = "onlyoffice-parity-2026-05-24";

    private const string RecoverySectionId = "recovery-section-main";
    private const string RecoveryUrlImageUrl = "/document-editor-evidence.svg";
    private const string RecoveryProviderAssetId = "contract-evidence-asset";
    private readonly Dictionary<string, StoredDocument> _documents = [];
    private readonly Dictionary<string, List<DocumentVersion>> _versions = [];
    private readonly List<DocumentEditorAuditEvent> _auditEvents = [];

    /// <summary>Recorded audit events.</summary>
    public IReadOnlyList<DocumentEditorAuditEvent> AuditEvents => _auditEvents;

    /// <summary>Clears all in-memory documents, versions, and audit events.</summary>
    protected void ClearStore()
    {
        _documents.Clear();
        _versions.Clear();
        _auditEvents.Clear();
    }

    /// <summary>Seeds a new empty document.</summary>
    public DocumentEditorDocument SeedEmptyDocument(string documentId = "empty-document")
    {
        var document = DocumentEditorDocument.Empty(documentId);
        document.Metadata.Title = "Empty document";
        StoreDocument(document);
        return Clone(document);
    }

    /// <summary>Seeds a representative contract document.</summary>
    public DocumentEditorDocument SeedContractDocument(string documentId = "contract-demo")
    {
        var document = DocumentEditorDocument.Empty(documentId);
        document.Metadata.Title = "Service agreement";
        document.Theme = new DocumentEditorTheme
        {
            BodyFontFamily = "Aptos, Arial, sans-serif",
            BodyFontSize = 11.5,
            BodyLineHeight = 1.25,
            ParagraphSpacingAfter = 9
        };
        document.Sections[0].Id = "contract-section-main";
        document.Sections[0].Title = "Agreement";
        document.Sections[0].Properties.HeaderFooterReferences =
        [
            new DocumentHeaderFooterReference
            {
                HeaderFooterId = "contract-header-primary",
                Type = DocumentHeaderFooterType.Header,
                Scope = DocumentHeaderFooterScope.Primary
            },
            new DocumentHeaderFooterReference
            {
                HeaderFooterId = "contract-footer-primary",
                Type = DocumentHeaderFooterType.Footer,
                Scope = DocumentHeaderFooterScope.Primary
            }
        ];
        document.Blocks.Add(new DocumentBlock
        {
            Id = "contract-heading",
            SectionId = "contract-section-main",
            Type = DocumentBlockType.Heading,
            Order = 10,
            Content = new HeadingBlockContent
            {
                Level = 1,
                Inlines =
                [
                    new TextRun
                    {
                        Id = "contract-heading-text",
                        Text = "Service agreement",
                        Marks =
                        [
                            new InlineMark { Type = InlineMarkType.FontFamily, Value = "Aptos Display, Aptos, Arial, sans-serif" },
                            new InlineMark { Type = InlineMarkType.FontSize, Value = "24pt" }
                        ]
                    }
                ]
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Id = "contract-intro",
            SectionId = "contract-section-main",
            Type = DocumentBlockType.Paragraph,
            Order = 20,
            ParagraphProperties = new DocumentParagraphProperties
            {
                Alignment = DocumentTextAlignment.Justify,
                LineSpacing = 1.25,
                SpacingAfter = 10
            },
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun
                    {
                        Id = "contract-intro-prefix",
                        Text = "This agreement is made with ",
                        Marks = [new InlineMark { Type = InlineMarkType.Bold }]
                    },
                    new TokenRun
                    {
                        Id = "contract-client-token",
                        Key = "client.name",
                        DisplayName = "Client name",
                        TokenType = "text",
                        TypeLabel = "CRM",
                        FallbackText = "Acme s.r.o."
                    },
                    new TextRun { Id = "contract-intro-suffix", Text = "." }
                ]
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Id = "contract-scope",
            SectionId = "contract-section-main",
            Type = DocumentBlockType.Paragraph,
            Order = 25,
            ParagraphProperties = new DocumentParagraphProperties
            {
                Alignment = DocumentTextAlignment.Left,
                LineSpacing = 1.25,
                SpacingAfter = 12
            },
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun
                    {
                        Id = "contract-scope-approved",
                        Text = "The provider will deliver implementation, training, and documentation services.",
                        Marks = [new InlineMark { Type = InlineMarkType.TextColor, Value = "#1f2937" }]
                    },
                    new TextRun
                    {
                        Id = "contract-scope-revision",
                        Text = " Priority support is included during the first thirty days.",
                        Marks =
                        [
                            new InlineMark
                            {
                                Type = InlineMarkType.Revision,
                                RevisionId = "contract-revision-scope",
                                Value = "Insertion"
                            }
                        ]
                    }
                ]
            }
        });
        document.HeadersFooters.Add(CreateSeedHeaderFooter(
            "contract-header-primary",
            DocumentHeaderFooterType.Header,
            "Tempo Legal - Service agreement"));
        document.HeadersFooters.Add(CreateSeedHeaderFooter(
            "contract-footer-primary",
            DocumentHeaderFooterType.Footer,
            "Confidential - Page 1"));
        document.Revisions.Add(new DocumentRevision
        {
            Id = "contract-revision-scope",
            Type = DocumentRevisionType.Insertion,
            Range = new DocumentRevisionRange
            {
                BlockId = "contract-scope",
                StartInlineIndex = 1,
                EndInlineIndex = 1,
                StartOffset = 0,
                EndOffset = 60
            },
            Author = new DocumentRevisionAuthor
            {
                Id = "demo-reviewer",
                DisplayName = "Demo Reviewer",
                Email = "reviewer@example.local"
            },
            CreatedAt = new DateTimeOffset(2026, 5, 14, 8, 30, 0, TimeSpan.Zero),
            Action = DocumentRevisionAction.Pending,
            PayloadJson = "Priority support is included during the first thirty days."
        });

        StoreDocument(document);
        return Clone(document);
    }

    /// <summary>Seeds the deterministic recovery document used by Google Docs engine regression E2E tests.</summary>
    public DocumentEditorDocument SeedRecoveryDocument(string documentId = Recovery20260523DocumentId)
    {
        var document = DocumentEditorDocument.Empty(documentId);
        document.Metadata.Title = "Google Docs engine recovery baseline";
        document.Metadata.Description = "Deterministic baseline for the 2026-05-23 document editor recovery plan.";
        document.Metadata.Status = DocumentEditorStatus.Review;
        document.Theme = new DocumentEditorTheme
        {
            BodyFontFamily = "Aptos, Arial, sans-serif",
            BodyFontSize = 11,
            BodyLineHeight = 1.25,
            ParagraphSpacingAfter = 9
        };
        document.Sections[0].Id = RecoverySectionId;
        document.Sections[0].Title = "Recovery";
        document.Sections[0].Properties.HeaderFooterReferences =
        [
            new DocumentHeaderFooterReference
            {
                HeaderFooterId = "recovery-header-primary",
                Type = DocumentHeaderFooterType.Header,
                Scope = DocumentHeaderFooterScope.Primary
            },
            new DocumentHeaderFooterReference
            {
                HeaderFooterId = "recovery-footer-primary",
                Type = DocumentHeaderFooterType.Footer,
                Scope = DocumentHeaderFooterScope.Primary
            }
        ];
        document.HeadersFooters.Add(CreateRecoveryHeader());
        document.HeadersFooters.Add(CreateRecoveryFooter());

        document.Blocks.Add(new DocumentBlock
        {
            Id = "recovery-heading",
            SectionId = RecoverySectionId,
            Type = DocumentBlockType.Heading,
            Order = 10,
            Content = new HeadingBlockContent
            {
                Level = 1,
                Inlines = [new TextRun { Id = "recovery-heading-text", Text = "Recovery baseline" }]
            }
        });
        document.Blocks.Add(CreateParagraph(
            "recovery-comment-paragraph",
            20,
            [
                new TextRun { Id = "recovery-comment-prefix", Text = "This paragraph contains a " },
                new TextRun
                {
                    Id = "recovery-comment-anchor-run",
                    Text = "visible comment anchor",
                    Marks =
                    [
                        new InlineMark
                        {
                            Type = InlineMarkType.CommentAnchor,
                            CommentAnchor = new CommentAnchorMarkData
                            {
                                CommentId = "recovery-comment-visible",
                                AnchorId = "recovery-comment-visible-anchor"
                            }
                        }
                    ]
                },
                new TextRun { Id = "recovery-comment-suffix", Text = " for marker checks." }
            ]));
        document.Blocks.Add(CreateParagraph(
            "recovery-insertion-revision-paragraph",
            30,
            [
                new TextRun { Id = "recovery-insertion-prefix", Text = "Pending inserted text should be decorated: " },
                new TextRun
                {
                    Id = "recovery-insertion-run",
                    Text = "inserted recovery clause",
                    Marks =
                    [
                        new InlineMark
                        {
                            Type = InlineMarkType.Revision,
                            RevisionId = "recovery-revision-insertion",
                            Value = "Insertion"
                        }
                    ]
                },
                new TextRun { Id = "recovery-insertion-suffix", Text = "." }
            ]));
        document.Blocks.Add(CreateParagraph(
            "recovery-deletion-revision-paragraph",
            40,
            [
                new TextRun { Id = "recovery-deletion-prefix", Text = "Pending deleted text should remain visible in all markup: " },
                new TextRun
                {
                    Id = "recovery-deletion-run",
                    Text = "deleted recovery clause",
                    Marks =
                    [
                        new InlineMark
                        {
                            Type = InlineMarkType.Revision,
                            RevisionId = "recovery-revision-deletion",
                            Value = "Deletion"
                        }
                    ]
                },
                new TextRun { Id = "recovery-deletion-suffix", Text = "." }
            ]));
        document.Blocks.Add(CreateParagraph(
            "recovery-selection-paragraph",
            50,
            "Select this inline recovery text with the mouse to verify that the floating toolbar appears and stays anchored near the selection."));
        document.Blocks.Add(CreateImage(
            "recovery-url-image",
            60,
            DocumentImageSource.Url,
            RecoveryUrlImageUrl,
            null,
            "URL recovery evidence",
            "URL image for recovery baseline",
            156,
            88,
            DocumentImageAlignment.Center,
            DocumentObjectLayout.Inline()));
        document.Blocks.Add(CreateImage(
            "recovery-provider-image",
            70,
            DocumentImageSource.Asset,
            null,
            RecoveryProviderAssetId,
            "Provider recovery evidence",
            "Provider image for recovery baseline",
            156,
            88,
            DocumentImageAlignment.Center,
            DocumentObjectLayout.Inline()));
        document.Blocks.Add(CreateImage(
            "recovery-inline-image",
            80,
            DocumentImageSource.Url,
            RecoveryUrlImageUrl,
            null,
            "Inline recovery image",
            "Inline image for recovery baseline",
            140,
            79,
            DocumentImageAlignment.Center,
            DocumentObjectLayout.Inline()));
        document.Blocks.Add(CreateImage(
            "recovery-left-wrap-image",
            90,
            DocumentImageSource.Url,
            RecoveryUrlImageUrl,
            null,
            "Left wrapped recovery image",
            "Left wrapped image for recovery baseline",
            148,
            84,
            DocumentImageAlignment.Start,
            CreateRecoveryWrappedImageLayout(148, 84, DocumentImageHorizontalPosition.Left, "recovery-left-wrap-text")));
        document.Blocks.Add(CreateParagraph(
            "recovery-left-wrap-text",
            91,
            "This text belongs below the left wrapped image and is long enough to create several visual lines beside the object. It should be readable, selectable, and free from image overlap."));
        document.Blocks.Add(CreateImage(
            "recovery-right-wrap-image",
            100,
            DocumentImageSource.Url,
            RecoveryUrlImageUrl,
            null,
            "Right wrapped recovery image",
            "Right wrapped image for recovery baseline",
            148,
            84,
            DocumentImageAlignment.End,
            CreateRecoveryWrappedImageLayout(148, 84, DocumentImageHorizontalPosition.Right, "recovery-right-wrap-text")));
        document.Blocks.Add(CreateParagraph(
            "recovery-right-wrap-text",
            101,
            "This text belongs below the right wrapped image and verifies that opposite image positioning still leaves clear selectable text lines for human interaction."));
        document.Blocks.Add(CreateImage(
            "recovery-top-bottom-image",
            110,
            DocumentImageSource.Asset,
            null,
            RecoveryProviderAssetId,
            "Top bottom recovery image",
            "Top-bottom image for recovery baseline",
            220,
            124,
            DocumentImageAlignment.Center,
            CreateRecoveryTopBottomImageLayout(220, 124, "recovery-top-bottom-text")));
        document.Blocks.Add(CreateParagraph(
            "recovery-top-bottom-text",
            111,
            "Top-bottom wrapping reserves a full horizontal band before this text continues."));
        document.Blocks.Add(CreateImage(
            "recovery-missing-alt-image",
            120,
            DocumentImageSource.Url,
            RecoveryUrlImageUrl,
            null,
            null,
            "Missing alt text image for recovery baseline",
            156,
            88,
            DocumentImageAlignment.Center,
            DocumentObjectLayout.Inline()));
        document.Blocks.Add(CreateRecoveryTable());

        document.Comments.Add(new DocumentComment
        {
            Id = "recovery-comment-visible",
            Anchor = new DocumentCommentAnchor
            {
                Type = DocumentCommentAnchorType.TextRange,
                BlockId = "recovery-comment-paragraph",
                StartInlineIndex = 1,
                EndInlineIndex = 1,
                StartOffset = 0,
                EndOffset = "visible comment anchor".Length,
                ExternalAnchorId = "recovery-comment-visible-anchor"
            },
            Visibility = DocumentCommentVisibility.Internal,
            Entries =
            [
                new DocumentCommentEntry
                {
                    Id = "recovery-comment-visible-entry",
                    Author = new DocumentEditorAuthor { Id = "recovery-reviewer", DisplayName = "Recovery Reviewer" },
                    Text = "The recovery baseline must show this comment in the document.",
                    CreatedAt = new DateTimeOffset(2026, 5, 23, 8, 0, 0, TimeSpan.Zero)
                }
            ]
        });
        document.Revisions.Add(CreateRecoveryRevision(
            "recovery-revision-insertion",
            DocumentRevisionType.Insertion,
            "recovery-insertion-revision-paragraph",
            1,
            "inserted recovery clause",
            0));
        document.Revisions.Add(CreateRecoveryRevision(
            "recovery-revision-deletion",
            DocumentRevisionType.Deletion,
            "recovery-deletion-revision-paragraph",
            1,
            "deleted recovery clause",
            5));

        StoreDocument(document, "recovery-2026-05-23-canonical-v1");
        return Clone(document);
    }

    /// <summary>Seeds the deterministic ONLYOFFICE parity baseline used by P0 editor engine E2E tests.</summary>
    public DocumentEditorDocument SeedOnlyOfficeParityDocument(string documentId = OnlyOfficeParity20260524DocumentId)
    {
        var document = SeedRecoveryDocument(documentId);
        document.Metadata.Title = "ONLYOFFICE parity baseline";
        document.Metadata.Description = "Deterministic baseline for selection, formatting, track changes, comments, and undo parity tests.";

        document.Blocks.Add(CreateParagraph(
            "onlyoffice-formatting-paragraph",
            52,
            "Apply formatting to this exact target phrase and keep the surrounding text unchanged.",
            spacingAfter: 9));

        document.Blocks.Add(CreateParagraph(
            "onlyoffice-mixed-formatting-paragraph",
            53,
            [
                new TextRun
                {
                    Id = "onlyoffice-mixed-bold-run",
                    Text = "Bold mixed segment",
                    Marks = [new InlineMark { Type = InlineMarkType.Bold }]
                },
                new TextRun { Id = "onlyoffice-mixed-plain-run", Text = " and plain mixed segment." }
            ],
            spacingAfter: 9));

        document.Blocks.Add(CreateParagraph(
            "onlyoffice-collapsed-caret-paragraph",
            54,
            "Collapsed caret typing style starts here.",
            spacingAfter: 9));

        document.Blocks.Add(CreateParagraph(
            "onlyoffice-track-changes-paragraph",
            55,
            "Track changes typing target.",
            spacingAfter: 9));

        document.Blocks.Add(CreateParagraph(
            "onlyoffice-comment-boundary-paragraph",
            56,
            [
                new TextRun { Id = "onlyoffice-comment-boundary-prefix", Text = "Text before " },
                new TextRun
                {
                    Id = "onlyoffice-comment-boundary-anchor",
                    Text = "commented range",
                    Marks =
                    [
                        new InlineMark
                        {
                            Type = InlineMarkType.CommentAnchor,
                            CommentAnchor = new CommentAnchorMarkData
                            {
                                CommentId = "onlyoffice-comment-boundary",
                                AnchorId = "onlyoffice-comment-boundary-anchor"
                            }
                        }
                    ]
                },
                new TextRun { Id = "onlyoffice-comment-boundary-suffix", Text = " and editable suffix." }
            ],
            spacingAfter: 9));

        document.Comments.Add(new DocumentComment
        {
            Id = "onlyoffice-comment-boundary",
            Anchor = new DocumentCommentAnchor
            {
                Type = DocumentCommentAnchorType.TextRange,
                BlockId = "onlyoffice-comment-boundary-paragraph",
                StartInlineIndex = 1,
                EndInlineIndex = 1,
                StartOffset = 0,
                EndOffset = "commented range".Length,
                ExternalAnchorId = "onlyoffice-comment-boundary-anchor"
            },
            Visibility = DocumentCommentVisibility.Internal,
            Entries =
            [
                new DocumentCommentEntry
                {
                    Id = "onlyoffice-comment-boundary-entry",
                    Author = new DocumentEditorAuthor { Id = "onlyoffice-reviewer", DisplayName = "ONLYOFFICE Reviewer" },
                    Text = "Typing after this comment must not extend the comment range.",
                    CreatedAt = new DateTimeOffset(2026, 5, 24, 8, 0, 0, TimeSpan.Zero)
                }
            ]
        });

        StoreDocument(document, "onlyoffice-parity-2026-05-24-canonical-v1");
        return Clone(document);
    }

    /// <summary>Seeds a simple court filing document.</summary>
    public DocumentEditorDocument SeedFilingDocument(string documentId = "filing-demo")
    {
        var document = DocumentEditorDocument.Empty(documentId);
        document.Metadata.Title = "Court filing";
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Heading,
            Order = 10,
            Content = new HeadingBlockContent
            {
                Level = 1,
                Inlines = [new TextRun { Text = "Court filing" }]
            }
        });
        document.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Order = 20,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = "The claimant submits the following petition." }]
            }
        });

        StoreDocument(document);
        return Clone(document);
    }

    /// <inheritdoc />
    public virtual Task<DocumentEditorLoadResult> LoadAsync(
        string documentId,
        DocumentEditorLoadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DocumentEditorLoadOptions();
        if (!_documents.TryGetValue(documentId, out var stored))
        {
            return Task.FromResult(DocumentEditorLoadResult.NotFound());
        }

        return Task.FromResult(new DocumentEditorLoadResult
        {
            Found = true,
            Document = options.IncludeDocument ? Clone(stored.Document) : null,
            JsonSnapshot = options.IncludeJson ? stored.Json : null,
            ConcurrencyToken = stored.ConcurrencyToken
        });
    }

    /// <inheritdoc />
    public Task<string?> LoadJsonAsync(string documentId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_documents.TryGetValue(documentId, out var stored) ? stored.Json : null);
    }

    /// <inheritdoc />
    public virtual Task<DocumentEditorSaveResult> SaveAsync(
        DocumentEditorSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_documents.TryGetValue(request.DocumentId, out var stored)
            && request.ConcurrencyMode == DocumentEditorConcurrencyMode.Required
            && stored.ConcurrencyToken != request.BaseConcurrencyToken)
        {
            return Task.FromResult(DocumentEditorSaveResult.ConcurrencyConflict(stored.ConcurrencyToken));
        }

        if (_documents.TryGetValue(request.DocumentId, out stored)
            && request.ConcurrencyMode == DocumentEditorConcurrencyMode.Optional
            && request.BaseConcurrencyToken is not null
            && stored.ConcurrencyToken != request.BaseConcurrencyToken)
        {
            return Task.FromResult(DocumentEditorSaveResult.ConcurrencyConflict(stored.ConcurrencyToken));
        }

        var document = request.Document ?? DocumentEditorJson.Deserialize(request.JsonSnapshot ?? string.Empty);
        document.DocumentId = request.DocumentId;
        document.Metadata.ModifiedAt = DateTimeOffset.UtcNow;

        var json = request.Document is not null
            ? DocumentEditorJson.Serialize(document)
            : request.NormalizeJson ? DocumentEditorJson.Normalize(request.JsonSnapshot!) : request.JsonSnapshot!;

        var savedDocument = request.Document is not null ? Clone(document) : DocumentEditorJson.Deserialize(json);
        var concurrencyToken = CreateConcurrencyToken();
        _documents[request.DocumentId] = new StoredDocument(savedDocument, json, concurrencyToken);

        return Task.FromResult(DocumentEditorSaveResult.Saved(Clone(savedDocument), json, concurrencyToken));
    }

    /// <inheritdoc />
    public virtual Task<DocumentVersion> CreateVersionAsync(
        DocumentVersionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(request.DocumentId, out var stored))
        {
            throw new KeyNotFoundException($"Document '{request.DocumentId}' was not found.");
        }

        var snapshot = new DocumentVersionSnapshot
        {
            DocumentId = request.DocumentId,
            SchemaVersion = stored.Document.SchemaVersion,
            Json = stored.Json
        };
        snapshot.Hash = DocumentVersionHashHelper.ComputeSnapshotHash(snapshot);

        var version = new DocumentVersion
        {
            DocumentId = request.DocumentId,
            Kind = request.Kind,
            Label = request.Label,
            Description = request.Description,
            Author = request.Author,
            Snapshot = snapshot
        };

        if (!_versions.TryGetValue(request.DocumentId, out var versions))
        {
            versions = [];
            _versions[request.DocumentId] = versions;
        }

        versions.Add(Clone(version));
        return Task.FromResult(Clone(version));
    }

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<DocumentVersion>> GetVersionsAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var versions = _versions.TryGetValue(documentId, out var stored)
            ? stored.Select(Clone).ToList()
            : [];

        return Task.FromResult<IReadOnlyList<DocumentVersion>>(versions);
    }

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<DocumentComment>> GetCommentsAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var comments = _documents.TryGetValue(documentId, out var stored)
            ? stored.Document.Comments.Select(Clone).ToList()
            : [];

        return Task.FromResult<IReadOnlyList<DocumentComment>>(comments);
    }

    /// <inheritdoc />
    public virtual Task<DocumentComment> CreateCommentAsync(
        string documentId,
        DocumentComment comment,
        CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(documentId, out var stored))
        {
            throw new KeyNotFoundException($"Document '{documentId}' was not found.");
        }

        var storedComment = NormalizeComment(Clone(comment));
        stored.Document.Comments.Add(storedComment);
        StoreDocument(stored.Document, stored.ConcurrencyToken);
        return Task.FromResult(Clone(storedComment));
    }

    /// <inheritdoc />
    public virtual Task<DocumentComment> AddCommentReplyAsync(
        string documentId,
        string commentId,
        DocumentCommentEntry entry,
        CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(documentId, out var stored))
        {
            throw new KeyNotFoundException($"Document '{documentId}' was not found.");
        }

        var comment = stored.Document.Comments.First(item => item.Id == commentId);
        var storedEntry = NormalizeCommentEntry(Clone(entry));
        comment.Entries.Add(storedEntry);
        StoreDocument(stored.Document, stored.ConcurrencyToken);
        return Task.FromResult(Clone(comment));
    }

    /// <inheritdoc />
    public virtual Task<DocumentComment> UpdateCommentEntryAsync(
        string documentId,
        string commentId,
        string entryId,
        string text,
        DocumentEditorAuthor updatedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(documentId, out var stored))
        {
            throw new KeyNotFoundException($"Document '{documentId}' was not found.");
        }

        var comment = stored.Document.Comments.First(item => item.Id == commentId);
        var entry = comment.Entries.First(item => item.Id == entryId);
        entry.Text = text.Trim();
        entry.ModifiedAt = DateTimeOffset.UtcNow;
        StoreDocument(stored.Document, stored.ConcurrencyToken);
        return Task.FromResult(Clone(comment));
    }

    /// <inheritdoc />
    public virtual Task<DocumentComment> ResolveCommentAsync(
        string documentId,
        string commentId,
        DocumentEditorAuthor resolvedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(documentId, out var stored))
        {
            throw new KeyNotFoundException($"Document '{documentId}' was not found.");
        }

        var comment = stored.Document.Comments.First(item => item.Id == commentId);
        comment.Status = DocumentCommentStatus.Resolved;
        comment.ResolvedAt = DateTimeOffset.UtcNow;
        comment.ResolvedBy = resolvedBy;
        StoreDocument(stored.Document, stored.ConcurrencyToken);
        return Task.FromResult(Clone(comment));
    }

    /// <inheritdoc />
    public virtual Task<DocumentComment> ReopenCommentAsync(
        string documentId,
        string commentId,
        DocumentEditorAuthor reopenedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(documentId, out var stored))
        {
            throw new KeyNotFoundException($"Document '{documentId}' was not found.");
        }

        var comment = stored.Document.Comments.First(item => item.Id == commentId);
        comment.Status = DocumentCommentStatus.Open;
        comment.ResolvedAt = null;
        comment.ResolvedBy = null;
        StoreDocument(stored.Document, stored.ConcurrencyToken);
        return Task.FromResult(Clone(comment));
    }

    /// <inheritdoc />
    public virtual Task DeleteCommentAsync(
        string documentId,
        string commentId,
        DocumentEditorAuthor deletedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_documents.TryGetValue(documentId, out var stored))
        {
            throw new KeyNotFoundException($"Document '{documentId}' was not found.");
        }

        stored.Document.Comments.RemoveAll(item => item.Id == commentId);
        StoreDocument(stored.Document, stored.ConcurrencyToken);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordAsync(DocumentEditorAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        _auditEvents.Add(Clone(auditEvent));
        return Task.CompletedTask;
    }

    /// <summary>Stores a document snapshot in memory.</summary>
    protected void StoreDocument(DocumentEditorDocument document, string? concurrencyToken = null)
    {
        var clone = Clone(document);
        _documents[clone.DocumentId] = new StoredDocument(
            clone,
            DocumentEditorJson.Serialize(clone),
            concurrencyToken ?? CreateConcurrencyToken());
    }

    /// <summary>Stores a prepared document version in memory.</summary>
    protected void StoreVersion(DocumentVersion version)
    {
        if (!_versions.TryGetValue(version.DocumentId, out var versions))
        {
            versions = [];
            _versions[version.DocumentId] = versions;
        }

        versions.Add(Clone(version));
    }

    private static string CreateConcurrencyToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    }

    private static DocumentComment NormalizeComment(DocumentComment comment)
    {
        if (string.IsNullOrWhiteSpace(comment.Id))
        {
            comment.Id = Guid.NewGuid().ToString("N");
        }

        foreach (var entry in comment.Entries)
        {
            NormalizeCommentEntry(entry);
        }

        return comment;
    }

    private static DocumentCommentEntry NormalizeCommentEntry(DocumentCommentEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Id))
        {
            entry.Id = Guid.NewGuid().ToString("N");
        }

        if (entry.CreatedAt == default)
        {
            entry.CreatedAt = DateTimeOffset.UtcNow;
        }

        return entry;
    }

    private static DocumentHeaderFooter CreateSeedHeaderFooter(string id, DocumentHeaderFooterType type, string text)
    {
        return new DocumentHeaderFooter
        {
            Id = id,
            Type = type,
            Scope = DocumentHeaderFooterScope.Primary,
            SectionId = "contract-section-main",
            Blocks =
            [
                new DocumentBlock
                {
                    Id = $"{id}-block",
                    SectionId = "contract-section-main",
                    Type = DocumentBlockType.Paragraph,
                    Content = new ParagraphBlockContent
                    {
                        Inlines =
                        [
                            new TextRun
                            {
                                Id = $"{id}-text",
                                Text = text,
                                Marks = [new InlineMark { Type = InlineMarkType.FontSize, Value = "9pt" }]
                            }
                        ]
                    }
                }
            ]
        };
    }

    private static DocumentHeaderFooter CreateRecoveryHeader()
        => new()
        {
            Id = "recovery-header-primary",
            Type = DocumentHeaderFooterType.Header,
            Scope = DocumentHeaderFooterScope.Primary,
            SectionId = RecoverySectionId,
            Blocks =
            [
                new DocumentBlock
                {
                    Id = "recovery-header-primary-block",
                    SectionId = RecoverySectionId,
                    Type = DocumentBlockType.Paragraph,
                    Content = new ParagraphBlockContent
                    {
                        Inlines =
                        [
                            new TextRun
                            {
                                Id = "recovery-header-primary-text",
                                Text = "Recovery Primary Header",
                                Marks = [new InlineMark { Type = InlineMarkType.FontSize, Value = "9pt" }]
                            }
                        ]
                    }
                }
            ]
        };

    private static DocumentHeaderFooter CreateRecoveryFooter()
        => new()
        {
            Id = "recovery-footer-primary",
            Type = DocumentHeaderFooterType.Footer,
            Scope = DocumentHeaderFooterScope.Primary,
            SectionId = RecoverySectionId,
            Blocks =
            [
                new DocumentBlock
                {
                    Id = "recovery-footer-primary-block",
                    SectionId = RecoverySectionId,
                    Type = DocumentBlockType.Paragraph,
                    Content = new ParagraphBlockContent
                    {
                        Inlines =
                        [
                            new TextRun
                            {
                                Id = "recovery-footer-primary-prefix",
                                Text = "Recovery Primary Footer - Page ",
                                Marks = [new InlineMark { Type = InlineMarkType.FontSize, Value = "9pt" }]
                            },
                            new DocumentFieldRun
                            {
                                Id = "recovery-footer-primary-page-number",
                                FieldType = DocumentFieldType.PageNumber,
                                FallbackText = "1",
                                DisplayText = "1",
                                Marks = [new InlineMark { Type = InlineMarkType.FontSize, Value = "9pt" }]
                            }
                        ]
                    }
                }
            ]
        };

    private static DocumentBlock CreateParagraph(string id, double order, string text, double spacingAfter = 10)
        => CreateParagraph(
            id,
            order,
            [new TextRun { Id = $"{id}-text", Text = text }],
            spacingAfter);

    private static DocumentBlock CreateParagraph(string id, double order, List<InlineContent> inlines, double spacingAfter = 10)
        => new()
        {
            Id = id,
            SectionId = RecoverySectionId,
            Type = DocumentBlockType.Paragraph,
            Order = order,
            ParagraphProperties = new DocumentParagraphProperties
            {
                Alignment = DocumentTextAlignment.Left,
                LineSpacing = 1.25,
                SpacingAfter = spacingAfter
            },
            Content = new ParagraphBlockContent { Inlines = inlines }
        };

    private static DocumentBlock CreateImage(
        string id,
        double order,
        DocumentImageSource source,
        string? url,
        string? assetId,
        string? altText,
        string caption,
        double width,
        double height,
        DocumentImageAlignment alignment,
        DocumentObjectLayout layout)
        => new()
        {
            Id = id,
            SectionId = RecoverySectionId,
            Type = DocumentBlockType.Image,
            Order = order,
            Content = new ImageBlockContent
            {
                Source = source,
                Url = source == DocumentImageSource.Url ? url : null,
                AssetId = source == DocumentImageSource.Asset ? assetId : null,
                AltText = altText,
                Caption = caption,
                Size = new DocumentImageSize { Width = width, Height = height },
                NaturalSize = new DocumentImageSize { Width = width, Height = height },
                Alignment = alignment,
                Layout = layout
            }
        };

    private static DocumentObjectLayout CreateRecoveryWrappedImageLayout(
        double width,
        double height,
        DocumentImageHorizontalPosition horizontalPosition,
        string anchorBlockId)
        => new()
        {
            Kind = DocumentObjectLayoutKind.Anchored,
            Anchor = new DocumentObjectAnchor
            {
                BlockId = anchorBlockId,
                MoveWithText = true,
                FixedOnPage = false
            },
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Page,
                VerticalRelativeTo = DocumentRelativePosition.Paragraph,
                HorizontalAlignment = horizontalPosition
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = DocumentWrapMode.Square,
                DistanceLeft = horizontalPosition == DocumentImageHorizontalPosition.Right ? 16 : 0,
                DistanceRight = horizontalPosition == DocumentImageHorizontalPosition.Left ? 16 : 0,
                DistanceBottom = 12
            },
            Transform = new DocumentObjectTransform
            {
                Width = width,
                Height = height,
                NaturalWidth = width,
                NaturalHeight = height,
                LockAspectRatio = true
            }
        };

    private static DocumentObjectLayout CreateRecoveryTopBottomImageLayout(double width, double height, string anchorBlockId)
        => new()
        {
            Kind = DocumentObjectLayoutKind.Anchored,
            Anchor = new DocumentObjectAnchor
            {
                BlockId = anchorBlockId,
                MoveWithText = true,
                FixedOnPage = false
            },
            Position = new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Page,
                VerticalRelativeTo = DocumentRelativePosition.Paragraph,
                HorizontalAlignment = DocumentImageHorizontalPosition.Center
            },
            Wrap = new DocumentObjectWrap
            {
                Mode = DocumentWrapMode.TopBottom,
                DistanceTop = 10,
                DistanceBottom = 12
            },
            Transform = new DocumentObjectTransform
            {
                Width = width,
                Height = height,
                NaturalWidth = width,
                NaturalHeight = height,
                LockAspectRatio = true
            }
        };

    private static DocumentBlock CreateRecoveryTable()
        => new()
        {
            Id = "recovery-table-under-images",
            SectionId = RecoverySectionId,
            Type = DocumentBlockType.Table,
            Order = 130,
            Content = new TableBlockContent
            {
                Layout = new TableLayoutContent
                {
                    Width = 420,
                    Alignment = TableHorizontalAlignment.Center,
                    CellPadding = 7,
                    Borders = new TableCellBorders
                    {
                        Top = "1px solid #cbd5e1",
                        Right = "1px solid #cbd5e1",
                        Bottom = "1px solid #cbd5e1",
                        Left = "1px solid #cbd5e1"
                    }
                },
                Rows =
                [
                    new TableRowContent
                    {
                        Cells =
                        [
                            CreateRecoveryTableCell("Scenario", true),
                            CreateRecoveryTableCell("Expected visible state", true)
                        ]
                    },
                    new TableRowContent
                    {
                        Cells =
                        [
                            CreateRecoveryTableCell("Images"),
                            CreateRecoveryTableCell("All image layouts render before this table")
                        ]
                    },
                    new TableRowContent
                    {
                        Cells =
                        [
                            CreateRecoveryTableCell("Review"),
                            CreateRecoveryTableCell("Comments and revisions are visible in text")
                        ]
                    }
                ]
            }
        };

    private static TableCellContent CreateRecoveryTableCell(string text, bool isHeader = false)
        => new()
        {
            IsHeader = isHeader,
            Blocks =
            [
                new DocumentBlock
                {
                    Type = DocumentBlockType.Paragraph,
                    Content = new ParagraphBlockContent
                    {
                        Inlines = [new TextRun { Text = text }]
                    }
                }
            ]
        };

    private static DocumentRevision CreateRecoveryRevision(
        string id,
        DocumentRevisionType type,
        string blockId,
        int inlineIndex,
        string text,
        int createdAtOffsetMinutes)
        => new()
        {
            Id = id,
            Type = type,
            Range = new DocumentRevisionRange
            {
                BlockId = blockId,
                StartInlineIndex = inlineIndex,
                EndInlineIndex = inlineIndex,
                StartOffset = 0,
                EndOffset = text.Length
            },
            Author = new DocumentRevisionAuthor
            {
                Id = "recovery-reviewer",
                DisplayName = "Recovery Reviewer",
                Email = "recovery@example.local"
            },
            CreatedAt = new DateTimeOffset(2026, 5, 23, 8, createdAtOffsetMinutes, 0, TimeSpan.Zero),
            Action = DocumentRevisionAction.Pending,
            PayloadJson = text
        };

    private static T Clone<T>(T value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)
            ?? throw new System.Text.Json.JsonException("Could not clone document editor value.");
    }

    private sealed record StoredDocument(DocumentEditorDocument Document, string Json, string ConcurrencyToken);
}
