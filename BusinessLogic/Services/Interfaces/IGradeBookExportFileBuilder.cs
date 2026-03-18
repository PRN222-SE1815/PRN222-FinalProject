using BusinessLogic.DTOs.Responses.Gradebook;

namespace BusinessLogic.Services.Interfaces;

public interface IGradeBookExportFileBuilder
{
    bool CanBuild(string format);

    ExportGradeBookResponse Build(GradeBookExportDataDto data, CancellationToken ct = default);
}
