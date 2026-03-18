using BusinessObject.Enum;

namespace BusinessLogic.Constants;

public static class GradeBookStatusValues
{
    public const string Draft = nameof(GradeBookStatus.DRAFT);
    public const string PendingApproval = nameof(GradeBookStatus.PENDING_APPROVAL);
    public const string Rejected = nameof(GradeBookStatus.REJECTED);
    public const string Published = nameof(GradeBookStatus.PUBLISHED);
    public const string Locked = nameof(GradeBookStatus.LOCKED);
    public const string Archived = nameof(GradeBookStatus.ARCHIVED);

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Draft,
            PendingApproval,
            Rejected,
            Published,
            Locked,
            Archived
        };
}
