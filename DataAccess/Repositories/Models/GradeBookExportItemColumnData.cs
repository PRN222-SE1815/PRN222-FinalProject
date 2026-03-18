namespace DataAccess.Repositories.Models;

public sealed class GradeBookExportItemColumnData
{
    public int GradeItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public decimal? Weight { get; set; }

    public int SortOrder { get; set; }
}
