using BusinessObject.Enum;

namespace BusinessLogic.Constants;

public static class GradeExportRoles
{
    public const string Teacher = nameof(UserRole.TEACHER);
    public const string Admin = nameof(UserRole.ADMIN);

    public static readonly IReadOnlySet<string> SupportedRoles =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Teacher,
            Admin
        };
}
