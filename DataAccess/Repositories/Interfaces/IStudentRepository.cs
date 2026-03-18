using BusinessObject.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface IStudentRepository
{
    Task<Student?> GetStudentByUserIdAsync(int userId);
}
