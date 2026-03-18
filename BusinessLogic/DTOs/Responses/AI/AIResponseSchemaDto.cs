using System.Text.Json.Serialization;

namespace BusinessLogic.DTOs.Responses.AI;

public sealed class AIResponseSchemaDto
{
    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = string.Empty;

    [JsonPropertyName("summaryCards")]
    public List<SummaryCardDto> SummaryCards { get; set; } = [];

    [JsonPropertyName("riskFlags")]
    public List<RiskFlagDto> RiskFlags { get; set; } = [];

    [JsonPropertyName("recommendedActions")]
    public List<RecommendedActionDto> RecommendedActions { get; set; } = [];

    [JsonPropertyName("plans")]
    public List<AIPlanOptionDto> Plans { get; set; } = [];

    [JsonPropertyName("disclaimer")]
    public string? Disclaimer { get; set; }
}

public sealed class SummaryCardDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }
}

public sealed class RiskFlagDto
{
    [JsonPropertyName("courseCode")]
    public string CourseCode { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "LOW";
}

public sealed class RecommendedActionDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 3;
}

public sealed class AIPlanOptionDto
{
    [JsonPropertyName("planName")]
    public string PlanName { get; set; } = string.Empty;

    [JsonPropertyName("totalCredits")]
    public int TotalCredits { get; set; }

    [JsonPropertyName("constraintsOk")]
    public bool ConstraintsOk { get; set; }

    [JsonPropertyName("courseCodes")]
    public List<string> CourseCodes { get; set; } = [];

    [JsonPropertyName("notes")]
    public List<string> Notes { get; set; } = [];
}
