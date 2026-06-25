namespace Tempo.Reporting.Abstractions.Serialization;

/// <summary>Exception thrown when report definition JSON cannot be parsed or migrated.</summary>
public sealed class ReportDefinitionJsonException : Exception
{
    /// <summary>Creates an exception.</summary>
    public ReportDefinitionJsonException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with an inner cause.</summary>
    public ReportDefinitionJsonException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
