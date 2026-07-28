namespace WinBridge.Models;

public sealed record OperationResult(bool IsSuccess, string UserMessage, string? TechnicalDetails = null)
{
    public static OperationResult Success(string message) => new(true, L.T(message));
    public static OperationResult Failure(string message, string? details = null) => new(false, L.T(message), details);
}

public sealed record OperationResult<T>(bool IsSuccess, T? Value, string UserMessage, string? TechnicalDetails = null)
{
    public static OperationResult<T> Success(T value, string message = "") =>
        new(true, LocalizeValue(value), L.T(message));
    public static OperationResult<T> Failure(string message, string? details = null) =>
        new(false, default, L.T(message), details);

    private static T LocalizeValue(T value)
    {
        if (value is string text)
            return (T)(object)L.T(text);
        return value;
    }
}
