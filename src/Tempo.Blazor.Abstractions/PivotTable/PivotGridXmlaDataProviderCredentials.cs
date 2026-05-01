namespace Tempo.Blazor.Abstractions.PivotTable;

/// <summary>
/// Network credentials for authenticating against an XMLA endpoint.
/// </summary>
public sealed class PivotGridXmlaDataProviderCredentials
{
    /// <summary>The username for authentication.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>The password for authentication.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>The optional Windows domain.</summary>
    public string? Domain { get; set; }
}
