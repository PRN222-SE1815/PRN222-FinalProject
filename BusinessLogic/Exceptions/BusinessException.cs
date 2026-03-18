namespace BusinessLogic.Exceptions;

/// <summary>
/// Exception for business rule violations (non-critical, expected errors).
/// </summary>
public class BusinessException : Exception
{
    public string ErrorCode { get; }

    public BusinessException(string message, string? errorCode = null) : base(message)
    {
        ErrorCode = errorCode ?? "BUSINESS_ERROR";
    }

    public BusinessException(string message, Exception innerException, string? errorCode = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode ?? "BUSINESS_ERROR";
    }
}
