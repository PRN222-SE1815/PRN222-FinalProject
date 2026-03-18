namespace BusinessLogic.DTOs.Responses.Chat;

/// <summary>
/// Generic operation result for service methods.
/// Prefer returning this over throwing for normal business errors.
/// </summary>
public class OperationResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public static OperationResult Ok(string? message = null)
        => new() { Success = true, Message = message };

    public static OperationResult Fail(string message, string? errorCode = null)
        => new() { Success = false, Message = message, ErrorCode = errorCode };

    public static OperationResult<T> Ok<T>(T data, string? message = null)
        => new() { Success = true, Data = data, Message = message };

    public static OperationResult<T> Fail<T>(string message, string? errorCode = null)
        => new() { Success = false, Message = message, ErrorCode = errorCode };
}

/// <summary>
/// Generic operation result carrying a typed payload.
/// </summary>
public class OperationResult<T> : OperationResult
{
    public T? Data { get; init; }
}
