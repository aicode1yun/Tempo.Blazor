using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.NotionEditor.Interfaces;

/// <summary>Optional provider for Notion audit log entries.</summary>
public interface INotionAuditProvider
{
    /// <summary>Persists a single audit entry.</summary>
    Task LogAsync(AuditEntryDto entry, CancellationToken cancellationToken = default);

    /// <summary>Returns audit entries matching the requested filter and paging window.</summary>
    Task<PagedResult<AuditEntryDto>> GetEntriesAsync(
        AuditLogFilter filter,
        NotionAuditQuery paging,
        CancellationToken cancellationToken = default);
}
