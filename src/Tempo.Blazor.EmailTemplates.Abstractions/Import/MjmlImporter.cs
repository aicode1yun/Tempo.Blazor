using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Import;

/// <summary>
/// Imports MJML markup into an <see cref="EmailTemplateDocument"/>. Parsing is XML-based with an
/// <c>mj-raw</c> shim (raw content may be non-well-formed HTML), tolerates comments, preserves unknown
/// elements as raw blocks and unknown attributes in <see cref="EmailBlockBase.ExtraAttributes"/>, and
/// never throws — failures are returned as <see cref="ImportMessage"/>s.
/// </summary>
public sealed partial class MjmlImporter
{
    private const string RawTokenPrefix = "MJRAWPLACEHOLDER_";
    private const string RawTokenSuffix = "_END";

    private readonly List<ImportMessage> _warnings = new();
    private readonly List<ImportMessage> _errors = new();
    private List<string> _rawStore = new();
    private IMjmlIncludeResolver? _includes;

    /// <summary>Imports the given MJML markup, optionally resolving <c>mj-include</c> references.</summary>
    public ImportResult Import(string mjml, IMjmlIncludeResolver? includeResolver = null)
    {
        _warnings.Clear();
        _errors.Clear();
        _rawStore = new List<string>();
        _includes = includeResolver;

        if (string.IsNullOrWhiteSpace(mjml))
            return Fail(new ImportMessage(ImportKeys.Empty));

        XElement root;
        try
        {
            root = XDocument.Parse(ShimRaw(mjml), LoadOptions.PreserveWhitespace).Root!;
        }
        catch (XmlException ex)
        {
            return Fail(new ImportMessage(ImportKeys.ParseError, ex.Message, ex.LineNumber, ex.LinePosition));
        }

        if (!NameIs(root, "mjml"))
            return Fail(new ImportMessage(ImportKeys.NotMjml, root.Name.LocalName));

        var document = new EmailTemplateDocument();
        var lang = root.Attribute("lang")?.Value;
        if (!string.IsNullOrEmpty(lang)) document.Language = lang;

        var head = root.Elements().FirstOrDefault(e => NameIs(e, "mj-head"));
        if (head is not null) ImportHead(head, document);

        var body = root.Elements().FirstOrDefault(e => NameIs(e, "mj-body"));
        if (body is not null) ImportBody(body, document);

        // EI.16: surface content/layout issues found in the imported document as warnings
        // (e.g. foreign templates with images missing alt text).
        foreach (var finding in new Rendering.EmailDocumentValidator().Validate(document))
            _warnings.Add(new ImportMessage(finding.Key, finding.Path));

        return new ImportResult { Document = document, Warnings = _warnings.ToList(), Errors = _errors.ToList() };
    }

    private ImportResult Fail(ImportMessage error)
    {
        _errors.Add(error);
        return new ImportResult { Document = null, Warnings = _warnings.ToList(), Errors = _errors.ToList() };
    }

    // ── Raw shim ──────────────────────────────────────────────────────────────────────────────

    private string ShimRaw(string mjml)
        => RawRegex.Replace(mjml, m =>
        {
            _rawStore.Add(m.Groups["content"].Value);
            return m.Groups["open"].Value + RawTokenPrefix + (_rawStore.Count - 1) + RawTokenSuffix + m.Groups["close"].Value;
        });

    private string RestoreRaw(string placeholderText)
    {
        var match = RawTokenRegex.Match(placeholderText);
        return match.Success && int.TryParse(match.Groups["index"].Value, null, out var i) && i < _rawStore.Count
            ? _rawStore[i]
            : placeholderText;
    }

    // ── Head ────────────────────────────────────────────────────────────────────────────────────

    private static void ImportHead(XElement head, EmailTemplateDocument document)
    {
        var styles = document.Styles;
        foreach (var e in head.Elements())
        {
            switch (e.Name.LocalName)
            {
                case "mj-title": document.Subject = e.Value; break;
                case "mj-preview": document.Preheader = e.Value; break;
                case "mj-breakpoint":
                    var w = e.Attribute("width")?.Value;
                    if (!string.IsNullOrEmpty(w)) styles.Breakpoint = w;
                    break;
                case "mj-font":
                    styles.Fonts.Add(new EmailFont
                    {
                        Name = e.Attribute("name")?.Value ?? string.Empty,
                        Href = e.Attribute("href")?.Value ?? string.Empty,
                    });
                    break;
                case "mj-style":
                    styles.Styles.Add(new EmailStyle
                    {
                        Css = e.Value,
                Inline = string.Equals(e.Attribute("inline")?.Value, "inline", StringComparison.Ordinal),
                    });
                    break;
                case "mj-attributes": ImportAttributes(e, styles.Attributes); break;
                case "mj-html-attributes": ImportHtmlAttributes(e, styles); break;
            }
        }
    }

    private static void ImportAttributes(XElement element, MjAttributes attributes)
    {
        foreach (var child in element.Elements())
        {
            var pairs = child.Attributes()
                .Where(a => !string.Equals(a.Name.LocalName, "name", StringComparison.Ordinal))
                .ToDictionary(a => a.Name.LocalName, a => a.Value, StringComparer.Ordinal);

            switch (child.Name.LocalName)
            {
                case "mj-all":
                    Merge(attributes.All, pairs);
                    break;
                case "mj-class":
                    var name = child.Attribute("name")?.Value;
                    if (!string.IsNullOrEmpty(name)) attributes.Classes[name] = pairs;
                    break;
                default:
                    attributes.PerTag[child.Name.LocalName] = pairs;
                    break;
            }
        }
    }

    private static void ImportHtmlAttributes(XElement element, TemplateStyles styles)
    {
        foreach (var selectorEl in element.Elements().Where(e => NameIs(e, "mj-selector")))
        {
            var selector = new MjHtmlSelector { Path = selectorEl.Attribute("path")?.Value ?? string.Empty };
            foreach (var attrEl in selectorEl.Elements().Where(e => NameIs(e, "mj-html-attribute")))
            {
                var attrName = attrEl.Attribute("name")?.Value;
                if (!string.IsNullOrEmpty(attrName)) selector.Attributes[attrName] = attrEl.Value;
            }
            styles.HtmlAttributes.Add(selector);
        }
    }

    // ── Body / layout ─────────────────────────────────────────────────────────────────────────

    private void ImportBody(XElement body, EmailTemplateDocument document)
    {
        var width = body.Attribute("width")?.Value;
        if (!string.IsNullOrEmpty(width)) document.Styles.ContentWidth = width;
        var bg = body.Attribute("background-color")?.Value;
        if (!string.IsNullOrEmpty(bg)) document.Styles.BackgroundColor = bg;

        foreach (var e in body.Elements())
            ImportBodyChild(e, document);
    }

    private void ImportBodyChild(XElement e, EmailTemplateDocument document)
    {
        switch (e.Name.LocalName)
        {
            case "mj-section":
                document.Sections.Add(MapSection(e));
                break;
            case "mj-wrapper":
                // Body-level wrapper: hoist its inner sections (the model has no body-level wrapper).
                _warnings.Add(new ImportMessage(ImportKeys.WrapperFlattened));
                foreach (var inner in e.Elements().Where(c => NameIs(c, "mj-section")))
                    document.Sections.Add(MapSection(inner));
                break;
            case "mj-include":
                ResolveInclude(e, resolved =>
                {
                    foreach (var child in resolved.Elements())
                        ImportBodyChild(child, document);
                });
                break;
            default:
                // hero / raw / unknown at body level → wrap into a section+column to fit the model.
                _warnings.Add(new ImportMessage(ImportKeys.ElementWrapped, e.Name.LocalName));
                document.Sections.Add(WrapInSection(MapBlock(e)));
                break;
        }
    }

    private EmailSection MapSection(XElement e)
    {
        var section = new EmailSection();
        var a = new AttrBag(e);
        section.BackgroundColor = a.Take("background-color");
        section.BackgroundUrl = a.Take("background-url");
        section.BackgroundPosition = a.Take("background-position");
        section.BackgroundRepeat = a.Take("background-repeat");
        section.BackgroundSize = a.Take("background-size");
        section.Border = a.Take("border");
        section.BorderRadius = a.Take("border-radius");
        section.Direction = a.Take("direction") ?? section.Direction;
        section.FullWidth = a.Take("full-width") is not null;
        section.Padding = a.Take("padding") ?? section.Padding;
        section.TextAlign = a.Take("text-align") ?? section.TextAlign;
        ApplyCommon(a, v => section.CssClass = v, section.MjClasses, section.ExtraAttributes);

        foreach (var child in e.Elements())
        {
            if (NameIs(child, "mj-column"))
                section.Columns.Add(MapColumn(child));
            else if (NameIs(child, "mj-group"))
            {
                _warnings.Add(new ImportMessage(ImportKeys.ElementWrapped, "mj-group"));
                var col = new EmailColumn();
                col.Blocks.Add(MapGroup(child));
                section.Columns.Add(col);
            }
            else if (!IsComment(child))
            {
                _warnings.Add(new ImportMessage(ImportKeys.ElementWrapped, child.Name.LocalName));
                var col = new EmailColumn();
                col.Blocks.Add(MapBlock(child));
                section.Columns.Add(col);
            }
        }
        return section;
    }

    private EmailColumn MapColumn(XElement e)
    {
        var col = new EmailColumn();
        var a = new AttrBag(e);
        col.Width = a.Take("width");
        col.VerticalAlign = a.Take("vertical-align") ?? col.VerticalAlign;
        col.BackgroundColor = a.Take("background-color");
        col.Border = a.Take("border");
        col.BorderRadius = a.Take("border-radius");
        col.Padding = a.Take("padding");
        ApplyCommon(a, v => col.CssClass = v, col.MjClasses, col.ExtraAttributes);

        ImportBlocks(e, col.Blocks);
        return col;
    }

    /// <summary>
    /// Imports the block children of a container, reattaching any <c>{{ if expr }}…{{ end }}</c>
    /// Scriban wrapper emitted around a block as that block's <see cref="EmailBlockBase.VisibleWhen"/>.
    /// </summary>
    private void ImportBlocks(XElement parent, IList<EmailBlockBase> target)
    {
        string? pendingCondition = null;
        foreach (var node in parent.Nodes())
        {
            if (node is XText text)
            {
                var match = IfRegex.Match(text.Value);
                if (match.Success) pendingCondition = match.Groups["condition"].Value.Trim();
                continue;
            }
            if (node is XElement element)
            {
                var block = MapBlock(element);
                if (pendingCondition is not null)
                {
                    block.VisibleWhen = pendingCondition;
                    pendingCondition = null;
                }
                target.Add(block);
            }
        }
    }

    private static EmailSection WrapInSection(EmailBlockBase block)
    {
        var section = new EmailSection();
        var col = new EmailColumn();
        col.Blocks.Add(block);
        section.Columns.Add(col);
        return section;
    }

    // ── Blocks ──────────────────────────────────────────────────────────────────────────────────

    private EmailBlockBase MapBlock(XElement e)
    {
        return e.Name.LocalName switch
        {
            "mj-text" => MapText(e),
            "mj-button" => MapButton(e),
            "mj-image" => MapImage(e),
            "mj-divider" => MapDivider(e),
            "mj-spacer" => MapSpacer(e),
            "mj-raw" => MapRaw(e),
            "mj-table" => MapTable(e),
            "mj-social" => MapSocial(e),
            "mj-navbar" => MapNavbar(e),
            "mj-carousel" => MapCarousel(e),
            "mj-accordion" => MapAccordion(e),
            "mj-hero" => MapHero(e),
            "mj-wrapper" => MapWrapper(e),
            "mj-group" => MapGroup(e),
            _ => MapUnknown(e),
        };
    }

    private EmailBlockBase MapUnknown(XElement e)
    {
        _warnings.Add(new ImportMessage(ImportKeys.UnknownElement, e.Name.LocalName));
        return new EmailRawBlock { Content = e.ToString(SaveOptions.DisableFormatting) };
    }

    private EmailTextBlock MapText(XElement e)
    {
        var b = new EmailTextBlock { Content = InnerMarkup(e) };
        var a = new AttrBag(e);
        b.Color = a.Take("color") ?? b.Color;
        b.FontFamily = a.Take("font-family") ?? b.FontFamily;
        b.FontSize = a.Take("font-size") ?? b.FontSize;
        b.FontStyle = a.Take("font-style");
        b.FontWeight = a.Take("font-weight");
        b.LineHeight = a.Take("line-height") ?? b.LineHeight;
        b.LetterSpacing = a.Take("letter-spacing");
        b.Height = a.Take("height");
        b.TextDecoration = a.Take("text-decoration");
        b.TextTransform = a.Take("text-transform");
        b.Align = a.Take("align") ?? b.Align;
        ApplyBlockCommon(a, b);
        return b;
    }

    private EmailButtonBlock MapButton(XElement e)
    {
        var b = new EmailButtonBlock { Text = e.Value };
        var a = new AttrBag(e);
        b.Href = a.Take("href");
        b.Rel = a.Take("rel");
        b.Target = a.Take("target") ?? b.Target;
        b.BackgroundColor = a.Take("background-color") ?? b.BackgroundColor;
        b.Color = a.Take("color") ?? b.Color;
        b.FontFamily = a.Take("font-family");
        b.FontSize = a.Take("font-size") ?? b.FontSize;
        b.FontStyle = a.Take("font-style");
        b.FontWeight = a.Take("font-weight") ?? b.FontWeight;
        b.LineHeight = a.Take("line-height") ?? b.LineHeight;
        b.LetterSpacing = a.Take("letter-spacing");
        b.TextAlign = a.Take("text-align") ?? b.TextAlign;
        b.TextDecoration = a.Take("text-decoration") ?? b.TextDecoration;
        b.TextTransform = a.Take("text-transform");
        b.Align = a.Take("align") ?? b.Align;
        b.VerticalAlign = a.Take("vertical-align") ?? b.VerticalAlign;
        b.Border = a.Take("border") ?? b.Border;
        b.BorderRadius = a.Take("border-radius") ?? b.BorderRadius;
        b.InnerPadding = a.Take("inner-padding") ?? b.InnerPadding;
        b.Width = a.Take("width");
        b.Height = a.Take("height");
        ApplyBlockCommon(a, b);
        return b;
    }

    private EmailImageBlock MapImage(XElement e)
    {
        var b = new EmailImageBlock();
        var a = new AttrBag(e);
        b.Src = a.Take("src") ?? string.Empty;
        b.Alt = a.Take("alt") ?? string.Empty;
        b.Href = a.Take("href");
        b.Rel = a.Take("rel");
        b.Target = a.Take("target") ?? b.Target;
        b.Title = a.Take("title");
        b.Align = a.Take("align") ?? b.Align;
        b.Width = a.Take("width");
        b.Height = a.Take("height");
        b.Border = a.Take("border") ?? b.Border;
        b.BorderRadius = a.Take("border-radius");
        b.FluidOnMobile = a.Take("fluid-on-mobile");
        ApplyBlockCommon(a, b);
        return b;
    }

    private EmailDividerBlock MapDivider(XElement e)
    {
        var b = new EmailDividerBlock();
        var a = new AttrBag(e);
        b.BorderColor = a.Take("border-color") ?? b.BorderColor;
        b.BorderStyle = a.Take("border-style") ?? b.BorderStyle;
        b.BorderWidth = a.Take("border-width") ?? b.BorderWidth;
        b.Width = a.Take("width") ?? b.Width;
        b.Align = a.Take("align") ?? b.Align;
        ApplyBlockCommon(a, b);
        return b;
    }

    private EmailSpacerBlock MapSpacer(XElement e)
    {
        var b = new EmailSpacerBlock();
        var a = new AttrBag(e);
        b.Height = a.Take("height") ?? b.Height;
        ApplyBlockCommon(a, b);
        return b;
    }

    private EmailRawBlock MapRaw(XElement e)
        => new() { Content = RestoreRaw(e.Value) };

    private EmailTableBlock MapTable(XElement e)
    {
        var b = new EmailTableBlock();
        var a = new AttrBag(e);
        b.Align = a.Take("align") ?? b.Align;
        b.Border = a.Take("border");
        b.CellPadding = a.Take("cellpadding") ?? b.CellPadding;
        b.CellSpacing = a.Take("cellspacing") ?? b.CellSpacing;
        b.Color = a.Take("color") ?? b.Color;
        b.FontFamily = a.Take("font-family") ?? b.FontFamily;
        b.FontSize = a.Take("font-size") ?? b.FontSize;
        b.LineHeight = a.Take("line-height") ?? b.LineHeight;
        b.TableLayout = a.Take("table-layout") ?? b.TableLayout;
        b.Width = a.Take("width") ?? b.Width;
        ApplyBlockCommon(a, b);

        foreach (var tr in e.Elements().Where(x => NameIs(x, "tr")))
        {
            var cells = tr.Elements().Where(x => x.Name.LocalName is "td" or "th").ToList();
            var row = new EmailTableRow { IsHeader = cells.Count > 0 && cells.All(c => NameIs(c, "th")) };
            foreach (var cell in cells)
            {
                int? colSpan = int.TryParse(cell.Attribute("colspan")?.Value, null, out var cs) ? cs : null;
                int? rowSpan = int.TryParse(cell.Attribute("rowspan")?.Value, null, out var rs) ? rs : null;
                row.Cells.Add(new EmailTableCell
                {
                    Text = cell.Value,
                    Align = cell.Attribute("align")?.Value,
                    ColSpan = colSpan,
                    RowSpan = rowSpan,
                });
            }
            b.Rows.Add(row);
        }
        return b;
    }

    private EmailSocialBlock MapSocial(XElement e)
    {
        var b = new EmailSocialBlock();
        var a = new AttrBag(e);
        b.Mode = a.Take("mode") ?? b.Mode;
        b.Align = a.Take("align") ?? b.Align;
        b.IconSize = a.Take("icon-size") ?? b.IconSize;
        b.BorderRadius = a.Take("border-radius") ?? b.BorderRadius;
        b.Color = a.Take("color") ?? b.Color;
        b.FontSize = a.Take("font-size") ?? b.FontSize;
        b.FontFamily = a.Take("font-family");
        b.LineHeight = a.Take("line-height") ?? b.LineHeight;
        b.TextPadding = a.Take("text-padding") ?? b.TextPadding;
        b.TextDecoration = a.Take("text-decoration") ?? b.TextDecoration;
        ApplyBlockCommon(a, b);

        foreach (var el in e.Elements().Where(x => NameIs(x, "mj-social-element")))
        {
            b.Elements.Add(new EmailSocialElement
            {
                Name = el.Attribute("name")?.Value,
                Href = el.Attribute("href")?.Value,
                Src = el.Attribute("src")?.Value,
                Alt = el.Attribute("alt")?.Value ?? string.Empty,
                Target = el.Attribute("target")?.Value ?? "_blank",
                BackgroundColor = el.Attribute("background-color")?.Value,
                Label = string.IsNullOrEmpty(el.Value) ? null : el.Value,
            });
        }
        return b;
    }

    private EmailNavbarBlock MapNavbar(XElement e)
    {
        var b = new EmailNavbarBlock();
        var a = new AttrBag(e);
        b.Align = a.Take("align") ?? b.Align;
        b.BaseUrl = a.Take("base-url");
        b.Hamburger = a.Take("hamburger");
        ApplyBlockCommon(a, b);

        foreach (var el in e.Elements().Where(x => NameIs(x, "mj-navbar-link")))
        {
            b.Links.Add(new EmailNavbarLink
            {
                Text = el.Value,
                Href = el.Attribute("href")?.Value,
                Rel = el.Attribute("rel")?.Value,
                Target = el.Attribute("target")?.Value ?? "_blank",
                Color = el.Attribute("color")?.Value ?? "#000000",
                FontFamily = el.Attribute("font-family")?.Value,
                FontSize = el.Attribute("font-size")?.Value ?? "13px",
                FontWeight = el.Attribute("font-weight")?.Value ?? "normal",
                LineHeight = el.Attribute("line-height")?.Value ?? "22px",
                TextDecoration = el.Attribute("text-decoration")?.Value ?? "none",
                TextTransform = el.Attribute("text-transform")?.Value ?? "uppercase",
                Padding = el.Attribute("padding")?.Value ?? "15px 10px",
            });
        }
        return b;
    }

    private EmailCarouselBlock MapCarousel(XElement e)
    {
        var b = new EmailCarouselBlock();
        var a = new AttrBag(e);
        b.Align = a.Take("align") ?? b.Align;
        b.BorderRadius = a.Take("border-radius") ?? b.BorderRadius;
        b.IconWidth = a.Take("icon-width") ?? b.IconWidth;
        b.LeftIcon = a.Take("left-icon");
        b.RightIcon = a.Take("right-icon");
        b.Thumbnails = a.Take("thumbnails") ?? b.Thumbnails;
        b.TbBorderRadius = a.Take("tb-border-radius") ?? b.TbBorderRadius;
        ApplyBlockCommon(a, b);

        foreach (var el in e.Elements().Where(x => NameIs(x, "mj-carousel-image")))
        {
            b.Images.Add(new EmailCarouselImage
            {
                Src = el.Attribute("src")?.Value ?? string.Empty,
                Alt = el.Attribute("alt")?.Value ?? string.Empty,
                Href = el.Attribute("href")?.Value,
                Rel = el.Attribute("rel")?.Value,
                Target = el.Attribute("target")?.Value ?? "_blank",
                Title = el.Attribute("title")?.Value,
                ThumbnailsSrc = el.Attribute("thumbnails-src")?.Value,
            });
        }
        return b;
    }

    private EmailAccordionBlock MapAccordion(XElement e)
    {
        var b = new EmailAccordionBlock();
        var a = new AttrBag(e);
        b.Border = a.Take("border") ?? b.Border;
        b.IconAlign = a.Take("icon-align") ?? b.IconAlign;
        b.IconPosition = a.Take("icon-position") ?? b.IconPosition;
        b.IconHeight = a.Take("icon-height") ?? b.IconHeight;
        b.IconWidth = a.Take("icon-width") ?? b.IconWidth;
        b.IconWrappedUrl = a.Take("icon-wrapped-url");
        b.IconUnwrappedUrl = a.Take("icon-unwrapped-url");
        b.FontFamily = a.Take("font-family");
        ApplyBlockCommon(a, b);

        foreach (var el in e.Elements().Where(x => NameIs(x, "mj-accordion-element")))
        {
            var title = el.Elements().FirstOrDefault(x => NameIs(x, "mj-accordion-title"));
            var text = el.Elements().FirstOrDefault(x => NameIs(x, "mj-accordion-text"));
            b.Items.Add(new EmailAccordionItem
            {
                Title = title?.Value ?? string.Empty,
                Content = text is null ? string.Empty : InnerMarkup(text),
                BackgroundColor = el.Attribute("background-color")?.Value,
                TitleColor = title?.Attribute("color")?.Value,
            });
        }
        return b;
    }

    private EmailHeroBlock MapHero(XElement e)
    {
        var b = new EmailHeroBlock();
        var a = new AttrBag(e);
        b.Mode = a.Take("mode") ?? b.Mode;
        b.Height = a.Take("height") ?? b.Height;
        b.BackgroundColor = a.Take("background-color") ?? b.BackgroundColor;
        b.BackgroundUrl = a.Take("background-url");
        b.BackgroundWidth = a.Take("background-width");
        b.BackgroundHeight = a.Take("background-height");
        b.BackgroundPosition = a.Take("background-position") ?? b.BackgroundPosition;
        b.VerticalAlign = a.Take("vertical-align") ?? b.VerticalAlign;
        ApplyBlockCommon(a, b);

        ImportBlocks(e, b.Blocks);
        return b;
    }

    private EmailWrapperBlock MapWrapper(XElement e)
    {
        var b = new EmailWrapperBlock();
        var a = new AttrBag(e);
        b.BackgroundColor = a.Take("background-color");
        b.BackgroundUrl = a.Take("background-url");
        b.Border = a.Take("border");
        b.BorderRadius = a.Take("border-radius");
        b.TextAlign = a.Take("text-align") ?? b.TextAlign;
        b.FullWidth = a.Take("full-width") is not null;
        ApplyBlockCommon(a, b);

        foreach (var child in e.Elements().Where(c => NameIs(c, "mj-section")))
            b.Sections.Add(MapSection(child));
        return b;
    }

    private EmailGroupBlock MapGroup(XElement e)
    {
        var b = new EmailGroupBlock();
        var a = new AttrBag(e);
        b.BackgroundColor = a.Take("background-color");
        b.Direction = a.Take("direction") ?? b.Direction;
        b.VerticalAlign = a.Take("vertical-align") ?? b.VerticalAlign;
        b.Width = a.Take("width");
        ApplyBlockCommon(a, b);

        foreach (var child in e.Elements().Where(c => NameIs(c, "mj-column")))
            b.Columns.Add(MapColumn(child));
        return b;
    }

    private void ResolveInclude(XElement e, Action<XElement> onResolved)
    {
        var path = e.Attribute("path")?.Value ?? e.Attribute("src")?.Value;
        var content = path is null ? null : _includes?.Resolve(path);
        if (content is null)
        {
            _warnings.Add(new ImportMessage(ImportKeys.IncludeUnresolved, path));
            return;
        }

        try
        {
            // Wrap the fragment so multiple top-level elements parse under a single root.
            var fragment = XElement.Parse("<mj-include-fragment>" + ShimRaw(content) + "</mj-include-fragment>",
                LoadOptions.PreserveWhitespace);
            onResolved(fragment);
        }
        catch (XmlException)
        {
            _warnings.Add(new ImportMessage(ImportKeys.IncludeUnresolved, path));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────

    private void ApplyBlockCommon(AttrBag a, EmailBlockBase block)
    {
        block.Padding = a.Take("padding") ?? block.Padding;
        block.PaddingTop = a.Take("padding-top");
        block.PaddingRight = a.Take("padding-right");
        block.PaddingBottom = a.Take("padding-bottom");
        block.PaddingLeft = a.Take("padding-left");
        block.ContainerBackgroundColor = a.Take("container-background-color");
        ApplyCommon(a, v => block.CssClass = v, block.MjClasses, block.ExtraAttributes);
    }

    private static void ApplyCommon(AttrBag a, Action<string?> setCss, IList<string> mjClasses, IDictionary<string, string> extra)
    {
        setCss(a.Take("css-class"));
        var mj = a.Take("mj-class");
        if (!string.IsNullOrEmpty(mj))
            foreach (var mjClass in mj.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                mjClasses.Add(mjClass);
        foreach (var (name, value) in a.Rest())
            extra[name] = value;
    }

    private static void Merge(IDictionary<string, string> target, IReadOnlyDictionary<string, string> source)
    {
        foreach (var (k, v) in source) target[k] = v;
    }

    private static bool NameIs(XElement e, string local) => string.Equals(e.Name.LocalName, local, StringComparison.Ordinal);

    private static bool IsComment(XElement e) => false; // comments are XComment nodes, never XElement

    private static string InnerMarkup(XElement e)
        => string.Concat(e.Nodes().Select(n => n.ToString(SaveOptions.DisableFormatting))).Trim();

    [GeneratedRegex(@"(?<open><mj-raw\b[^>]*>)(?<content>.*?)(?<close></mj-raw>)", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex RawRegex { get; }

    [GeneratedRegex(RawTokenPrefix + @"(?<index>\d+)" + RawTokenSuffix, RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex RawTokenRegex { get; }

    [GeneratedRegex(@"\{\{\s*if\s+(?<condition>.+?)\s*\}\}", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex IfRegex { get; }

    /// <summary>Mutable attribute lookup that tracks which attributes have been consumed.</summary>
    private sealed class AttrBag
    {
        private readonly Dictionary<string, string> _attrs;

        public AttrBag(XElement element)
            => _attrs = element.Attributes()
                .Where(a => !a.IsNamespaceDeclaration)
                .ToDictionary(a => a.Name.LocalName, a => a.Value, StringComparer.Ordinal);

        public string? Take(string name)
        {
            if (_attrs.TryGetValue(name, out var value)) { _attrs.Remove(name); return value; }
            return null;
        }

        public IEnumerable<KeyValuePair<string, string>> Rest() => _attrs;
    }
}
