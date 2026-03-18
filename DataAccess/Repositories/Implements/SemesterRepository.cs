using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataAccess.Repositories.Implements;

public sealed class SemesterRepository : ISemesterRepository
{
    private readonly SchoolManagementDbContext _context;
    private readonly ILogger<SemesterRepository> _logger;

    public SemesterRepository(SchoolManagementDbContext context, ILogger<SemesterRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Semester>> GetAllSemestersAsync()
    {
        return await _context.Semesters
            .AsNoTracking()
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();
    }

    public Task<Semester?> GetActiveSemesterAsync()
    {
        return _context.Semesters
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync();
    }

    public Task<Semester?> GetSemesterByIdAsync(int semesterId)
    {
        return _context.Semesters
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.SemesterId == semesterId);
    }
}
