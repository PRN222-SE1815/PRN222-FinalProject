using BusinessLogic.DTOs.Requests.Gradebook;
using BusinessLogic.DTOs.Responses;
using BusinessLogic.DTOs.Responses.Gradebook;

namespace BusinessLogic.Services.Interfaces;

public interface IGradeBookExportService
{
    Task<ServiceResult<GradeBookExportDataDto>> PrepareClassSectionExportAsync(
        int requesterUserId,
        ExportGradeBookRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<ExportGradeBookResponse>> ExportClassSectionAsync(
        int requesterUserId,
        ExportGradeBookRequest request,
        CancellationToken ct = default);
}
