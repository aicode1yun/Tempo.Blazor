using System.Text.Json;
using StackExchange.Redis;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Collaboration;

/// <summary>
/// Redis pub/sub implementation of <see cref="IDocumentCollaborationBackplane"/> for multi-server
/// collaboration: each document fans out on its own channel
/// (<c>tm:doc-collab:{documentId}</c>), messages travel as the same JSON envelope the in-memory
/// backplane uses (<see cref="DocumentCollaborationBackplaneMessage"/>), and the publishing
/// instance filters its own echo by <c>SourceInstanceId</c>. Wire it into
/// <see cref="BackplaneDocumentCollaborationProvider"/> on every server instance.
/// </summary>
public sealed class RedisDocumentCollaborationBackplane : IDocumentCollaborationBackplane, IAsyncDisposable
{
    private const string ChannelPrefix = "tm:doc-collab:";

    private readonly IConnectionMultiplexer _connection;
    private readonly bool _ownsConnection;

    /// <summary>Creates the backplane over an existing Redis connection (the caller keeps ownership).</summary>
    public RedisDocumentCollaborationBackplane(IConnectionMultiplexer connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _ownsConnection = false;
    }

    private RedisDocumentCollaborationBackplane(IConnectionMultiplexer connection, bool ownsConnection)
    {
        _connection = connection;
        _ownsConnection = ownsConnection;
    }

    /// <summary>Connects to Redis and creates the backplane, taking ownership of the connection.</summary>
    public static async Task<RedisDocumentCollaborationBackplane> ConnectAsync(
        string configuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await ConnectionMultiplexer.ConnectAsync(configuration);
        return new RedisDocumentCollaborationBackplane(connection, ownsConnection: true);
    }

    /// <summary>Builds the Redis channel name for a document.</summary>
    public static string GetChannelName(string documentId) => $"{ChannelPrefix}{documentId}";

    /// <inheritdoc />
    public async Task PublishAsync(DocumentCollaborationBackplaneMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        var payload = JsonSerializer.Serialize(message, DocumentEditorJson.Options);
        await _connection.GetSubscriber().PublishAsync(
            RedisChannel.Literal(GetChannelName(message.DocumentId)),
            payload);
    }

    /// <inheritdoc />
    public async Task<IAsyncDisposable> SubscribeAsync(
        string documentId,
        Func<DocumentCollaborationBackplaneMessage, Task> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        cancellationToken.ThrowIfCancellationRequested();
        var subscriber = _connection.GetSubscriber();
        var channel = RedisChannel.Literal(GetChannelName(documentId));

        async void OnMessage(RedisChannel _, RedisValue value)
        {
            try
            {
                var message = JsonSerializer.Deserialize<DocumentCollaborationBackplaneMessage>(
                    value.ToString(),
                    DocumentEditorJson.Options);
                if (message is not null)
                {
                    await handler(message);
                }
            }
            catch
            {
                // A malformed message must never kill the subscription; the next message continues.
            }
        }

        await subscriber.SubscribeAsync(channel, OnMessage);
        return new Subscription(() => subscriber.UnsubscribeAsync(channel, OnMessage));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_ownsConnection)
        {
            await _connection.DisposeAsync();
        }
    }

    private sealed class Subscription(Func<Task> unsubscribe) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await unsubscribe();
            }
            catch
            {
                // Best-effort unsubscription during teardown.
            }
        }
    }
}
