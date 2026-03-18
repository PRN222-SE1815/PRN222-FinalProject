using System.Diagnostics;
using System.Text.Json;
using BusinessLogic.Constants;
using BusinessLogic.DTOs.Requests.Gradebook;
using BusinessLogic.DTOs.Response;
using BusinessLogic.DTOs.Responses.Gradebook;
using BusinessLogic.Services.Interfaces;
using BusinessLogic.Services.Models;
using BusinessLogic.Settings;
using DataAccess.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessLogic.Services.Implements;

public sealed class GradeBookExportService : IGradeBookExportService
{
    private static readonly EventId ExportAttemptEventId = new(5101, "GradeBookExportAttempt");
    private static readonly EventId ExportSuccessEventId = new(5102, "GradeBookExportSuccess");
    private static readonly EventId ExportFailureEventId = new(5103, "GradeBookExportFailure");
    private static readonly EventId ExportCancelledEventId = new(5104, "GradeBookExportCancelled");
    private static readonly EventId ExportAuditEventId = new(5105, "GradeBookExportAudit");

    private readonly IGradeBookExportReadRepository _gradeBookExportReadRepository;
    private readonly IUserRepository _userRepository;
    private readonly IGradeExportAuthorizationPolicy _gradeExportAuthorizationPolicy;
    private readonly IWeightedTotalCalculator _weightedTotalCalculator;
    private readonly IReadOnlyList<IGradeBookExportFileBuilder> _fileBuilders;
    private readonly ExportSettings _exportSettings;
    private readonly ILogger<GradeBookExportService> _logger;

    public GradeBookExportService(
        IGradeBookExportReadRepository gradeBookExportReadRepository,
        IUserRepository userRepository,
        IGradeExportAuthorizationPolicy gradeExportAuthorizationPolicy,
        IWeightedTotalCalculator weightedTotalCalculator,
        IEnumerable<IGradeBookExportFileBuilder> fileBuilders,
        IOptions<ExportSettings> exportSettings,
        ILogger<GradeBookExportService> logger)
    {
        _gradeBookExportReadRepository = gradeBookExportReadRepository;
        _userRepository = userRepository;
        _gradeExportAuthorizationPolicy = gradeExportAuthorizationPolicy;
        _weightedTotalCalculator = weightedTotalCalculator;
        _fileBuilders = fileBuilders.ToList();
        _exportSettings = exportSettings.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<ExportGradeBookResponse>> ExportClassSectionAsync(
        int requesterUserId,
        ExportGradeBookRequest request,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        if (request is null)
        {
            return ServiceResult<ExportGradeBookResponse>.Fail(ErrorCodes.InvalidInput, "Invalid export request.");
        }

        var classSectionId = request.ClassSectionId;
        var format = request.Format;
        string requesterRole = "UNKNOWN";
        int? gradeBookId = null;
        int rowCount = 0;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var timeoutSeconds = _exportSettings.ExportTimeoutSeconds <= 0 ? 30 : _exportSettings.ExportTimeoutSeconds;
        linkedCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            var requester = await _userRepository.GetUserByIdAsync(requesterUserId);
            requesterRole = requester?.Role ?? requesterRole;

            _logger.LogInformation(
                ExportAttemptEventId,
                "Gradebook export attempt. RequesterUserId={RequesterUserId}, Role={Role}, ClassSectionId={ClassSectionId}, Format={Format}",
                requesterUserId,
                requesterRole,
                classSectionId,
                format);

            var preparedResult = await PrepareClassSectionExportAsync(requesterUserId, request, linkedCts.Token);
            if (!preparedResult.IsSuccess || preparedResult.Data is null)
            {
                _logger.LogWarning(
                    ExportFailureEventId,
                    "Gradebook export failed before file generation. RequesterUserId={RequesterUserId}, Role={Role}, ClassSectionId={ClassSectionId}, GradeBookId={GradeBookId}, Format={Format}, RowCount={RowCount}, DurationMs={DurationMs}, Result={Result}, ErrorCode={ErrorCode}, Message={Message}",
                    requesterUserId,
                    requesterRole,
                    classSectionId,
                    gradeBookId,
                    format,
                    rowCount,
                    stopwatch.ElapsedMilliseconds,
                    "FAILED",
                    preparedResult.ErrorCode,
                    preparedResult.Message);

                WriteAuditTrail(
                    requesterUserId,
                    requesterRole,
                    classSectionId,
                    gradeBookId,
                    format,
                    rowCount,
                    stopwatch.ElapsedMilliseconds,
                    "FAILED",
                    preparedResult.ErrorCode,
                    preparedResult.Message);

                return ServiceResult<ExportGradeBookResponse>.Fail(
                    preparedResult.ErrorCode ?? ErrorCodes.InvalidInput,
                    preparedResult.Message);
            }

            gradeBookId = preparedResult.Data.GradeBookId;
            rowCount = preparedResult.Data.Rows.Count;

            if (rowCount > _exportSettings.MaxExportRows)
            {
                const string message = "Export row limit exceeded.";
                _logger.LogWarning(
                    ExportFailureEventId,
                    "Gradebook export rejected by row limit. RequesterUserId={RequesterUserId}, Role={Role}, ClassSectionId={ClassSectionId}, GradeBookId={GradeBookId}, Format={Format}, RowCount={RowCount}, DurationMs={DurationMs}, Result={Result}, ErrorCode={ErrorCode}",
                    requesterUserId,
                    requesterRole,
                    classSectionId,
                    gradeBookId,
                    format,
                    rowCount,
                    stopwatch.ElapsedMilliseconds,
                    "REJECTED",
                    ErrorCodes.ExportRowLimitExceeded);

                WriteAuditTrail(
                    requesterUserId,
                    requesterRole,
                    classSectionId,
                    gradeBookId,
                    format,
                    rowCount,
                    stopwatch.ElapsedMilliseconds,
                    "REJECTED",
                    ErrorCodes.ExportRowLimitExceeded,
                    message);

                return ServiceResult<ExportGradeBookResponse>.Fail(ErrorCodes.ExportRowLimitExceeded, message);
            }

            var builder = _fileBuilders.FirstOrDefault(x => x.CanBuild(preparedResult.Data.RequestedFormat));
            if (builder is null)
            {
                const string message = "Export format is not supported.";
                _logger.LogWarning(
                    ExportFailureEventId,
                    "Gradebook export failed due to unsupported builder. RequesterUserId={RequesterUserId}, Role={Role}, ClassSectionId={ClassSectionId}, GradeBookId={GradeBookId}, Format={Format}, RowCount={RowCount}, DurationMs={DurationMs}, Result={Result}, ErrorCode={ErrorCode}",
                    requesterUserId,
                    requesterRole,
                    classSectionId,
                    gradeBookId,
                    format,
                    rowCount,
                    stopwatch.ElapsedMilliseconds,
                    "FAILED",
                    ErrorCodes.InvalidInput);

                WriteAuditTrail(
                    requesterUserId,
                    requesterRole,
                    classSectionId,
                    gradeBookId,
                    format,
                    rowCount,
                    stopwatch.ElapsedMilliseconds,
                    "FAILED",
                    ErrorCodes.InvalidInput,
                    message);

                return ServiceResult<ExportGradeBookResponse>.Fail(ErrorCodes.InvalidInput, message);
            }

            var fileResult = builder.Build(preparedResult.Data, linkedCts.Token);

            _logger.LogInformation(
                ExportSuccessEventId,
                "Gradebook export succeeded. RequesterUserId={RequesterUserId}, Role={Role}, ClassSectionId={ClassSectionId}, GradeBookId={GradeBookId}, Format={Format}, RowCount={RowCount}, DurationMs={DurationMs}, Result={Result}",
                requesterUserId,
                requesterRole,
                classSectionId,
                gradeBookId,
                format,
                rowCount,
                stopwatch.ElapsedMilliseconds,
                "SUCCESS");

            WriteAuditTrail(
                requesterUserId,
                requesterRole,
                classSectionId,
                gradeBookId,
                format,
                rowCount,
                stopwatch.ElapsedMilliseconds,
                "SUCCESS",
                null,
                "Export completed.");

            return ServiceResult<ExportGradeBookResponse>.Success(fileResult);
        }
        catch (OperationCanceledException)
        {
            var message = ct.IsCancellationRequested
                ? "Export was cancelled by caller."
                : "Export timed out.";

            _logger.LogWarning(
                ExportCancelledEventId,
                "Gradebook export cancelled. RequesterUserId={RequesterUserId}, Role={Role}, ClassSectionId={ClassSectionId}, GradeBookId={GradeBookId}, Format={Format}, RowCount={RowCount}, DurationMs={DurationMs}, Result={Result}, ErrorCode={ErrorCode}, Message={Message}",
                requesterUserId,
                requesterRole,
                classSectionId,
                gradeBookId,
                format,
                rowCount,
                stopwatch.ElapsedMilliseconds,
                "CANCELLED",
                ErrorCodes.SystemError,
                message);

            WriteAuditTrail(
                requesterUserId,
                requesterRole,
                classSectionId,
                gradeBookId,
                format,
                rowCount,
                stopwatch.ElapsedMilliseconds,
                "CANCELLED",
                ErrorCodes.SystemError,
                message);

            return ServiceResult<ExportGradeBookResponse>.Fail(ErrorCodes.SystemError, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ExportFailureEventId,
                ex,
                "Gradebook export failed unexpectedly. RequesterUserId={RequesterUserId}, Role={Role}, ClassSectionId={ClassSectionId}, GradeBookId={GradeBookId}, Format={Format}, RowCount={RowCount}, DurationMs={DurationMs}, Result={Result}, ErrorCode={ErrorCode}",
                requesterUserId,
                requesterRole,
                classSectionId,
                gradeBookId,
                format,
                rowCount,
                stopwatch.ElapsedMilliseconds,
                "FAILED",
                ErrorCodes.SystemError);

            WriteAuditTrail(
                requesterUserId,
                requesterRole,
                classSectionId,
                gradeBookId,
                format,
                rowCount,
                stopwatch.ElapsedMilliseconds,
                "FAILED",
                ErrorCodes.SystemError,
                "An unexpected error occurred.");

            return ServiceResult<ExportGradeBookResponse>.Fail(ErrorCodes.SystemError, "An unexpected error occurred.");
        }
    }

    public async Task<ServiceResult<GradeBookExportDataDto>> PrepareClassSectionExportAsync(
        int requesterUserId,
        ExportGradeBookRequest request,
        CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            if (request is null || request.ClassSectionId <= 0)
            {
                return ServiceResult<GradeBookExportDataDto>.Fail(ErrorCodes.InvalidInput, "ClassSectionId must be greater than 0.");
            }

            if (!GradeExportPolicy.IsFormatSupported(request.Format))
            {
                return ServiceResult<GradeBookExportDataDto>.Fail(ErrorCodes.InvalidInput, "Export format is not supported.");
            }

            var requester = await _userRepository.GetUserByIdAsync(requesterUserId);
            if (requester is null || !requester.IsActive)
            {
                return ServiceResult<GradeBookExportDataDto>.Fail(ErrorCodes.Forbidden, "Requester is not active or does not exist.");
            }

            if (!GradeExportPolicy.IsRoleSupported(requester.Role))
            {
                return ServiceResult<GradeBookExportDataDto>.Fail(ErrorCodes.Forbidden, "Only teacher or admin can export gradebook.");
            }

            var exportMeta = await _gradeBookExportReadRepository.GetExportMetaByClassSectionIdAsync(request.ClassSectionId, ct);
            if (exportMeta is null)
            {
                return ServiceResult<GradeBookExportDataDto>.Fail(ErrorCodes.GradebookNotFound, "Gradebook not found.");
            }

            if (string.Equals(requester.Role, GradeExportRoles.Teacher, StringComparison.OrdinalIgnoreCase)
                && exportMeta.TeacherId != requesterUserId)
            {
                return ServiceResult<GradeBookExportDataDto>.Fail(ErrorCodes.Forbidden, "Teacher is not assigned to this class section.");
            }

            var statusPermission = _gradeExportAuthorizationPolicy.CanExport(requester.Role, exportMeta.GradeBookStatus);
            if (!statusPermission.IsSuccess)
            {
                return ServiceResult<GradeBookExportDataDto>.Fail(statusPermission.ErrorCode ?? ErrorCodes.Forbidden, statusPermission.Message);
            }

            var itemColumnsRaw = await _gradeBookExportReadRepository.GetItemColumnsAsync(exportMeta.GradeBookId, ct);
            var studentRowsRaw = await _gradeBookExportReadRepository.GetStudentRowsRawAsync(exportMeta.ClassSectionId, ct);
            var entryRowsRaw = await _gradeBookExportReadRepository.GetEntryRowsRawAsync(exportMeta.GradeBookId, exportMeta.ClassSectionId, ct);

            var columnDtos = itemColumnsRaw
                .Select(c => new GradeExportItemColumnDto
                {
                    GradeItemId = c.GradeItemId,
                    ItemName = c.ItemName,
                    Weight = c.Weight,
                    SortOrder = c.SortOrder
                })
                .ToList();

            var scoreLookup = entryRowsRaw
                .GroupBy(x => x.EnrollmentId)
                .ToDictionary(
                    x => x.Key,
                    x => x.ToDictionary(y => y.GradeItemId, y => y.Score));

            var rowDtos = new List<GradeExportRowDto>(studentRowsRaw.Count);
            foreach (var student in studentRowsRaw)
            {
                ct.ThrowIfCancellationRequested();

                scoreLookup.TryGetValue(student.EnrollmentId, out var enrollmentScores);
                var itemScores = new Dictionary<string, decimal?>(columnDtos.Count, StringComparer.Ordinal);
                var weightedInputs = new List<WeightedScoreInput>(columnDtos.Count);

                foreach (var column in columnDtos)
                {
                    decimal? scoreValue = null;
                    if (enrollmentScores is not null
                        && enrollmentScores.TryGetValue(column.GradeItemId, out var score))
                    {
                        scoreValue = score;
                    }

                    itemScores[column.GradeItemId.ToString()] = scoreValue;
                    weightedInputs.Add(new WeightedScoreInput
                    {
                        Score = scoreValue,
                        Weight = column.Weight
                    });
                }

                var total = _weightedTotalCalculator.CalculateTotal(weightedInputs);

                rowDtos.Add(new GradeExportRowDto
                {
                    StudentId = student.StudentId,
                    StudentCode = student.StudentCode,
                    FullName = student.FullName,
                    GradeBookStatus = exportMeta.GradeBookStatus,
                    ItemScores = itemScores,
                    Total = total
                });
            }

            var normalizedFormat = string.Equals(request.Format, GradeExportFormats.Csv, StringComparison.OrdinalIgnoreCase)
                ? GradeExportFormats.Csv
                : GradeExportFormats.Xlsx;

            var data = new GradeBookExportDataDto
            {
                GradeBookId = exportMeta.GradeBookId,
                ClassSectionId = exportMeta.ClassSectionId,
                SemesterCode = exportMeta.SemesterCode,
                CourseCode = exportMeta.CourseCode,
                SectionCode = exportMeta.SectionCode,
                GradeBookStatus = exportMeta.GradeBookStatus,
                Columns = columnDtos,
                Rows = rowDtos,
                GeneratedAtUtc = DateTime.UtcNow,
                RequestedFormat = normalizedFormat
            };

            return ServiceResult<GradeBookExportDataDto>.Success(data);
        }
        catch (OperationCanceledException)
        {
            return ServiceResult<GradeBookExportDataDto>.Fail(ErrorCodes.SystemError, "Export preparation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "PrepareClassSectionExportAsync failed. RequesterUserId={RequesterUserId}, ClassSectionId={ClassSectionId}",
                requesterUserId,
                request?.ClassSectionId);
            return ServiceResult<GradeBookExportDataDto>.Fail(ErrorCodes.SystemError, "An unexpected error occurred.");
        }
    }

    private void WriteAuditTrail(
        int requesterUserId,
        string role,
        int classSectionId,
        int? gradeBookId,
        string format,
        int rowCount,
        long durationMs,
        string result,
        string? errorCode,
        string message)
    {
        var payload = JsonSerializer.Serialize(new
        {
            RequesterUserId = requesterUserId,
            Role = role,
            ClassSectionId = classSectionId,
            GradeBookId = gradeBookId,
            Format = format,
            RowCount = rowCount,
            DurationMs = durationMs,
            Result = result,
            ErrorCode = errorCode,
            Message = message,
            Action = "GRADEBOOK_EXPORT_AUDIT",
            CreatedAtUtc = DateTime.UtcNow
        });

        _logger.LogInformation(ExportAuditEventId, "GRADEBOOK_EXPORT_AUDIT {AuditPayload}", payload);
    }
}
