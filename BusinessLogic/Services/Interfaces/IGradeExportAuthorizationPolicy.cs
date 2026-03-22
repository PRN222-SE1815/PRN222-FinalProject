using BusinessLogic.DTOs.Responses;

namespace BusinessLogic.Services.Interfaces;

public interface IGradeExportAuthorizationPolicy
{
    ServiceResult<bool> CanExport(string actorRole, string gradeBookStatus);
}
