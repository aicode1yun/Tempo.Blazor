using System.Net;
using System.Net.Http.Json;
using Tempo.Blazor.EmailTemplates.Abstractions.Dtos;

namespace Tempo.Blazor.Demo.Services;

/// <summary>The result of a send request, surfacing the HTTP status and any errors.</summary>
/// <param name="Success">Whether the send was accepted.</param>
/// <param name="StatusCode">The HTTP status code returned.</param>
/// <param name="Errors">Validation or render error messages, when the send failed.</param>
public sealed record SendEmailResult(bool Success, int StatusCode, IReadOnlyList<string> Errors);

/// <summary>Typed HTTP client for the demo email template API.</summary>
public interface IEmailTemplateApiClient
{
    /// <summary>Lists all templates.</summary>
    Task<IReadOnlyList<EmailTemplateSummaryDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a template, or <see langword="null"/> if it does not exist.</summary>
    Task<EmailTemplateDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a template.</summary>
    Task<EmailTemplateDetailDto> CreateAsync(CreateEmailTemplateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates a template. Returns whether it existed.</summary>
    Task<bool> UpdateAsync(Guid id, UpdateEmailTemplateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes a template. Returns whether it existed.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Renders a preview of the given content with optional variable data.</summary>
    Task<RenderPreviewResponse> PreviewAsync(RenderPreviewRequest request, CancellationToken cancellationToken = default);

    /// <summary>Checks whether a template name is available.</summary>
    Task<bool> IsNameAvailableAsync(string name, Guid? excludingId = null, CancellationToken cancellationToken = default);

    /// <summary>Renders and sends a stored template to recipients.</summary>
    Task<SendEmailResult> SendAsync(Guid id, SendEmailRequest request, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IEmailTemplateApiClient" />
public sealed class EmailTemplateApiClient : IEmailTemplateApiClient
{
    private const string Root = "/api/email-templates";
    private readonly IHttpClientFactory _factory;

    /// <summary>Initializes the client with the demo API HTTP client factory.</summary>
    public EmailTemplateApiClient(IHttpClientFactory factory) => _factory = factory;

    private HttpClient Client => _factory.CreateClient("DemoApi");

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailTemplateSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
        => await Client.GetFromJsonAsync<List<EmailTemplateSummaryDto>>(Root, cancellationToken) ?? new();

    /// <inheritdoc />
    public async Task<EmailTemplateDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Client.GetAsync($"{Root}/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EmailTemplateDetailDto>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EmailTemplateDetailDto> CreateAsync(CreateEmailTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var response = await Client.PostAsJsonAsync(Root, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EmailTemplateDetailDto>(cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Guid id, UpdateEmailTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var response = await Client.PutAsJsonAsync($"{Root}/{id}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Client.DeleteAsync($"{Root}/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public async Task<RenderPreviewResponse> PreviewAsync(RenderPreviewRequest request, CancellationToken cancellationToken = default)
    {
        var response = await Client.PostAsJsonAsync($"{Root}/preview", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RenderPreviewResponse>(cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task<bool> IsNameAvailableAsync(string name, Guid? excludingId = null, CancellationToken cancellationToken = default)
    {
        var url = $"{Root}/name-available?name={Uri.EscapeDataString(name)}";
        if (excludingId is { } id) url += $"&excludingId={id}";
        return await Client.GetFromJsonAsync<bool>(url, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SendEmailResult> SendAsync(Guid id, SendEmailRequest request, CancellationToken cancellationToken = default)
    {
        var response = await Client.PostAsJsonAsync($"{Root}/{id}/send", request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return new SendEmailResult(true, (int)response.StatusCode, Array.Empty<string>());

        var errors = await ReadErrorsAsync(response, cancellationToken);
        return new SendEmailResult(false, (int)response.StatusCode, errors);
    }

    private static async Task<IReadOnlyList<string>> ReadErrorsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            // 422 render errors come back as an array of RenderErrorDto; 400 as a validation ProblemDetails.
            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                var renderErrors = await response.Content.ReadFromJsonAsync<List<RenderErrorDto>>(cancellationToken);
                return renderErrors?.Select(e => e.Message).ToList() ?? new();
            }
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemPayload>(cancellationToken);
            return problem?.Errors?.SelectMany(kv => kv.Value).ToList() ?? new() { response.ReasonPhrase ?? "Error" };
        }
        catch
        {
            return new List<string> { response.ReasonPhrase ?? "Error" };
        }
    }

    private sealed class ValidationProblemPayload
    {
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}
