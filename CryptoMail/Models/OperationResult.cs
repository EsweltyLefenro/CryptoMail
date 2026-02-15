namespace CryptoMail.Models;

public sealed class OperationResult
{
    public bool IsSuccess { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public Exception? Exception { get; private set; }

    public static OperationResult Success(string message) => new()
    {
        IsSuccess = true,
        Message = message
    };

    public static OperationResult Failure(string message, Exception? exception = null) => new()
    {
        IsSuccess = false,
        Message = message,
        Exception = exception
    };
}
