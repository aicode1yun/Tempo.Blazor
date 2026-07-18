namespace Tempo.ReportServer.Api.Storage;

/// <summary>
/// Raised when a permission operation targets a subject/effect combination the persistent store cannot
/// represent. The EF folder-ACL model is allow-only and user-subject-only (grants are projected onto the
/// catalog folder tree); Role/Application subjects and Deny effects have no persistent representation.
/// Callers must surface this as a client error (HTTP 400) rather than silently succeeding, which would
/// produce a false security assurance and a lying audit trail.
/// </summary>
public sealed class ReportPermissionUnsupportedException : Exception
{
    /// <summary>Creates the exception with an operator-facing explanation.</summary>
    public ReportPermissionUnsupportedException(string message)
        : base(message)
    {
    }
}
