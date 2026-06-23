namespace Tempo.Reporting.Engine.Processing;

/// <summary>Exception thrown by the report processing pipeline.</summary>
public sealed class ReportProcessingException : Exception
{
    /// <summary>Creates a processing exception.</summary>
    public ReportProcessingException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Creates a processing exception with an inner exception.</summary>
    public ReportProcessingException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Stable processing error code.</summary>
    public string Code { get; }
}
