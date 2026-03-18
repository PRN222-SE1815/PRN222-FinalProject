namespace BusinessLogic.Constants;

public static class GradeExportPolicy
{
    public static readonly IReadOnlySet<string> TeacherAllowedStatuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            GradeBookStatusValues.Draft,
            GradeBookStatusValues.Published
        };

    public static readonly IReadOnlySet<string> AdminAllowedStatuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            GradeBookStatusValues.Draft,
            GradeBookStatusValues.Published,
            GradeBookStatusValues.PendingApproval,
            GradeBookStatusValues.Rejected,
            GradeBookStatusValues.Locked,
            GradeBookStatusValues.Archived
        };

    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedStatusesByRole =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [GradeExportRoles.Teacher] = TeacherAllowedStatuses,
            [GradeExportRoles.Admin] = AdminAllowedStatuses
        };

    public static bool IsRoleSupported(string role)
        => !string.IsNullOrWhiteSpace(role) && GradeExportRoles.SupportedRoles.Contains(role);

    public static bool IsFormatSupported(string format)
        => !string.IsNullOrWhiteSpace(format) && GradeExportFormats.SupportedFormats.Contains(format);

    public static bool IsKnownGradeBookStatus(string status)
        => !string.IsNullOrWhiteSpace(status) && GradeBookStatusValues.All.Contains(status);

    public static bool IsStatusAllowedForRole(string role, string status)
    {
        if (string.IsNullOrWhiteSpace(role)
            || string.IsNullOrWhiteSpace(status)
            || !AllowedStatusesByRole.TryGetValue(role, out var allowedStatuses))
        {
            return false;
        }

        return allowedStatuses.Contains(status);
    }
}
