using BusinessLogic.DTOs.Response;

namespace BusinessLogic.Services.Interfaces;

public interface IGradeExportAuthorizationPolicy
{
    ServiceResult<bool> CanExport(string actorRole, string gradeBookStatus);
}
