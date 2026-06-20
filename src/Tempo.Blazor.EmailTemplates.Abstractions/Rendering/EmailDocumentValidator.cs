using Tempo.Blazor.EmailTemplates.Abstractions.Layout;
using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

/// <summary>
/// Validates a document for structural and content correctness: layout (via
/// <see cref="LayoutValidator"/>) plus per-block rules such as required button hrefs and image
/// sources/alt text. Findings carry localization keys, never localized text.
/// </summary>
public sealed class EmailDocumentValidator
{
    private readonly LayoutValidator _layoutValidator = new();

    /// <summary>Validates the document and returns all findings (layout + block level).</summary>
    public IReadOnlyList<DocumentValidationMessage> Validate(EmailTemplateDocument document)
    {
        var messages = new List<DocumentValidationMessage>();

        foreach (var layout in _layoutValidator.Validate(document))
            messages.Add(new DocumentValidationMessage(layout.Severity, layout.Key, layout.Path));

        foreach (var block in DocumentTree.AllBlocks(document))
            ValidateBlock(block, messages);

        return messages;
    }

    private static void ValidateBlock(EmailBlockBase block, List<DocumentValidationMessage> messages)
    {
        switch (block)
        {
            case EmailButtonBlock button when string.IsNullOrWhiteSpace(button.Href):
                messages.Add(new(LayoutSeverity.Error, DocumentValidationKeys.ButtonHrefMissing, block.Id.ToString()));
                break;
            case EmailImageBlock image:
                if (string.IsNullOrWhiteSpace(image.Src))
                    messages.Add(new(LayoutSeverity.Error, DocumentValidationKeys.ImageSrcMissing, block.Id.ToString()));
                if (string.IsNullOrWhiteSpace(image.Alt))
                    messages.Add(new(LayoutSeverity.Warning, DocumentValidationKeys.ImageAltMissing, block.Id.ToString()));
                break;
            case EmailCarouselBlock carousel:
                foreach (var img in carousel.Images.Where(i => string.IsNullOrWhiteSpace(i.Src)))
                    messages.Add(new(LayoutSeverity.Error, DocumentValidationKeys.CarouselImageSrcMissing, block.Id.ToString()));
                break;
        }
    }
}
