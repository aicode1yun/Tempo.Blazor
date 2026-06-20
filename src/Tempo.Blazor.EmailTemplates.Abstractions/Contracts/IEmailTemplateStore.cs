using Tempo.Blazor.EmailTemplates.Abstractions.Dtos;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Contracts;

/// <summary>
/// Persistence contract for email templates, implemented by the host application. Operations are
/// asynchronous and cancellable; mutations are expected to be atomic (unit of work).
/// </summary>
public interface IEmailTemplateStore
{
    /// <summary>Lists all templates as summaries.</summary>
    Task<IReadOnlyList<EmailTemplateSummaryDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a template's full detail, or <see langword="null"/> if it does not exist.</summary>
    Task<EmailTemplateDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new template and returns its detail.</summary>
    Task<EmailTemplateDetailDto> CreateAsync(CreateEmailTemplateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing template. Returns <see langword="false"/> if it does not exist.</summary>
    Task<bool> UpdateAsync(Guid id, UpdateEmailTemplateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a template. Returns <see langword="false"/> if it does not exist.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns whether a template name is available (optionally excluding one template).</summary>
    Task<bool> IsNameAvailableAsync(string name, Guid? excludingId = null, CancellationToken cancellationToken = default);
}
