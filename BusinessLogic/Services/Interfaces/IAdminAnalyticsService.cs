using BusinessLogic.DTOs.Requests.AdminAnalytics;
using BusinessLogic.DTOs.Responses;
using BusinessLogic.DTOs.Responses.AdminAnalytics;

namespace BusinessLogic.Services.Interfaces;

public interface IAdminAnalyticsService
{
    Task<ServiceResult<AdminAnalyticsDashboardDto>> GetDashboardAsync(
        int adminUserId,
        AdminAnalyticsQueryRequest request,
        CancellationToken ct = default);
}
