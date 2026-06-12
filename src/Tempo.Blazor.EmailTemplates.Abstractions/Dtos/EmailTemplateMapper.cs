using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Serialization;
using Tempo.Blazor.EmailTemplates.Abstractions.Templating;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Dtos;

/// <summary>Maps between the editable <see cref="EmailTemplateDocument"/> and transport DTOs.</summary>
public static class EmailTemplateMapper
{
    /// <summary>Serializes a document to its canonical content JSON.</summary>
    public static string ToContentJson(EmailTemplateDocument document) => EmailTemplateSerializer.Serialize(document);

    /// <summary>Deserializes a document from content JSON.</summary>
    public static EmailTemplateDocument ToDocument(string contentJson) => EmailTemplateSerializer.Deserialize(contentJson);

    /// <summary>Computes the distinct root variable names a document requires.</summary>
    public static IReadOnlyList<string> RequiredVariables(EmailTemplateDocument document)
        => EmailDocumentVariableExtractor.Extract(document)
            .Select(v => v.Path.Split('.')[0])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();

    /// <summary>Builds the full detail DTO from a document.</summary>
    public static EmailTemplateDetailDto ToDetailDto(EmailTemplateDocument document, bool isActive = true, string? sampleDataJson = null)
        => new()
        {
            Id = document.Id,
            Name = document.Name,
            Subject = document.Subject,
            Preheader = document.Preheader,
            Language = document.Language,
            ContentJson = ToContentJson(document),
            RequiredVariables = RequiredVariables(document),
            SampleDataJson = sampleDataJson,
            IsActive = isActive,
            UpdatedAt = document.UpdatedAt,
        };

    /// <summary>Builds the list summary DTO from a document.</summary>
    public static EmailTemplateSummaryDto ToSummaryDto(EmailTemplateDocument document, bool isActive = true)
        => new()
        {
            Id = document.Id,
            Name = document.Name,
            Subject = document.Subject,
            Language = document.Language,
            IsActive = isActive,
            UpdatedAt = document.UpdatedAt,
        };

    /// <summary>Builds a new document from a create request (content + overridden metadata).</summary>
    public static EmailTemplateDocument ApplyCreate(CreateEmailTemplateRequest request)
    {
        var document = ToDocument(request.ContentJson);
        document.Name = request.Name;
        document.Subject = request.Subject;
        document.Preheader = request.Preheader;
        document.Language = request.Language;
        return document;
    }

    /// <summary>Applies an update request's content and metadata onto a document.</summary>
    public static EmailTemplateDocument ApplyUpdate(UpdateEmailTemplateRequest request)
    {
        var document = ToDocument(request.ContentJson);
        document.Name = request.Name;
        document.Subject = request.Subject;
        document.Preheader = request.Preheader;
        document.Language = request.Language;
        document.UpdatedAt = DateTime.UtcNow;
        return document;
    }
}
