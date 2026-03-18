using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories.Implements;

public sealed class SemesterTuitionPolicyRepository : ISemesterTuitionPolicyRepository
{
    private readonly SchoolManagementDbContext _context;

    public SemesterTuitionPolicyRepository(SchoolManagementDbContext context)
    {
        _context = context;
    }

    public Task<SemesterTuitionPolicy?> GetActivePolicyBySemesterAsync(int semesterId, DateTime utcNow, CancellationToken ct = default)
    {
        return _context.SemesterTuitionPolicies
            .AsNoTracking()
            .Where(policy => policy.SemesterId == semesterId
                && policy.IsActive
                && policy.EffectiveFrom <= utcNow
                && (!policy.EffectiveTo.HasValue || policy.EffectiveTo.Value >= utcNow))
            .OrderByDescending(policy => policy.EffectiveFrom)
            .FirstOrDefaultAsync(ct);
    }
}
