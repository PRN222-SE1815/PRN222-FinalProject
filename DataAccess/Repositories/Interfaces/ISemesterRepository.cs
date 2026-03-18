using BusinessObject.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface ISemesterRepository
{
    Task<IReadOnlyList<Semester>> GetAllSemestersAsync();
    Task<Semester?> GetActiveSemesterAsync();
    Task<Semester?> GetSemesterByIdAsync(int semesterId);
}
