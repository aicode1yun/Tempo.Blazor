namespace Tempo.ReportServer.Api;

/// <summary>Host-level options for the Tempo Report Server API surface.</summary>
public sealed class ReportServerApiOptions
{
    /// <summary>
    /// When <see langword="true"/>, live catalog/render/data-source handlers proceed WITHOUT ACL
    /// enforcement or auditing for requests that resolve no security principal. This is an explicit
    /// opt-in for lightweight development/test hosts that map the API without an authentication gate.
    /// It defaults to <see langword="false"/> so the handlers fail closed (HTTP 401) on a missing
    /// principal: a production host must never rely on a null principal slipping through — e.g. a valid
    /// bearer token accompanied by a bogus <c>X-Api-Key</c> header resolves to a null principal and MUST
    /// be rejected rather than silently bypassing authorization.
    /// </summary>
    public bool AllowAnonymousOperations { get; set; }
}
