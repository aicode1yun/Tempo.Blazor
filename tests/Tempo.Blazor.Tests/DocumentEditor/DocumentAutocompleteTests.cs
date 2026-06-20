using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Tests.DocumentEditor;

public class DocumentAutocompleteTests
{
    [Fact]
    public void DefaultTriggers_DefineMarkersLimitsAndRenderers()
    {
        DocumentAutocompleteTrigger.Token().Marker.Should().Be("{{");
        DocumentAutocompleteTrigger.Token().MarkerType.Should().Be("tokenQuery");
        DocumentAutocompleteTrigger.Mention().Marker.Should().Be("@");
        DocumentAutocompleteTrigger.Tag().MinimumCharacters.Should().Be(1);
        DocumentAutocompleteTrigger.SlashCommand().RendererKey.Should().Be("command");
    }

    [Fact]
    public async Task TokenAdapter_MapsTokenProviderToAutocompleteItems()
    {
        var provider = new TokenDocumentAutocompleteProvider(new TestTokenProvider());

        var result = await provider.SearchAsync(new DocumentAutocompleteRequest
        {
            Trigger = DocumentAutocompleteTrigger.Token(),
            Query = "client",
            Limit = 5,
            Sequence = 7
        });

        result.Sequence.Should().Be(7);
        result.Items.Should().ContainSingle();
        var item = result.Items[0];
        item.Kind.Should().Be(DocumentAutocompleteKind.Token);
        item.Label.Should().Be("Client name");
        item.Metadata["key"].Should().Be("client.name");
        item.Metadata["typeLabel"].Should().Be("Text");
    }

    [Fact]
    public async Task MentionAdapter_MapsMentionProviderToAutocompleteItems()
    {
        var provider = new MentionDocumentAutocompleteProvider(new TestMentionProvider());

        var result = await provider.SearchAsync(new DocumentAutocompleteRequest
        {
            Trigger = DocumentAutocompleteTrigger.Mention(),
            Query = "alex",
            Limit = 5
        });

        result.Items.Should().ContainSingle();
        var item = result.Items[0];
        item.Kind.Should().Be(DocumentAutocompleteKind.Mention);
        item.Label.Should().Be("Alex Johnson");
        item.Description.Should().Be("alex");
        item.Metadata["avatarUrl"].Should().Be("https://example.test/alex.png");
    }

    [Fact]
    public async Task SlashProvider_FiltersCommandsByLabelAndCommandId()
    {
        var provider = new DocumentSlashCommandAutocompleteProvider(
        [
            Command("insertTable", "Table"),
            Command("insertImage", "Image"),
            Command("insertPageBreak", "Page break")
        ]);

        var result = await provider.SearchAsync(new DocumentAutocompleteRequest
        {
            Trigger = DocumentAutocompleteTrigger.SlashCommand(),
            Query = "page",
            Limit = 8
        });

        result.Items.Should().ContainSingle(item => item.Id == "insertPageBreak");
    }

    [Fact]
    public async Task Runner_CancelsOlderRequestAndDiscardsOutOfOrderResponse()
    {
        using var runner = new DocumentAutocompleteProviderRunner();
        var provider = new ControlledAutocompleteProvider();

        var first = runner.SearchAsync(provider, Request("a"), "warning");
        await provider.WaitForRequestCountAsync(1);

        var second = runner.SearchAsync(provider, Request("ab"), "warning");
        await provider.WaitForRequestCountAsync(2);

        provider.Requests[0].CancellationToken.IsCancellationRequested.Should().BeTrue();
        provider.Complete(0, [Command("old", "Old")]);
        provider.Complete(1, [Command("new", "New")]);

        (await first).IsStale.Should().BeTrue();
        var secondResult = await second;
        secondResult.IsStale.Should().BeFalse();
        secondResult.Items.Should().ContainSingle(item => item.Id == "new");
        runner.LatestAppliedSequence.Should().Be(2);
    }

    [Fact]
    public async Task Runner_ReturnsWarningWhenProviderFails()
    {
        using var runner = new DocumentAutocompleteProviderRunner();
        var provider = new ThrowingAutocompleteProvider();

        var result = await runner.SearchAsync(provider, Request("bad"), "Provider warning");

        result.WarningMessage.Should().Be("Provider warning");
        result.Items.Should().BeEmpty();
    }

    private static DocumentAutocompleteRequest Request(string query) => new()
    {
        Trigger = DocumentAutocompleteTrigger.Token(),
        Query = query,
        Limit = 8
    };

    private static DocumentAutocompleteItem Command(string id, string label) => new()
    {
        Id = id,
        Label = label,
        Kind = DocumentAutocompleteKind.SlashCommand,
        Metadata = { ["command"] = id }
    };

    private sealed class TestTokenProvider : ITokenDataProvider
    {
        public bool SupportsCreation => false;

        public void Refresh()
        {
        }

        public Task<IEnumerable<IToken>> SearchTokensAsync(string query, CancellationToken ct = default)
        {
            IEnumerable<IToken> tokens =
            [
                new TestToken
                {
                    Key = "client.name",
                    DisplayName = "Client name",
                    Description = "Client display name",
                    Category = "Client",
                    TypeLabel = "Text"
                }
            ];

            return Task.FromResult(tokens);
        }
    }

    private sealed class TestMentionProvider : IMentionDataProvider
    {
        public Task<IEnumerable<IMentionUser>> SearchUsersAsync(string query, CancellationToken ct = default)
        {
            IEnumerable<IMentionUser> users =
            [
                new TestMentionUser
                {
                    Id = "u1",
                    UserName = "alex",
                    DisplayName = "Alex Johnson",
                    AvatarUrl = "https://example.test/alex.png"
                }
            ];

            return Task.FromResult(users);
        }
    }

    private sealed class TestToken : IToken
    {
        public string Key { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public string? Description { get; init; }

        public string? Category { get; init; }

        public string? Icon { get; init; }

        public string? ColorClass { get; init; }

        public string? TypeLabel { get; init; }
    }

    private sealed class TestMentionUser : IMentionUser
    {
        public string Id { get; init; } = string.Empty;

        public string UserName { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public string? AvatarUrl { get; init; }
    }

    private sealed class ControlledAutocompleteProvider : IDocumentAutocompleteProvider
    {
        private readonly List<TaskCompletionSource<IReadOnlyList<DocumentAutocompleteItem>>> _responses = [];
        private readonly TaskCompletionSource _secondRequestSeen = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<(DocumentAutocompleteRequest Request, CancellationToken CancellationToken)> Requests { get; } = [];

        public Task WaitForRequestCountAsync(int count)
        {
            return Requests.Count >= count ? Task.CompletedTask : _secondRequestSeen.Task;
        }

        public Task<DocumentAutocompleteResult> SearchAsync(
            DocumentAutocompleteRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = new TaskCompletionSource<IReadOnlyList<DocumentAutocompleteItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
            Requests.Add((request, cancellationToken));
            _responses.Add(response);
            if (Requests.Count >= 2)
            {
                _secondRequestSeen.TrySetResult();
            }

            return CompleteAsync(request.Sequence, response.Task);
        }

        public void Complete(int index, IReadOnlyList<DocumentAutocompleteItem> items)
        {
            _responses[index].SetResult(items);
        }

        private static async Task<DocumentAutocompleteResult> CompleteAsync(
            long sequence,
            Task<IReadOnlyList<DocumentAutocompleteItem>> itemsTask)
        {
            return new DocumentAutocompleteResult
            {
                Sequence = sequence,
                Items = await itemsTask
            };
        }
    }

    private sealed class ThrowingAutocompleteProvider : IDocumentAutocompleteProvider
    {
        public Task<DocumentAutocompleteResult> SearchAsync(
            DocumentAutocompleteRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Provider failed");
        }
    }
}
