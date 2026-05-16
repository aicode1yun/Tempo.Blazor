using System.Net.Http.Json;

namespace Tempo.Blazor.E2E;

internal static class DocumentEditorE2EReset
{
    public static async Task ResetAsync()
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:5100")
        };

        var response = await http.PostAsync("api/document-editor/reset", JsonContent.Create(new { }));
        response.EnsureSuccessStatusCode();
    }
}
