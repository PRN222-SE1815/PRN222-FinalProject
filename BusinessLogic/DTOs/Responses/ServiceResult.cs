namespace BusinessLogic.DTOs.Response;

public class ServiceResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }

    public static ServiceResult Success(string message = "Success", object? data = null)
    {
        return new ServiceResult
        {
            IsSuccess = true,
            Message = message,
            Data = data
        };
    }

    public static ServiceResult Fail(string errorCode, string message, object? data = null)
    {
        return new ServiceResult
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            Message = message,
            Data = data
        };
    }
}

public sealed class ServiceResult<T>
{
    public bool IsSuccess { get; set; }
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static ServiceResult<T> Success(T? data, string message = "Success")
    {
        return new ServiceResult<T>
        {
            IsSuccess = true,
            Data = data,
            Message = message
        };
    }

    public static ServiceResult<T> Fail(string errorCode, string message, T? data = default)
    {
        return new ServiceResult<T>
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            Message = message,
            Data = data
        };
    }
}
