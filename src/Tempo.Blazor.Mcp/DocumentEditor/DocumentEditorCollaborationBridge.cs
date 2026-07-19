using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Mcp.DocumentEditor;

/// <summary>
/// Opt-in live co-editing configuration for the document MCP tools: when enabled, every
/// operation batch a semantic tool applies is also published to the host collaboration stream —
/// the agent appears as a named participant (name + presence color) and humans with the document
/// open see the edits live. Disabled by default; publishing is FAIL-OPEN (a broken backplane
/// never fails the edit, it is only logged).
/// </summary>
public sealed class TempoDocumentMcpCollaborationOptions
{
    /// <summary>Whether MCP edits are published to the collaboration stream. Default false.</summary>
    public bool Enabled { get; set; }

    /// <summary>Stable agent author id.</summary>
    public string AgentId { get; set; } = "mcp-agent";

    /// <summary>Agent display name shown in presence and tracked metadata.</summary>
    public string AgentName { get; set; } = "MCP Agent";

    /// <summary>Agent presence color (CSS color).</summary>
    public string AgentColor { get; set; } = "#7C3AED";

    /// <summary>Instance id stamped on backplane envelopes so the source can suppress echo.</summary>
    public string SourceInstanceId { get; set; } = $"mcp-{Guid.NewGuid():N}";

    /// <summary>
    /// Optional host hook invoked after a batch reaches the collaboration provider — e.g. a
    /// SignalR host pushes the broadcast to the document group here.
    /// </summary>
    public Func<DocumentCollaborationOperationBatch, CancellationToken, Task>? OperationPublishedCallback { get; set; }

    /// <summary>Optional host hook invoked after the agent presence cursor is broadcast.</summary>
    public Func<DocumentCollaborationCursor, CancellationToken, Task>? CursorPublishedCallback { get; set; }
}

/// <summary>Publishes MCP-applied operation batches to the host collaboration stream.</summary>
public interface IDocumentEditorMcpCollaborationBridge
{
    /// <summary>
    /// Publishes an applied batch. Returns true when at least one channel (collaboration
    /// provider or backplane) received it; false when disabled, unavailable, or failed —
    /// publishing never throws (fail-open).
    /// </summary>
    Task<bool> PublishAsync(string documentId, DocumentOperationBatch batch, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default bridge: joins the host <see cref="IDocumentCollaborationProvider"/> once per document
/// as the named agent participant and broadcasts batches + a presence cursor; additionally
/// publishes an envelope (with <see cref="TempoDocumentMcpCollaborationOptions.SourceInstanceId"/>
/// against echo) to the <see cref="IDocumentCollaborationBackplane"/> when one is registered.
/// </summary>
public sealed class TempoDocumentMcpCollaborationBridge : IDocumentEditorMcpCollaborationBridge
{
    private readonly TempoDocumentMcpCollaborationOptions _options;
    private readonly IDocumentCollaborationProvider? _provider;
    private readonly IDocumentCollaborationBackplane? _backplane;
    private readonly ILogger<TempoDocumentMcpCollaborationBridge>? _logger;
    private readonly ConcurrentDictionary<string, Task<DocumentCollaborationSession>> _sessions = new(StringComparer.Ordinal);

    /// <summary>Creates the bridge over the optionally registered collaboration services.</summary>
    public TempoDocumentMcpCollaborationBridge(
        TempoDocumentMcpCollaborationOptions options,
        IDocumentCollaborationProvider? provider = null,
        IDocumentCollaborationBackplane? backplane = null,
        ILogger<TempoDocumentMcpCollaborationBridge>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _provider = provider;
        _backplane = backplane;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> PublishAsync(string documentId, DocumentOperationBatch batch, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || (_provider is null && _backplane is null))
        {
            return false;
        }

        try
        {
            DocumentCollaborationOperationBatch? broadcast = null;
            if (_provider is not null)
            {
                var session = await GetOrJoinSessionAsync(documentId, cancellationToken);
                broadcast = await _provider.BroadcastOperationBatchAsync(session.Id, batch, cancellationToken);

                var firstTarget = batch.Operations.FirstOrDefault()?.Target;
                var cursor = new DocumentCollaborationCursor
                {
                    DocumentId = documentId,
                    SessionId = session.Id,
                    ClientId = _options.SourceInstanceId,
                    DisplayName = _options.AgentName,
                    Color = _options.AgentColor,
                    BlockId = firstTarget?.BlockId,
                    InlineIndex = firstTarget?.InlineIndex,
                    Offset = firstTarget?.Offset ?? 0
                };
                await _provider.BroadcastCursorAsync(cursor, cancellationToken);

                if (_options.OperationPublishedCallback is not null)
                {
                    await _options.OperationPublishedCallback(broadcast, cancellationToken);
                }

                if (_options.CursorPublishedCallback is not null)
                {
                    await _options.CursorPublishedCallback(cursor, cancellationToken);
                }
            }

            if (_backplane is not null)
            {
                await _backplane.PublishAsync(new DocumentCollaborationBackplaneMessage
                {
                    DocumentId = documentId,
                    SourceInstanceId = _options.SourceInstanceId,
                    Batch = broadcast ?? new DocumentCollaborationOperationBatch
                    {
                        SessionId = _options.SourceInstanceId,
                        Batch = batch
                    }
                }, cancellationToken);
            }

            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Fail-open: the edit is already saved; a broken collaboration channel must never
            // surface as a tool failure — humans simply reload to catch up.
            _logger?.LogWarning(ex,
                "MCP collaboration publish failed for document {DocumentId}; the edit is saved but was not broadcast.",
                documentId);
            _sessions.TryRemove(documentId, out _);
            return false;
        }
    }

    private Task<DocumentCollaborationSession> GetOrJoinSessionAsync(string documentId, CancellationToken cancellationToken)
    {
        return _sessions.GetOrAdd(documentId, id => _provider!.JoinAsync(new DocumentCollaborationJoinRequest
        {
            DocumentId = id,
            ClientId = _options.SourceInstanceId,
            Author = new DocumentEditorAuthor
            {
                Id = _options.AgentId,
                DisplayName = _options.AgentName
            }
        }, cancellationToken));
    }
}

/// <summary>DI registration for the opt-in MCP live co-editing bridge.</summary>
public static class TempoDocumentEditorMcpCollaborationExtensions
{
    /// <summary>
    /// Registers <see cref="TempoDocumentMcpCollaborationOptions"/> and the
    /// <see cref="IDocumentEditorMcpCollaborationBridge"/>. Publishing stays disabled until the
    /// host sets <c>options.Enabled = true</c>; the bridge resolves the collaboration provider
    /// and backplane lazily and fails open when neither is available. Idempotent.
    /// </summary>
    public static IServiceCollection AddTempoDocumentEditorMcpCollaboration(
        this IServiceCollection services,
        Action<TempoDocumentMcpCollaborationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new TempoDocumentMcpCollaborationOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);
        services.TryAddSingleton<IDocumentEditorMcpCollaborationBridge>(provider => new TempoDocumentMcpCollaborationBridge(
            provider.GetRequiredService<TempoDocumentMcpCollaborationOptions>(),
            provider.GetService<IDocumentCollaborationProvider>(),
            provider.GetService<IDocumentCollaborationBackplane>(),
            provider.GetService<ILogger<TempoDocumentMcpCollaborationBridge>>()));
        return services;
    }
}
