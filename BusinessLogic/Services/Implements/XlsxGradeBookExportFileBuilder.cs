using System.Globalization;
using ClosedXML.Excel;
using BusinessLogic.Constants;
using BusinessLogic.DTOs.Responses.Gradebook;
using BusinessLogic.Services.Interfaces;

namespace BusinessLogic.Services.Implements;

public sealed class XlsxGradeBookExportFileBuilder : IGradeBookExportFileBuilder
{
    public bool CanBuild(string format)
        => string.Equals(format, GradeExportFormats.Xlsx, StringComparison.OrdinalIgnoreCase);

    public ExportGradeBookResponse Build(GradeBookExportDataDto data, CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Gradebook");

        var columnIndex = 1;
        worksheet.Cell(1, columnIndex++).Value = "StudentCode";
        worksheet.Cell(1, columnIndex++).Value = "FullName";

        foreach (var column in data.Columns)
        {
            worksheet.Cell(1, columnIndex++).Value = column.ItemName;
        }

        worksheet.Cell(1, columnIndex++).Value = "Total";
        worksheet.Cell(1, columnIndex).Value = "GradeBookStatus";

        worksheet.Row(1).Style.Font.Bold = true;

        var rowIndex = 2;
        foreach (var row in data.Rows)
        {
            ct.ThrowIfCancellationRequested();

            var currentColumn = 1;
            worksheet.Cell(rowIndex, currentColumn++).Value = row.StudentCode;
            worksheet.Cell(rowIndex, currentColumn++).Value = row.FullName;

            foreach (var column in data.Columns)
            {
                row.ItemScores.TryGetValue(column.GradeItemId.ToString(), out var score);
                worksheet.Cell(rowIndex, currentColumn++).Value = score;
            }

            worksheet.Cell(rowIndex, currentColumn++).Value = row.Total;
            worksheet.Cell(rowIndex, currentColumn).Value = row.GradeBookStatus;

            rowIndex++;
        }

        worksheet.Columns().AdjustToContents();
        ct.ThrowIfCancellationRequested();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new ExportGradeBookResponse
        {
            FileName = BuildFileName(data, "xlsx"),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            Content = stream.ToArray()
        };
    }

    private static string BuildFileName(GradeBookExportDataDto data, string extension)
    {
        var semester = SanitizeSegment(data.SemesterCode);
        var course = SanitizeSegment(data.CourseCode);
        var section = SanitizeSegment(data.SectionCode);
        var timestamp = data.GeneratedAtUtc.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

        return $"Gradebook_{semester}_{course}_{section}_{timestamp}.{extension}";
    }

    private static string SanitizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "NA";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "NA" : cleaned;
    }
}
