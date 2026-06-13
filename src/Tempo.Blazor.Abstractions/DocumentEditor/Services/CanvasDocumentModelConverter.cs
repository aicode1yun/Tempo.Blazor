using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Converts between <see cref="DocumentEditorDocument"/> and the canvas document model.</summary>
public static class CanvasDocumentModelConverter
{
    private const double PointsToCssPixels = 96d / 72d;
    private const double CssPixelsToPoints = 72d / 96d;

    /// <summary>Converts a persisted document editor snapshot into the canonical canvas model.</summary>
    public static CanvasDocumentModel ToCanvasModel(DocumentEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var sections = document.Sections
            .OrderBy(section => section.Order)
            .ThenBy(section => section.Id, StringComparer.Ordinal)
            .Select(section => ToCanvasSection(section, document.Blocks))
            .ToList();

        if (sections.Count == 0)
        {
            var fallbackSection = new DocumentSection { Order = 0 };
            sections.Add(ToCanvasSection(fallbackSection, document.Blocks));
        }

        var bodyBlocks = document.Blocks
            .OrderBy(block => block.Order)
            .ThenBy(block => block.Id, StringComparer.Ordinal)
            .Select(ToCanvasBlock)
            .ToList();

        return new CanvasDocumentModel
        {
            DocumentId = document.DocumentId,
            Version = document.Version,
            Metadata = Clone(document.Metadata),
            PageSettings = ToCanvasPageSettings(document.PageSettings),
            Theme = Clone(document.Theme),
            Hyphenation = Clone(document.Hyphenation),
            PageBackground = Clone(document.PageBackground),
            Sections = sections,
            Body = new CanvasDocumentBody { Blocks = bodyBlocks },
            Comments = Clone(document.Comments),
            Notes = document.Notes.Select(ToCanvasNote).ToList(),
            HeadersFooters = document.HeadersFooters.Select(ToCanvasHeaderFooter).ToList(),
            NumberingDefinitions = Clone(document.NumberingDefinitions),
            ListStyles = Clone(document.ListStyles),
            Styles = Clone(document.Styles),
            BibliographySources = Clone(document.BibliographySources),
            Citations = Clone(document.Citations),
            Revisions = Clone(document.Revisions),
            Assets = Clone(document.Assets),
            Anchors = Clone(document.Anchors),
            IsProtected = document.IsProtected,
            RestrictedMarkers = Clone(document.RestrictedMarkers),
            OutlineRevision = 0,
            TableOfContentsRevision = 0,
            Preserve = Preserve(document)
        };
    }

    /// <summary>Rebuilds a persisted document editor snapshot from the canonical canvas model.</summary>
    public static DocumentEditorDocument FromCanvasModel(CanvasDocumentModel model, DocumentEditorDocument? template = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        var document = CloneFromPreserve<DocumentEditorDocument>(model.Preserve) ?? Clone(template) ?? DocumentEditorDocument.Empty(model.DocumentId);
        document.DocumentId = string.IsNullOrWhiteSpace(model.DocumentId) ? document.DocumentId : model.DocumentId;
        document.Version = model.Version;
        document.Metadata = Clone(model.Metadata);
        document.PageSettings = FromCanvasPageSettings(model.PageSettings);
        document.Theme = Clone(model.Theme);
        document.Hyphenation = Clone(model.Hyphenation);
        document.PageBackground = Clone(model.PageBackground);
        document.Sections = model.Sections.Select(FromCanvasSection).ToList();
        if (document.Sections.Count == 0)
        {
            document.Sections.Add(new DocumentSection { Order = 0, Properties = new DocumentSectionProperties() });
        }

        var blocks = model.Body.Blocks.Count > 0
            ? model.Body.Blocks
            : model.Sections.SelectMany(section => section.Blocks).ToList();

        document.Blocks = blocks
            .OrderBy(block => block.Order)
            .ThenBy(block => block.Id, StringComparer.Ordinal)
            .Select(FromCanvasBlock)
            .ToList();

        document.Comments = PreferCanvasList(model.Comments, document.Comments);
        document.Notes = model.Notes.Count > 0
            ? model.Notes.Select(FromCanvasNote).ToList()
            : Clone(document.Notes);
        document.HeadersFooters = model.HeadersFooters.Count > 0
            ? model.HeadersFooters.Select(FromCanvasHeaderFooter).ToList()
            : Clone(document.HeadersFooters);
        document.NumberingDefinitions = PreferCanvasList(model.NumberingDefinitions, document.NumberingDefinitions);
        document.ListStyles = PreferCanvasList(model.ListStyles, document.ListStyles);
        document.Styles = PreferCanvasList(model.Styles, document.Styles);
        document.BibliographySources = PreferCanvasList(model.BibliographySources, document.BibliographySources);
        document.Citations = PreferCanvasList(model.Citations, document.Citations);
        document.Revisions = PreferCanvasList(model.Revisions, document.Revisions);
        document.Assets = PreferCanvasList(model.Assets, document.Assets);
        document.Anchors = PreferCanvasList(model.Anchors, document.Anchors);
        document.IsProtected = model.IsProtected;
        document.RestrictedMarkers = PreferCanvasList(model.RestrictedMarkers, document.RestrictedMarkers);
        return document;
    }

    private static CanvasDocumentSection ToCanvasSection(DocumentSection section, IReadOnlyList<DocumentBlock> allBlocks)
    {
        var blocks = allBlocks
            .Where(block => string.Equals(block.SectionId, section.Id, StringComparison.Ordinal)
                || (string.IsNullOrWhiteSpace(block.SectionId) && section.Order == 0))
            .OrderBy(block => block.Order)
            .ThenBy(block => block.Id, StringComparer.Ordinal)
            .Select(ToCanvasBlock)
            .ToList();

        return new CanvasDocumentSection
        {
            Id = section.Id,
            Order = section.Order,
            Title = section.Title,
            Properties = Clone(section.Properties),
            PageSettings = ToCanvasPageSettings(section.Properties.PageSettings),
            Blocks = blocks,
            Preserve = Preserve(section)
        };
    }

    private static DocumentSection FromCanvasSection(CanvasDocumentSection section)
    {
        var restored = CloneFromPreserve<DocumentSection>(section.Preserve) ?? new DocumentSection();
        restored.Id = section.Id;
        restored.Order = section.Order;
        restored.Title = section.Title;
        restored.Properties = Clone(section.Properties);
        restored.Properties.PageSettings = FromCanvasPageSettings(section.PageSettings);
        return restored;
    }

    private static CanvasDocumentBlock ToCanvasBlock(DocumentBlock block)
    {
        var content = ToCanvasContent(block);
        return new CanvasDocumentBlock
        {
            Id = block.Id,
            SectionId = block.SectionId,
            Type = content.Type,
            Order = block.Order,
            ParagraphProperties = Clone(block.ParagraphProperties),
            Content = content,
            Preserve = Preserve(block)
        };
    }

    private static CanvasBlockContent ToCanvasContent(DocumentBlock block)
        => block.Content switch
        {
            HeadingBlockContent heading => new CanvasBlockContent
            {
                Type = CanvasDocumentModelTypes.Heading,
                HeadingLevel = Math.Max(1, heading.Level),
                StyleName = $"Heading {Math.Max(1, heading.Level)}",
                OutlineLevel = Math.Max(1, heading.Level),
                Runs = heading.Inlines.Select(ToCanvasRun).ToList()
            },
            ListBlockContent list => new CanvasBlockContent
            {
                Type = CanvasDocumentModelTypes.List,
                Runs = list.Inlines.Select(ToCanvasRun).ToList(),
                List = new CanvasListProperties
                {
                    Ordered = list.Ordered,
                    IndentLevel = Math.Max(0, list.IndentLevel),
                    StartNumber = Math.Max(1, list.StartNumber),
                    NumberingId = list.NumberingId,
                    AbstractNumberingId = list.AbstractNumberingId,
                    ListStyleId = list.ListStyleId,
                    NumberFormat = list.NumberFormat,
                    LevelText = list.LevelText,
                    Suffix = list.Suffix,
                    LabelIndent = list.LabelIndent.HasValue ? list.LabelIndent.Value * PointsToCssPixels : null,
                    HangingIndent = list.HangingIndent.HasValue ? list.HangingIndent.Value * PointsToCssPixels : null,
                    RestartNumbering = list.RestartNumbering,
                    ContinueNumbering = list.ContinueNumbering,
                    NumberingValue = list.NumberingValue
                }
            },
            QuoteBlockContent quote => new CanvasBlockContent
            {
                Type = CanvasDocumentModelTypes.Quote,
                Runs = quote.Inlines.Select(ToCanvasRun).ToList()
            },
            TableBlockContent table => new CanvasBlockContent
            {
                Type = CanvasDocumentModelTypes.Table,
                Table = ToCanvasTable(table)
            },
            ImageBlockContent image => new CanvasBlockContent
            {
                Type = CanvasDocumentModelTypes.Image,
                Image = ToCanvasImage(image)
            },
            PageBreakBlockContent pageBreak => new CanvasBlockContent
            {
                Type = CanvasDocumentModelTypes.PageBreak,
                PageBreak = new CanvasPageBreakContent
                {
                    BreakType = pageBreak.BreakType,
                    NextSectionId = pageBreak.NextSectionId
                }
            },
            ContentControlBlockContent control => new CanvasBlockContent
            {
                Type = CanvasDocumentModelTypes.ContentControl,
                ContentControl = new CanvasContentControlBlock
                {
                    Control = Clone(control.Control),
                    Blocks = control.Blocks.Select(ToCanvasBlock).ToList()
                }
            },
            ParagraphBlockContent paragraph => new CanvasBlockContent
            {
                Type = CanvasDocumentModelTypes.Paragraph,
                Runs = paragraph.Inlines.Select(ToCanvasRun).ToList(),
                Caption = Clone(paragraph.Caption),
                TableOfContents = Clone(paragraph.TableOfContents)
            },
            _ => new CanvasBlockContent
            {
                Type = CanvasDocumentModelTypes.Paragraph,
                Runs = []
            }
        };

    private static DocumentBlock FromCanvasBlock(CanvasDocumentBlock block)
    {
        var restored = CloneFromPreserve<DocumentBlock>(block.Preserve) ?? new DocumentBlock();
        var preservedType = restored.Type;
        var preservedContent = restored.Content;
        restored.Id = block.Id;
        restored.SectionId = block.SectionId;
        restored.Order = block.Order;
        restored.ParagraphProperties = Clone(block.ParagraphProperties);

        if (ShouldRestorePreservedStructuralBlock(block.Content, preservedType, preservedContent))
        {
            restored.Type = preservedType;
            restored.Content = Clone(preservedContent);
            return restored;
        }

        restored.Type = DocumentBlockTypeFromCanvas(block.Content.Type);
        restored.Content = FromCanvasContent(block.Content, preservedContent);
        return restored;
    }

    private static bool ShouldRestorePreservedStructuralBlock(
        CanvasBlockContent content,
        DocumentBlockType preservedType,
        DocumentBlockContent? preservedContent)
        => preservedContent is ImageBlockContent or TableBlockContent or PageBreakBlockContent
        && preservedType is DocumentBlockType.Image or DocumentBlockType.Table or DocumentBlockType.PageBreak
        && DocumentBlockTypeFromCanvas(content.Type) == DocumentBlockType.Paragraph
        && IsEmptyCanvasTextContent(content);

    private static bool IsEmptyCanvasTextContent(CanvasBlockContent content)
        => content.Runs.Count == 0
        || content.Runs.All(run => string.IsNullOrEmpty(run.Text));

    private static DocumentBlockContent FromCanvasContent(CanvasBlockContent content, DocumentBlockContent? preservedContent = null)
        => content.Type switch
        {
            CanvasDocumentModelTypes.Heading => new HeadingBlockContent
            {
                Level = Math.Max(1, content.HeadingLevel ?? content.OutlineLevel ?? 1),
                Inlines = content.Runs.Select(FromCanvasRun).ToList()
            },
            CanvasDocumentModelTypes.List => new ListBlockContent
            {
                Ordered = content.List?.Ordered ?? false,
                IndentLevel = Math.Max(0, content.List?.IndentLevel ?? 0),
                StartNumber = Math.Max(1, content.List?.StartNumber ?? 1),
                NumberingId = content.List?.NumberingId,
                AbstractNumberingId = content.List?.AbstractNumberingId,
                ListStyleId = content.List?.ListStyleId,
                NumberFormat = content.List?.NumberFormat,
                LevelText = content.List?.LevelText,
                Suffix = content.List?.Suffix,
                LabelIndent = content.List?.LabelIndent.HasValue == true ? content.List.LabelIndent.Value * CssPixelsToPoints : null,
                HangingIndent = content.List?.HangingIndent.HasValue == true ? content.List.HangingIndent.Value * CssPixelsToPoints : null,
                RestartNumbering = content.List?.RestartNumbering ?? false,
                ContinueNumbering = content.List?.ContinueNumbering ?? false,
                NumberingValue = content.List?.NumberingValue,
                Inlines = content.Runs.Select(FromCanvasRun).ToList()
            },
            CanvasDocumentModelTypes.Quote => new QuoteBlockContent
            {
                Inlines = content.Runs.Select(FromCanvasRun).ToList()
            },
            CanvasDocumentModelTypes.Table => FromCanvasTable(content.Table ?? new CanvasTableContent()),
            CanvasDocumentModelTypes.Image => FromCanvasImage(content.Image, preservedContent as ImageBlockContent),
            CanvasDocumentModelTypes.PageBreak => new PageBreakBlockContent
            {
                BreakType = content.PageBreak?.BreakType ?? DocumentSectionBreakType.Page,
                NextSectionId = content.PageBreak?.NextSectionId
            },
            CanvasDocumentModelTypes.ContentControl => new ContentControlBlockContent
            {
                Control = Clone(content.ContentControl?.Control ?? new DocumentContentControl { Scope = DocumentContentControlScope.Block }),
                Blocks = content.ContentControl?.Blocks.Select(FromCanvasBlock).ToList() ?? []
            },
            _ => new ParagraphBlockContent
            {
                Inlines = content.Runs.Select(FromCanvasRun).ToList(),
                Caption = Clone(content.Caption),
                TableOfContents = Clone(content.TableOfContents)
            }
        };

    private static DocumentBlockType DocumentBlockTypeFromCanvas(string? type)
        => type switch
        {
            CanvasDocumentModelTypes.Heading => DocumentBlockType.Heading,
            CanvasDocumentModelTypes.List => DocumentBlockType.List,
            CanvasDocumentModelTypes.Quote => DocumentBlockType.Quote,
            CanvasDocumentModelTypes.Table => DocumentBlockType.Table,
            CanvasDocumentModelTypes.Image => DocumentBlockType.Image,
            CanvasDocumentModelTypes.PageBreak => DocumentBlockType.PageBreak,
            CanvasDocumentModelTypes.ContentControl => DocumentBlockType.ContentControl,
            _ => DocumentBlockType.Paragraph
        };

    private static CanvasInlineRun ToCanvasRun(InlineContent inline)
        => inline switch
        {
            TextRun text => new CanvasInlineRun
            {
                Id = text.Id,
                Type = CanvasDocumentModelTypes.TextRun,
                Text = text.Text,
                Marks = text.Marks.Select(ToCanvasMark).ToList(),
                Preserve = Preserve(text)
            },
            DocumentFieldRun field => new CanvasInlineRun
            {
                Id = field.Id,
                Type = CanvasDocumentModelTypes.FieldRun,
                Marks = field.Marks.Select(ToCanvasMark).ToList(),
                Field = new CanvasFieldRun
                {
                    FieldType = field.FieldType,
                    Format = field.Format,
                    FallbackText = field.FallbackText,
                    DisplayText = field.DisplayText,
                    InstrText = field.InstrText,
                    CachedResult = field.CachedResult,
                    TargetId = field.TargetId,
                    ReferenceKind = field.ReferenceKind,
                    ReferenceFormat = field.ReferenceFormat,
                    SequenceId = field.SequenceId,
                    SequenceLabel = field.SequenceLabel,
                    CitationId = field.CitationId,
                    Metadata = Clone(field.Metadata)
                },
                Preserve = Preserve(field)
            },
            TokenRun token => new CanvasInlineRun
            {
                Id = token.Id,
                Type = CanvasDocumentModelTypes.TokenRun,
                Text = token.DisplayName,
                Marks = token.Marks.Select(ToCanvasMark).ToList(),
                Token = new CanvasTokenRun
                {
                    Key = token.Key,
                    DisplayName = token.DisplayName,
                    TokenType = token.TokenType,
                    TypeLabel = token.TypeLabel,
                    ColorClass = token.ColorClass,
                    Description = token.Description,
                    FallbackText = token.FallbackText
                },
                Preserve = Preserve(token)
            },
            DocumentNoteReferenceRun note => new CanvasInlineRun
            {
                Id = note.Id,
                Type = CanvasDocumentModelTypes.NoteReferenceRun,
                Marks = note.Marks.Select(ToCanvasMark).ToList(),
                NoteReference = new CanvasNoteReferenceRun
                {
                    NoteId = note.NoteId,
                    NoteType = note.NoteType,
                    DisplayMarker = note.DisplayMarker
                },
                Preserve = Preserve(note)
            },
            DocumentDrawingRun drawing => new CanvasInlineRun
            {
                Id = drawing.Id,
                Type = CanvasDocumentModelTypes.DrawingRun,
                Marks = drawing.Marks.Select(ToCanvasMark).ToList(),
                Drawing = ToCanvasDrawing(drawing),
                Preserve = Preserve(drawing)
            },
            DocumentMathRun math => new CanvasInlineRun
            {
                Id = math.Id,
                Type = CanvasDocumentModelTypes.MathRun,
                Marks = math.Marks.Select(ToCanvasMark).ToList(),
                Math = ToCanvasMath(math),
                Preserve = Preserve(math)
            },
            DocumentContentControlRun control => new CanvasInlineRun
            {
                Id = control.Id,
                Type = CanvasDocumentModelTypes.ContentControlRun,
                Marks = control.Marks.Select(ToCanvasMark).ToList(),
                Text = ContentControlDisplayText(control.Control),
                ContentControl = new CanvasContentControlRun
                {
                    Control = Clone(control.Control),
                    Runs = control.Inlines.Select(ToCanvasRun).ToList()
                },
                Preserve = Preserve(control)
            },
            DocumentSigningFieldRun signing => new CanvasInlineRun
            {
                Id = signing.Id,
                Type = CanvasDocumentModelTypes.SigningFieldRun,
                Marks = signing.Marks.Select(ToCanvasMark).ToList(),
                SigningField = new CanvasSigningFieldRun
                {
                    Uuid = signing.Uuid,
                    FieldType = signing.FieldType,
                    SubmitterUuid = signing.SubmitterUuid,
                    Required = signing.Required,
                    Label = signing.Label,
                    BoxWidth = signing.BoxWidth,
                    BoxHeight = signing.BoxHeight
                },
                Preserve = Preserve(signing)
            },
            _ => new CanvasInlineRun
            {
                Id = inline.Id,
                Type = CanvasDocumentModelTypes.TextRun,
                Marks = inline.Marks.Select(ToCanvasMark).ToList(),
                Preserve = Preserve(inline)
            }
        };

    private static InlineContent FromCanvasRun(CanvasInlineRun run)
    {
        InlineContent restored = run.Type switch
        {
            CanvasDocumentModelTypes.FieldRun => FromCanvasField(run, CloneFromPreserve<DocumentFieldRun>(run.Preserve)),
            CanvasDocumentModelTypes.TokenRun => FromCanvasToken(run, CloneFromPreserve<TokenRun>(run.Preserve)),
            CanvasDocumentModelTypes.NoteReferenceRun => FromCanvasNoteReference(run, CloneFromPreserve<DocumentNoteReferenceRun>(run.Preserve)),
            CanvasDocumentModelTypes.DrawingRun => FromCanvasDrawing(run, CloneFromPreserve<DocumentDrawingRun>(run.Preserve)),
            CanvasDocumentModelTypes.MathRun => FromCanvasMath(run, CloneFromPreserve<DocumentMathRun>(run.Preserve)),
            CanvasDocumentModelTypes.ContentControlRun => FromCanvasContentControl(run, CloneFromPreserve<DocumentContentControlRun>(run.Preserve)),
            CanvasDocumentModelTypes.SigningFieldRun => FromCanvasSigningField(run, CloneFromPreserve<DocumentSigningFieldRun>(run.Preserve)),
            _ => FromCanvasText(run, CloneFromPreserve<TextRun>(run.Preserve))
        };

        restored.Id = run.Id;
        restored.Marks = run.Marks.Select(FromCanvasMark).ToList();
        return restored;
    }

    private static InlineContent FromCanvasText(CanvasInlineRun run, TextRun? preserved)
    {
        var text = preserved ?? new TextRun();
        text.Text = run.Text;
        return text;
    }

    private static InlineContent FromCanvasField(CanvasInlineRun run, DocumentFieldRun? preserved)
    {
        var field = preserved ?? new DocumentFieldRun();
        if (run.Field is not null)
        {
            field.FieldType = run.Field.FieldType;
            field.Format = run.Field.Format;
            field.FallbackText = run.Field.FallbackText;
            field.DisplayText = run.Field.DisplayText;
            field.InstrText = run.Field.InstrText;
            field.CachedResult = run.Field.CachedResult;
            field.TargetId = run.Field.TargetId;
            field.ReferenceKind = run.Field.ReferenceKind;
            field.ReferenceFormat = run.Field.ReferenceFormat;
            field.SequenceId = run.Field.SequenceId;
            field.SequenceLabel = run.Field.SequenceLabel;
            field.CitationId = run.Field.CitationId;
            field.Metadata = Clone(run.Field.Metadata);
        }

        return field;
    }

    private static InlineContent FromCanvasToken(CanvasInlineRun run, TokenRun? preserved)
    {
        var token = preserved ?? new TokenRun();
        if (run.Token is not null)
        {
            token.Key = run.Token.Key;
            token.DisplayName = run.Token.DisplayName;
            token.TokenType = run.Token.TokenType;
            token.TypeLabel = run.Token.TypeLabel;
            token.ColorClass = run.Token.ColorClass;
            token.Description = run.Token.Description;
            token.FallbackText = run.Token.FallbackText;
        }

        return token;
    }

    private static InlineContent FromCanvasNoteReference(CanvasInlineRun run, DocumentNoteReferenceRun? preserved)
    {
        var note = preserved ?? new DocumentNoteReferenceRun();
        if (run.NoteReference is not null)
        {
            note.NoteId = run.NoteReference.NoteId;
            note.NoteType = run.NoteReference.NoteType;
            note.DisplayMarker = run.NoteReference.DisplayMarker;
        }

        return note;
    }

    private static InlineContent FromCanvasDrawing(CanvasInlineRun run, DocumentDrawingRun? preserved)
    {
        var drawing = preserved ?? new DocumentDrawingRun();
        if (run.Drawing is not null)
        {
            ApplyCanvasDrawing(drawing, run.Drawing);
        }

        return drawing;
    }

    private static InlineContent FromCanvasMath(CanvasInlineRun run, DocumentMathRun? preserved)
    {
        var math = preserved ?? new DocumentMathRun();
        if (run.Math is not null)
        {
            math.MathId = run.Math.MathId;
            math.DisplayMode = run.Math.DisplayMode;
            math.Content = Clone(run.Math.Content);
            math.AltText = run.Math.AltText;
            math.MathML = run.Math.MathML;
            math.OmmlXml = run.Math.OmmlXml;
            math.Metadata = Clone(run.Math.Metadata);
        }

        return math;
    }

    private static InlineContent FromCanvasContentControl(CanvasInlineRun run, DocumentContentControlRun? preserved)
    {
        var control = preserved ?? new DocumentContentControlRun();
        if (run.ContentControl is not null)
        {
            control.Control = Clone(run.ContentControl.Control);
            control.Control.Scope = DocumentContentControlScope.Inline;
            control.Inlines = run.ContentControl.Runs.Select(FromCanvasRun).ToList();
        }

        return control;
    }

    private static InlineContent FromCanvasSigningField(CanvasInlineRun run, DocumentSigningFieldRun? preserved)
    {
        var field = preserved ?? new DocumentSigningFieldRun();
        if (run.SigningField is not null)
        {
            field.Uuid = run.SigningField.Uuid;
            field.FieldType = run.SigningField.FieldType;
            field.SubmitterUuid = run.SigningField.SubmitterUuid;
            field.Required = run.SigningField.Required;
            field.Label = run.SigningField.Label;
            field.BoxWidth = run.SigningField.BoxWidth;
            field.BoxHeight = run.SigningField.BoxHeight;
        }

        return field;
    }

    private static string ContentControlDisplayText(DocumentContentControl control)
    {
        var value = control.Value ?? new DocumentContentControlValue();
        return control.Kind switch
        {
            DocumentContentControlKind.Checkbox => value.Checked == true ? "☑" : "☐",
            DocumentContentControlKind.DropDown or DocumentContentControlKind.ComboBox => DisplayChoice(control, value.SelectedValue ?? value.Text),
            DocumentContentControlKind.Date => value.DateIso ?? value.Text ?? control.PlaceholderText ?? string.Empty,
            DocumentContentControlKind.Picture => value.AssetId ?? control.PlaceholderText ?? string.Empty,
            _ => value.Text ?? control.PlaceholderText ?? string.Empty
        };
    }

    private static string DisplayChoice(DocumentContentControl control, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return control.PlaceholderText ?? string.Empty;
        }

        var item = control.Items.FirstOrDefault(candidate => string.Equals(candidate.Value, value, StringComparison.Ordinal));
        return string.IsNullOrWhiteSpace(item?.DisplayText) ? value : item.DisplayText;
    }

    private static CanvasInlineMark ToCanvasMark(InlineMark mark)
        => new()
        {
            Type = CanvasMarkTypeName(mark.Type),
            Value = mark.Value,
            Link = Clone(mark.Link),
            CommentAnchor = Clone(mark.CommentAnchor),
            RevisionId = mark.RevisionId,
            Preserve = Preserve(mark)
        };

    private static InlineMark FromCanvasMark(CanvasInlineMark mark)
    {
        var restored = CloneFromPreserve<InlineMark>(mark.Preserve) ?? new InlineMark();
        restored.Type = InlineMarkTypeFromName(mark.Type);
        restored.Value = mark.Value;
        restored.Link = Clone(mark.Link);
        restored.CommentAnchor = Clone(mark.CommentAnchor);
        restored.RevisionId = mark.RevisionId;
        return restored;
    }

    private static string CanvasMarkTypeName(InlineMarkType type)
        => type switch
        {
            InlineMarkType.Bold => "bold",
            InlineMarkType.Italic => "italic",
            InlineMarkType.Underline => "underline",
            InlineMarkType.Strikethrough => "strikethrough",
            InlineMarkType.Superscript => "superscript",
            InlineMarkType.Subscript => "subscript",
            InlineMarkType.SmallCaps => "smallCaps",
            InlineMarkType.AllCaps => "allCaps",
            InlineMarkType.DoubleStrikethrough => "doubleStrikethrough",
            InlineMarkType.CharacterSpacing => "characterSpacing",
            InlineMarkType.CharacterScale => "characterScale",
            InlineMarkType.Kerning => "kerning",
            InlineMarkType.Link => "link",
            InlineMarkType.CommentAnchor => "commentAnchor",
            InlineMarkType.Revision => "revision",
            InlineMarkType.Highlight => "highlight",
            InlineMarkType.TextColor => "textColor",
            InlineMarkType.FontFamily => "fontFamily",
            InlineMarkType.FontSize => "fontSize",
            InlineMarkType.Bookmark => "bookmark",
            _ => type.ToString()
        };

    private static InlineMarkType InlineMarkTypeFromName(string? name)
        => (name ?? string.Empty).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant() switch
        {
            "bold" => InlineMarkType.Bold,
            "italic" => InlineMarkType.Italic,
            "underline" => InlineMarkType.Underline,
            "strikethrough" or "strike" => InlineMarkType.Strikethrough,
            "superscript" => InlineMarkType.Superscript,
            "subscript" => InlineMarkType.Subscript,
            "smallcaps" => InlineMarkType.SmallCaps,
            "allcaps" => InlineMarkType.AllCaps,
            "doublestrikethrough" or "doublestrike" => InlineMarkType.DoubleStrikethrough,
            "characterspacing" => InlineMarkType.CharacterSpacing,
            "characterscale" => InlineMarkType.CharacterScale,
            "kerning" => InlineMarkType.Kerning,
            "link" or "hyperlink" => InlineMarkType.Link,
            "commentanchor" or "comment" => InlineMarkType.CommentAnchor,
            "revision" => InlineMarkType.Revision,
            "highlight" => InlineMarkType.Highlight,
            "textcolor" or "fontcolor" => InlineMarkType.TextColor,
            "fontfamily" => InlineMarkType.FontFamily,
            "fontsize" => InlineMarkType.FontSize,
            "bookmark" => InlineMarkType.Bookmark,
            _ => InlineMarkType.Bold
        };

    private static CanvasTableContent ToCanvasTable(TableBlockContent table)
        => new()
        {
            Layout = Clone(table.Layout),
            Rows = table.Rows.Select((row, rowIndex) => new CanvasTableRow
            {
                Id = $"row-{rowIndex + 1}",
                Cells = row.Cells.Select(ToCanvasCell).ToList()
            }).ToList()
        };

    private static CanvasTableCell ToCanvasCell(TableCellContent cell)
        => new()
        {
            Id = cell.Id,
            ColumnSpan = Math.Max(1, cell.ColumnSpan),
            RowSpan = Math.Max(1, cell.RowSpan),
            IsHeader = cell.IsHeader,
            Merge = Clone(cell.Merge),
            Width = cell.Width,
            BackgroundColor = cell.BackgroundColor,
            Borders = Clone(cell.Borders),
            VerticalAlignment = cell.VerticalAlignment,
            Padding = cell.Padding,
            Blocks = cell.Blocks.Select(ToCanvasBlock).ToList(),
            Preserve = Preserve(cell)
        };

    private static TableBlockContent FromCanvasTable(CanvasTableContent table)
        => new()
        {
            Layout = Clone(table.Layout),
            Rows = table.Rows.Select(row => new TableRowContent
            {
                Cells = row.Cells.Select(FromCanvasCell).ToList()
            }).ToList()
        };

    private static TableCellContent FromCanvasCell(CanvasTableCell cell)
    {
        var restored = CloneFromPreserve<TableCellContent>(cell.Preserve) ?? new TableCellContent();
        restored.Id = cell.Id;
        restored.ColumnSpan = Math.Max(1, cell.ColumnSpan);
        restored.RowSpan = Math.Max(1, cell.RowSpan);
        restored.IsHeader = cell.IsHeader;
        restored.Merge = Clone(cell.Merge);
        restored.Width = cell.Width;
        restored.BackgroundColor = cell.BackgroundColor;
        restored.Borders = Clone(cell.Borders);
        restored.VerticalAlignment = cell.VerticalAlignment;
        restored.Padding = cell.Padding;
        restored.Blocks = cell.Blocks.Select(FromCanvasBlock).ToList();
        return restored;
    }

    private static CanvasImageContent ToCanvasImage(ImageBlockContent image)
        => new()
        {
            Source = image.Source,
            Url = image.Url,
            AssetId = image.AssetId,
            AltText = image.AltText,
            IsDecorative = image.IsDecorative,
            Caption = image.Caption,
            Size = Clone(image.Size),
            NaturalSize = Clone(image.NaturalSize),
            Alignment = image.Alignment,
            Layout = Clone(image.Layout),
            LinkUrl = image.LinkUrl
        };

    private static ImageBlockContent FromCanvasImage(CanvasImageContent? image, ImageBlockContent? preservedImage)
    {
        var restored = preservedImage is null ? new ImageBlockContent() : Clone(preservedImage);
        if (image is null)
        {
            return restored;
        }

        restored.Source = image.Source;
        restored.Url = image.Url;
        restored.AssetId = image.AssetId;
        restored.AltText = image.AltText;
        restored.IsDecorative = image.IsDecorative;
        restored.Caption = image.Caption;
        restored.Size = HasImageSize(image.Size) ? Clone(image.Size) : Clone(restored.Size);
        restored.NaturalSize = HasImageSize(image.NaturalSize) ? Clone(image.NaturalSize) : Clone(restored.NaturalSize);
        restored.Alignment = image.Alignment;
        restored.Layout = image.Layout is null ? Clone(restored.Layout) : Clone(image.Layout);
        restored.LinkUrl = image.LinkUrl;
        return restored;
    }

    private static bool HasImageSize(DocumentImageSize? size)
        => size?.Width is not null || size?.Height is not null;

    private static CanvasDrawingRun ToCanvasDrawing(DocumentDrawingRun drawing)
        => new()
        {
            ObjectId = drawing.ObjectId,
            Kind = drawing.Kind,
            Source = drawing.Source,
            Url = drawing.Url,
            AssetId = drawing.AssetId,
            AltText = drawing.AltText,
            IsDecorative = drawing.IsDecorative,
            Caption = drawing.Caption,
            Size = Clone(drawing.Size),
            NaturalSize = Clone(drawing.NaturalSize),
            Layout = Clone(drawing.Layout),
            LinkUrl = drawing.LinkUrl,
            Shape = Clone(drawing.Shape),
            TextBody = Clone(drawing.TextBody),
            Chart = Clone(drawing.Chart),
            Group = Clone(drawing.Group),
            Docx = Clone(drawing.Docx),
            Metadata = Clone(drawing.Metadata)
        };

    private static void ApplyCanvasDrawing(DocumentDrawingRun drawing, CanvasDrawingRun canvas)
    {
        drawing.ObjectId = canvas.ObjectId;
        drawing.Kind = canvas.Kind;
        drawing.Source = canvas.Source;
        drawing.Url = canvas.Url;
        drawing.AssetId = canvas.AssetId;
        drawing.AltText = canvas.AltText;
        drawing.IsDecorative = canvas.IsDecorative;
        drawing.Caption = canvas.Caption;
        drawing.Size = Clone(canvas.Size);
        drawing.NaturalSize = Clone(canvas.NaturalSize);
        drawing.Layout = Clone(canvas.Layout);
        drawing.LinkUrl = canvas.LinkUrl;
        drawing.Shape = Clone(canvas.Shape);
        drawing.TextBody = Clone(canvas.TextBody);
        drawing.Chart = Clone(canvas.Chart);
        drawing.Group = Clone(canvas.Group);
        drawing.Docx = Clone(canvas.Docx);
        drawing.Metadata = Clone(canvas.Metadata);
    }

    private static CanvasMathRun ToCanvasMath(DocumentMathRun math)
        => new()
        {
            MathId = math.MathId,
            DisplayMode = math.DisplayMode,
            Content = Clone(math.Content),
            AltText = math.AltText,
            MathML = math.MathML,
            OmmlXml = math.OmmlXml,
            Metadata = Clone(math.Metadata)
        };

    private static CanvasDocumentNote ToCanvasNote(DocumentNote note)
        => new()
        {
            Id = note.Id,
            Type = note.Type,
            SectionId = note.SectionId,
            Marker = note.Marker,
            Blocks = note.Blocks.Select(ToCanvasBlock).ToList(),
            ReferenceIds = Clone(note.ReferenceIds),
            Preserve = Preserve(note)
        };

    private static DocumentNote FromCanvasNote(CanvasDocumentNote note)
    {
        var restored = CloneFromPreserve<DocumentNote>(note.Preserve) ?? new DocumentNote();
        restored.Id = note.Id;
        restored.Type = note.Type;
        restored.SectionId = note.SectionId;
        restored.Marker = note.Marker;
        restored.Blocks = note.Blocks.Select(FromCanvasBlock).ToList();
        restored.ReferenceIds = Clone(note.ReferenceIds);
        return restored;
    }

    private static CanvasDocumentHeaderFooter ToCanvasHeaderFooter(DocumentHeaderFooter headerFooter)
        => new()
        {
            Id = headerFooter.Id,
            Type = headerFooter.Type,
            Scope = headerFooter.Scope,
            SectionId = headerFooter.SectionId,
            Blocks = headerFooter.Blocks.Select(ToCanvasBlock).ToList(),
            Preserve = Preserve(headerFooter)
        };

    private static DocumentHeaderFooter FromCanvasHeaderFooter(CanvasDocumentHeaderFooter headerFooter)
    {
        var restored = CloneFromPreserve<DocumentHeaderFooter>(headerFooter.Preserve) ?? new DocumentHeaderFooter();
        restored.Id = headerFooter.Id;
        restored.Type = headerFooter.Type;
        restored.Scope = headerFooter.Scope;
        restored.SectionId = headerFooter.SectionId;
        restored.Blocks = headerFooter.Blocks.Select(FromCanvasBlock).ToList();
        return restored;
    }

    private static CanvasPageSettings ToCanvasPageSettings(DocumentPageSettings settings)
    {
        var size = settings.Size ?? DocumentPageSize.A4;
        var margins = settings.Margins ?? DocumentPageMargins.Default;
        var widthPoints = settings.Landscape ? Math.Max(size.Width, size.Height) : size.Width;
        var heightPoints = settings.Landscape ? Math.Min(size.Width, size.Height) : size.Height;
        return new CanvasPageSettings
        {
            Width = ToCssPixels(widthPoints),
            Height = ToCssPixels(heightPoints),
            MarginTop = ToCssPixels(margins.Top),
            MarginRight = ToCssPixels(margins.Right),
            MarginBottom = ToCssPixels(margins.Bottom),
            MarginLeft = ToCssPixels(margins.Left),
            HeaderDistanceFromTop = ToCssPixels(settings.HeaderDistanceFromTop),
            FooterDistanceFromBottom = ToCssPixels(settings.FooterDistanceFromBottom),
            SizeName = size.Name,
            Landscape = settings.Landscape,
            Preserve = Preserve(settings)
        };
    }

    private static DocumentPageSettings FromCanvasPageSettings(CanvasPageSettings pageSettings)
    {
        var restored = CloneFromPreserve<DocumentPageSettings>(pageSettings.Preserve) ?? new DocumentPageSettings();
        var preservedCanvas = ToCanvasPageSettings(restored);
        if (!CanvasPageSettingsChanged(pageSettings, preservedCanvas))
        {
            return restored;
        }

        restored.Size = new DocumentPageSize
        {
            Name = pageSettings.SizeName,
            Width = ToPoints(pageSettings.Landscape ? pageSettings.Height : pageSettings.Width),
            Height = ToPoints(pageSettings.Landscape ? pageSettings.Width : pageSettings.Height)
        };
        restored.Margins = new DocumentPageMargins
        {
            Top = ToPoints(pageSettings.MarginTop),
            Right = ToPoints(pageSettings.MarginRight),
            Bottom = ToPoints(pageSettings.MarginBottom),
            Left = ToPoints(pageSettings.MarginLeft)
        };
        restored.HeaderDistanceFromTop = ToPoints(pageSettings.HeaderDistanceFromTop);
        restored.FooterDistanceFromBottom = ToPoints(pageSettings.FooterDistanceFromBottom);
        restored.Landscape = pageSettings.Landscape;
        return restored;
    }

    private static bool CanvasPageSettingsChanged(CanvasPageSettings left, CanvasPageSettings right)
        => !Close(left.Width, right.Width)
        || !Close(left.Height, right.Height)
        || !Close(left.MarginTop, right.MarginTop)
        || !Close(left.MarginRight, right.MarginRight)
        || !Close(left.MarginBottom, right.MarginBottom)
        || !Close(left.MarginLeft, right.MarginLeft)
        || !Close(left.HeaderDistanceFromTop, right.HeaderDistanceFromTop)
        || !Close(left.FooterDistanceFromBottom, right.FooterDistanceFromBottom)
        || left.Landscape != right.Landscape
        || !string.Equals(left.SizeName, right.SizeName, StringComparison.Ordinal);

    private static double ToCssPixels(double points)
        => Math.Round(points * PointsToCssPixels, 6);

    private static double ToPoints(double cssPixels)
        => Math.Round(cssPixels * CssPixelsToPoints, 6);

    private static bool Close(double left, double right)
        => Math.Abs(left - right) <= 0.0001;

    private static CanvasPreserveChannel Preserve<T>(T value)
        => new() { SourceJson = JsonSerializer.Serialize(value, DocumentEditorJson.Options) };

    private static T? CloneFromPreserve<T>(CanvasPreserveChannel? preserve)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(preserve?.SourceJson))
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(preserve.SourceJson, DocumentEditorJson.Options);
    }

    private static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)!;
    }

    private static List<T> PreferCanvasList<T>(List<T> canvasValues, List<T> preservedValues)
        => canvasValues.Count > 0 ? Clone(canvasValues) : Clone(preservedValues);
}
