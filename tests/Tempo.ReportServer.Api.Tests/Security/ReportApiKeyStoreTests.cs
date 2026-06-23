using Tempo.ReportServer.Api.Security;

namespace Tempo.ReportServer.Api.Tests.Security;

public sealed class ReportApiKeyStoreTests
{
    [Fact]
    public async Task ApiKeyStore_GeneratesTenantApplicationScopedKeysAndHidesSecretMaterial()
    {
        var store = new InMemoryReportApiKeyStore();

        var created = await store.CreateAsync("tenant-a", "embedded-app", ReportPermission.Render);
        var descriptor = await store.ValidateAsync(created.PlainTextKey);

        created.PlainTextKey.Should().StartWith("tmr_");
        created.PlainTextKey.Should().NotContain(created.KeyId);
        descriptor.Should().NotBeNull();
        descriptor!.TenantId.Should().Be("tenant-a");
        descriptor.ApplicationId.Should().Be("embedded-app");
        descriptor.Permissions.Should().Be(ReportPermission.Render);
        descriptor.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task ApiKeyStore_RejectsUnknownAndRevokedKeys()
    {
        var store = new InMemoryReportApiKeyStore();
        var created = await store.CreateAsync("tenant-a", "embedded-app", ReportPermission.Render);

        await store.RevokeAsync(created.KeyId, "tenant-a", "admin");

        (await store.ValidateAsync("tmr_missing")).Should().BeNull();
        (await store.ValidateAsync(created.PlainTextKey)).Should().BeNull();
        var stored = await store.GetAsync(created.KeyId, "tenant-a");
        stored!.RevokedByUserId.Should().Be("admin");
    }
}
