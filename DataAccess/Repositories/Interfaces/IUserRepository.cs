using BusinessObject.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface IUserRepository
{
    Task<bool> UsernameExistsAsync(string username);
    Task<bool> StudentCodeExistsAsync(string studentCode, int? excludeUserId = null);
    Task<bool> TeacherCodeExistsAsync(string teacherCode, int? excludeUserId = null);
    Task<User?> GetActiveUserByUsernameAsync(string username);
    Task<User> CreateUserWithProfileAsync(User user, Student? student, Teacher? teacher);
    Task<User?> GetUserWithDetailsAsync(int id);
    Task<User?> GetUserByIdAsync(int id);
    Task<(IReadOnlyList<User> Users, int TotalCount)> GetUsersAsync(string? role, string? search, int page, int pageSize);
    Task UpdateUserAsync(User user);
    Task<IReadOnlyList<User>> SearchActiveUsersAsync(string? search, int excludeUserId, int limit);
    Task<IReadOnlyList<int>> GetActiveUserIdsByRoleAsync(string role, CancellationToken ct = default);
}
