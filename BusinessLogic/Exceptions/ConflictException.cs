namespace BusinessLogic.Exceptions;

/// <summary>
/// Exception for conflicts (e.g., duplicate attempt, concurrency issues).
/// </summary>
public class ConflictException : BusinessException
{
    public ConflictException(string message) : base(message, "CONFLICT")
    {
    }
}
