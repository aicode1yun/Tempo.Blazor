namespace Tempo.Blazor.EmailTemplates.Abstractions.Templating;

/// <summary>A success-or-error result that avoids throwing for expected failures.</summary>
/// <typeparam name="T">The success value type.</typeparam>
public readonly struct Result<T>
{
    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    /// <summary>Gets whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets the success value (default when failed).</summary>
    public T? Value { get; }

    /// <summary>Gets the error message (null when succeeded).</summary>
    public string? Error { get; }

    /// <summary>Creates a successful result.</summary>
    public static Result<T> Success(T value) => new(isSuccess: true, value: value, error: null);

    /// <summary>Creates a failed result with an error message.</summary>
    public static Result<T> Failure(string error) => new(isSuccess: false, value: default, error: error);
}
