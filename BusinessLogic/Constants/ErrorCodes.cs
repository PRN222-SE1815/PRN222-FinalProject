namespace BusinessLogic.Constants;

public static class ErrorCodes
{
    public const string InvalidInput = "INVALID_INPUT";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string GradebookNotFound = "GRADEBOOK_NOT_FOUND";
    public const string InvalidState = "INVALID_STATE";
    public const string ItemNotFound = "ITEM_NOT_FOUND";
    public const string InternalError = "INTERNAL_ERROR";

    // AI module
    public const string SessionNotFound = "SESSION_NOT_FOUND";
    public const string InvalidTool = "INVALID_TOOL";
    public const string ToolExecutionError = "TOOL_EXECUTION_ERROR";
    public const string RateLimited = "RATE_LIMITED";
    public const string AiProviderError = "AI_PROVIDER_ERROR";
    public const string SafeGuardTriggered = "SAFE_GUARD_TRIGGERED";
    public const string InvalidAiSchema = "INVALID_AI_SCHEMA";
}
