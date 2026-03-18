using BusinessObject.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface ISemesterTuitionPolicyRepository
{
    Task<SemesterTuitionPolicy?> GetActivePolicyBySemesterAsync(int semesterId, DateTime utcNow, CancellationToken ct = default);
}
