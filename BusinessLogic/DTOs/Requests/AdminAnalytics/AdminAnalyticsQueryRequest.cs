namespace BusinessLogic.DTOs.Requests.AdminAnalytics;

public sealed class AdminAnalyticsQueryRequest
{
    public int? SemesterId { get; set; }

    public int? CompareSemesterId { get; set; }

    public bool IncludeAllSemesters { get; set; }
}
