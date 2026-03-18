using BusinessLogic.Constants;
using BusinessLogic.DTOs.Response;
using BusinessLogic.Services.Interfaces;

namespace BusinessLogic.Services.Implements;

public sealed class GradeExportAuthorizationPolicy : IGradeExportAuthorizationPolicy
{
    public ServiceResult<bool> CanExport(string actorRole, string gradeBookStatus)
    {
        if (string.IsNullOrWhiteSpace(actorRole))
        {
            return ServiceResult<bool>.Fail(ErrorCodes.InvalidInput, "Actor role is required.");
        }

        if (string.IsNullOrWhiteSpace(gradeBookStatus))
        {
            return ServiceResult<bool>.Fail(ErrorCodes.InvalidInput, "Gradebook status is required.");
        }

        if (!GradeExportPolicy.IsRoleSupported(actorRole))
        {
            return ServiceResult<bool>.Fail(ErrorCodes.Forbidden, "Actor role is not allowed to export gradebook.");
        }

        if (!GradeExportPolicy.IsKnownGradeBookStatus(gradeBookStatus))
        {
            return ServiceResult<bool>.Fail(ErrorCodes.InvalidInput, "Gradebook status is invalid.");
        }

        if (!GradeExportPolicy.IsStatusAllowedForRole(actorRole, gradeBookStatus))
        {
            return ServiceResult<bool>.Fail(
                ErrorCodes.Forbidden,
                $"Role '{actorRole}' is not allowed to export gradebook in status '{gradeBookStatus}'.");
        }

        return ServiceResult<bool>.Success(true);
    }
}
