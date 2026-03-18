using System.Globalization;
using System.Text;
using BusinessLogic.Constants;
using BusinessLogic.DTOs.Responses.Gradebook;
using BusinessLogic.Services.Interfaces;

namespace BusinessLogic.Services.Implements;

public sealed class CsvGradeBookExportFileBuilder : IGradeBookExportFileBuilder
{
    public bool CanBuild(string format)
        => string.Equals(format, GradeExportFormats.Csv, StringComparison.OrdinalIgnoreCase);

    public ExportGradeBookResponse Build(GradeBookExportDataDto data, CancellationToken ct = default)
    {
        var sb = new StringBuilder();

        var headerCells = new List<string>
        {
            "StudentCode",
            "FullName"
        };

        headerCells.AddRange(data.Columns.Select(x => x.ItemName));
        headerCells.Add("Total");
        headerCells.Add("GradeBookStatus");

        sb.AppendLine(string.Join(',', headerCells.Select(EscapeCsv)));

        foreach (var row in data.Rows)
        {
            ct.ThrowIfCancellationRequested();

            var cells = new List<string>
            {
                EscapeCsv(row.StudentCode),
                EscapeCsv(row.FullName)
            };

            foreach (var column in data.Columns)
            {
                row.ItemScores.TryGetValue(column.GradeItemId.ToString(), out var score);
                cells.Add(score.HasValue ? EscapeCsv(score.Value.ToString("0.##", CultureInfo.InvariantCulture)) : string.Empty);
            }

            cells.Add(EscapeCsv(row.Total.ToString("0.##", CultureInfo.InvariantCulture)));
            cells.Add(EscapeCsv(row.GradeBookStatus));

            sb.AppendLine(string.Join(',', cells));
        }

        var bom = Encoding.UTF8.GetPreamble();
        var payload = Encoding.UTF8.GetBytes(sb.ToString());
        var content = new byte[bom.Length + payload.Length];

        Buffer.BlockCopy(bom, 0, content, 0, bom.Length);
        Buffer.BlockCopy(payload, 0, content, bom.Length, payload.Length);

        return new ExportGradeBookResponse
        {
            FileName = BuildFileName(data, "csv"),
            ContentType = "text/csv; charset=utf-8",
            Content = content
        };
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var mustQuote = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (!mustQuote)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
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
