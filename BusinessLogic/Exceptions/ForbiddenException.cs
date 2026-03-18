namespace BusinessLogic.Exceptions;

/// <summary>
/// Exception when user lacks permission for an action (authorization failure).
/// </summary>
public class ForbiddenException : BusinessException
{
    public ForbiddenException(string message = "You do not have permission to perform this action.")
        : base(message, "FORBIDDEN")
    {
    }
}
