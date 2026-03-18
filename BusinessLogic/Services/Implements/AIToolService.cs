using System.Text.Json;
using BusinessLogic.DTOs.Response;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using DataAccess.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace BusinessLogic.Services.Implements;

public sealed class AIToolService : IAIToolService
{
    private static readonly IReadOnlyList<string> SupportedTools =
    [
        "get_student_academic_snapshot",
        "get_current_enrollments",
        "get_course_catalog",
        "get_prerequisite_graph",
        "simulate_plan"
    ];

    private readonly IAIAnalyticsRepository _aiAnalyticsRepository;
    private readonly ILogger<AIToolService> _logger;

    public AIToolService(IAIAnalyticsRepository aiAnalyticsRepository, ILogger<AIToolService> logger)
    {
        _aiAnalyticsRepository = aiAnalyticsRepository;
        _logger = logger;
    }

    public IReadOnlyList<string> GetSupportedToolNames()
    {
        return SupportedTools;
    }

    public async Task<ServiceResult<string>> ExecuteToolAsync(
        int userId,
        string actorRole,
        string toolName,
        string toolArgumentsJson,
        CancellationToken ct = default)
    {
        try
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(actorRole) || string.IsNullOrWhiteSpace(toolName))
            {
                return ServiceResult<string>.Fail("INVALID_INPUT", "Invalid tool execution input.");
            }

            if (!string.Equals(actorRole, UserRole.STUDENT.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<string>.Fail("FORBIDDEN", "Only STUDENT role is supported in this phase.");
            }

            if (!SupportedTools.Contains(toolName, StringComparer.Ordinal))
            {
                _logger.LogWarning("Rejected unsupported tool {ToolName} for user {UserId}", toolName, userId);
                return ServiceResult<string>.Fail("INVALID_TOOL", "Tool name is not supported.");
            }

            var student = await _aiAnalyticsRepository.GetStudentByUserIdAsync(userId, ct);
            if (student is null)
            {
                _logger.LogWarning("Student ownership validation failed — UserId={UserId}", userId);
                return ServiceResult<string>.Fail("FORBIDDEN", "Student ownership validation failed.");
            }

            using var argsDocument = ParseArguments(toolArgumentsJson);
            var args = argsDocument?.RootElement;

            _logger.LogInformation("Executing tool {ToolName} for student {StudentId}", toolName, student.StudentId);

            object result = toolName switch
            {
                "get_student_academic_snapshot" => await ExecuteStudentAcademicSnapshotAsync(student.StudentId, args, ct),
                "get_current_enrollments" => await ExecuteCurrentEnrollmentsAsync(student.StudentId, args, ct),
                "get_course_catalog" => await ExecuteCourseCatalogAsync(student.ProgramId, args, ct),
                "get_prerequisite_graph" => await ExecutePrerequisiteGraphAsync(student.ProgramId, args, ct),
                "simulate_plan" => await ExecuteSimulatePlanAsync(student.StudentId, args, ct),
                _ => throw new InvalidOperationException("Unsupported tool")
            };

            var json = JsonSerializer.Serialize(result);
            _logger.LogInformation("Tool {ToolName} completed for student {StudentId}, result length={Length}",
                toolName, student.StudentId, json.Length);
            return ServiceResult<string>.Success(json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse tool arguments for tool {ToolName}", toolName);
            return ServiceResult<string>.Fail("INVALID_INPUT", "Invalid JSON tool arguments.");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid tool arguments for tool {ToolName}", toolName);
            return ServiceResult<string>.Fail("INVALID_INPUT", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution failed for tool {ToolName}, user {UserId}", toolName, userId);
            return ServiceResult<string>.Fail("TOOL_EXECUTION_ERROR", "Tool execution failed due to a system error.");
        }
    }

    private static JsonDocument? ParseArguments(string? toolArgumentsJson)
    {
        if (string.IsNullOrWhiteSpace(toolArgumentsJson))
        {
            return null;
        }

        return JsonDocument.Parse(toolArgumentsJson);
    }

    private async Task<object> ExecuteStudentAcademicSnapshotAsync(int studentId, JsonElement? args, CancellationToken ct)
    {
        var semesterId = GetNullableInt(args, "semesterId");
        return await _aiAnalyticsRepository.GetStudentAcademicSnapshotAsync(studentId, semesterId, ct);
    }

    private async Task<object> ExecuteCurrentEnrollmentsAsync(int studentId, JsonElement? args, CancellationToken ct)
    {
        var semesterId = GetNullableInt(args, "semesterId");
        return await _aiAnalyticsRepository.GetCurrentEnrollmentsAsync(studentId, semesterId, ct);
    }

    private async Task<object> ExecuteCourseCatalogAsync(int? studentProgramId, JsonElement? args, CancellationToken ct)
    {
        var semesterId = GetRequiredInt(args, "semesterId");
        var programId = GetNullableInt(args, "programId") ?? studentProgramId;
        return await _aiAnalyticsRepository.GetCourseCatalogAsync(programId, semesterId, ct);
    }

    private async Task<object> ExecutePrerequisiteGraphAsync(int? studentProgramId, JsonElement? args, CancellationToken ct)
    {
        var programId = GetNullableInt(args, "programId") ?? studentProgramId;
        return await _aiAnalyticsRepository.GetPrerequisiteGraphAsync(programId, ct);
    }

    private async Task<object> ExecuteSimulatePlanAsync(int studentId, JsonElement? args, CancellationToken ct)
    {
        var semesterId = GetRequiredInt(args, "semesterId");
        var candidateCourseIds = GetRequiredIntArray(args, "candidateCourseIds");
        return await _aiAnalyticsRepository.GetPlanConstraintDataAsync(studentId, semesterId, candidateCourseIds, ct);
    }

    private static int GetRequiredInt(JsonElement? args, string propertyName)
    {
        var value = GetNullableInt(args, propertyName);
        if (!value.HasValue)
        {
            throw new ArgumentException($"Missing required integer argument: {propertyName}");
        }

        return value.Value;
    }

    private static int? GetNullableInt(JsonElement? args, string propertyName)
    {
        if (!args.HasValue || args.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!args.Value.TryGetProperty(propertyName, out var valueElement))
        {
            return null;
        }

        return valueElement.ValueKind switch
        {
            JsonValueKind.Number when valueElement.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.String when int.TryParse(valueElement.GetString(), out var parsed) => parsed,
            JsonValueKind.Null => null,
            _ => throw new ArgumentException($"Invalid integer argument: {propertyName}")
        };
    }

    private static IReadOnlyCollection<int> GetRequiredIntArray(JsonElement? args, string propertyName)
    {
        if (!args.HasValue || args.Value.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException($"Missing required array argument: {propertyName}");
        }

        if (!args.Value.TryGetProperty(propertyName, out var valueElement) || valueElement.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException($"Missing required array argument: {propertyName}");
        }

        var values = new List<int>();
        foreach (var item in valueElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var number))
            {
                values.Add(number);
                continue;
            }

            if (item.ValueKind == JsonValueKind.String && int.TryParse(item.GetString(), out var parsed))
            {
                values.Add(parsed);
                continue;
            }

            throw new ArgumentException($"Invalid integer value in array argument: {propertyName}");
        }

        if (values.Count == 0)
        {
            throw new ArgumentException($"Array argument must contain at least one value: {propertyName}");
        }

        return values;
    }
}
