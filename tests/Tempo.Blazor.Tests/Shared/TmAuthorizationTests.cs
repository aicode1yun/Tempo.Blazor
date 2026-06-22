using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Tests.Shared;

public class TmAuthorizationTests
{
    [Fact]
    public void Request_Create_Normalizes_Action_Groups_And_Entity()
    {
        var request = TmAuthorizationRequest.Create(
            new TmUserRef { Id = "alice", DisplayName = "Alice" },
            " edit ",
            TmEntityRef.Create(" notion-page ", " page-1 "),
            ["admins", "ADMINS", " editors "]);

        request.IsValid.Should().BeTrue();
        request.Action.Should().Be(TmAuthorizationActions.Edit);
        request.EntityRef.EntityType.Should().Be("notion-page");
        request.EntityRef.EntityId.Should().Be("page-1");
        request.GroupIds.Should().Equal("admins", "editors");
    }

    [Fact]
    public void Result_Factories_Create_Allow_And_Deny_Results()
    {
        TmAuthorizationResult.Allow("owner").Allowed.Should().BeTrue();
        TmAuthorizationResult.Deny("readonly").Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task ServiceCollectionExtensions_RegisterAuthorizationProvider()
    {
        var provider = new StaticAuthorizationProvider(true);
        var services = new ServiceCollection()
            .AddTmAuthorizationProvider(provider);

        using var serviceProvider = services.BuildServiceProvider();

        var resolved = serviceProvider.GetRequiredService<ITmAuthorizationProvider>();
        resolved.Should().BeSameAs(provider);

        var result = await resolved.AuthorizeAsync(TmAuthorizationRequest.Create(
            new TmUserRef { Id = "alice", DisplayName = "Alice" },
            TmAuthorizationActions.View,
            TmEntityRef.Create("document", "doc-1")));

        result.Allowed.Should().BeTrue();
    }

    private sealed class StaticAuthorizationProvider(bool allowed) : ITmAuthorizationProvider
    {
        public ValueTask<TmAuthorizationResult> AuthorizeAsync(
            TmAuthorizationRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(allowed
                ? TmAuthorizationResult.Allow()
                : TmAuthorizationResult.Deny());
    }
}
