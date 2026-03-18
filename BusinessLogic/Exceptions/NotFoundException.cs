namespace BusinessLogic.Exceptions;

/// <summary>
/// Exception when a requested entity is not found.
/// </summary>
public class NotFoundException : BusinessException
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.", "NOT_FOUND")
    {
    }

    public NotFoundException(string message) : base(message, "NOT_FOUND")
    {
    }
}
