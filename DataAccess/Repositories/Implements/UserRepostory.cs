using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace DataAccess.Repositories.Implements;

public sealed class UserRepository : IUserRepository
{
    private readonly SchoolManagementDbContext _context;

    public UserRepository(SchoolManagementDbContext context)
    {
        _context = context;
    }

    public Task<bool> UsernameExistsAsync(string username)
    {
        return _context.Users.AsNoTracking().AnyAsync(u => u.Username == username);
    }

    public Task<bool> StudentCodeExistsAsync(string studentCode, int? excludeUserId = null)
    {
        return _context.Students.AsNoTracking().AnyAsync(s =>
            s.StudentCode == studentCode && (!excludeUserId.HasValue || s.StudentId != excludeUserId.Value));
    }

    public Task<bool> TeacherCodeExistsAsync(string teacherCode, int? excludeUserId = null)
    {
        return _context.Teachers.AsNoTracking().AnyAsync(t =>
            t.TeacherCode == teacherCode && (!excludeUserId.HasValue || t.TeacherId != excludeUserId.Value));
    }

    public Task<User?> GetActiveUserByUsernameAsync(string username)
    {
        return _context.Users
            .AsNoTracking()
            .Include(u => u.Student)
            .Include(u => u.Teacher)
            .SingleOrDefaultAsync(u => u.Username == username && u.IsActive);
    }

    public Task<User?> GetUserWithDetailsAsync(int id)
    {
        return _context.Users
            .AsNoTracking()
            .Include(u => u.Student)
            .Include(u => u.Teacher)
            .SingleOrDefaultAsync(u => u.UserId == id);
    }

    public Task<User?> GetUserByIdAsync(int id)
    {
        return _context.Users
            .Include(u => u.Student)
            .Include(u => u.Teacher)
            .SingleOrDefaultAsync(u => u.UserId == id);
    }

    public async Task<(IReadOnlyList<User> Users, int TotalCount)> GetUsersAsync(string? role, string? search, int page, int pageSize)
    {
        var query = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role == role);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(u => u.FullName.Contains(normalizedSearch) || (u.Email != null && u.Email.Contains(normalizedSearch)));
        }

        var totalCount = await query.CountAsync();
        var users = await query
            .OrderBy(u => u.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (users, totalCount);
    }

    public async Task<User> CreateUserWithProfileAsync(User user, Student? student, Teacher? teacher)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            if (student != null)
            {
                student.StudentId = user.UserId;
                _context.Students.Add(student);
            }

            if (teacher != null)
            {
                teacher.TeacherId = user.UserId;
                _context.Teachers.Add(teacher);
            }

            if (student != null || teacher != null)
            {
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            return user;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateUserAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<User>> SearchActiveUsersAsync(string? search, int excludeUserId, int limit)
    {
        var query = _context.Users.AsNoTracking().Where(u => u.IsActive && u.UserId != excludeUserId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(normalizedSearch)
                || (u.Email != null && u.Email.ToLower().Contains(normalizedSearch)));
        }

        return await query
            .OrderBy(u => u.FullName)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<int>> GetActiveUserIdsByRoleAsync(string role, CancellationToken ct = default)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.Role == role)
            .Select(u => u.UserId)
            .ToListAsync(ct);
    }
}
