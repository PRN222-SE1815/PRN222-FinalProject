using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataAccess.Repositories.Implements;

public sealed class StudentRepository : IStudentRepository
{
    private readonly SchoolManagementDbContext _context;
    private readonly ILogger<StudentRepository> _logger;

    public StudentRepository(SchoolManagementDbContext context, ILogger<StudentRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<Student?> GetStudentByUserIdAsync(int userId)
    {
        return _context.Students
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.StudentId == userId);
    }
}
