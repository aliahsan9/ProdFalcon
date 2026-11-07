using System.Text;
using System.Text.Json;
using ProdFalcon.Application.Scanning;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Infrastructure.Reporting;

public static class ScanReportWriter
{
    public static byte[] WriteJson(ScanResultDto scan)
    {
        var payload = new
        {
            notice = SecurityScoreMethodology.Summary,
            project = scan.ProjectName,
            repository = scan.RepositoryUrl,
            branch = scan.Branch,
            source = scan.SourceType,
            scannedAt = DateTime.UtcNow,
            technologies = scan.Technologies,
            score = scan.OverallScore,
            riskLevel = scan.RiskLevel,
            previousScore = scan.PreviousScore,
            scoreChange = scan.ScoreChange,
            fileCount = scan.FileCount,
            findings = scan.Issues.Select(i => new
            {
                i.Id,
                i.Title,
                i.Severity,
                i.Confidence,
                i.Status,
                i.Category,
                i.Cwe,
                i.Cve,
                i.FilePath,
                i.LineNumber,
                i.DetectionMethod,
                i.Description,
                i.Impact,
                evidence = i.Evidence,
                i.Remediation,
                i.SecureExample,
                i.References
            })
        };

        return JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    public static byte[] WriteCsv(ScanResultDto scan)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Id,Severity,Status,Confidence,Category,Cwe,Cve,Title,File,Line,Detection,Description,Remediation");
        foreach (var issue in scan.Issues)
        {
            builder.Append(issue.Id).Append(',')
                .Append(Csv(issue.Severity)).Append(',')
                .Append(Csv(issue.Status)).Append(',')
                .Append(Csv(issue.Confidence)).Append(',')
                .Append(Csv(issue.Category)).Append(',')
                .Append(Csv(issue.Cwe)).Append(',')
                .Append(Csv(issue.Cve)).Append(',')
                .Append(Csv(issue.Title)).Append(',')
                .Append(Csv(issue.FilePath)).Append(',')
                .Append(issue.LineNumber).Append(',')
                .Append(Csv(issue.DetectionMethod)).Append(',')
                .Append(Csv(issue.Description)).Append(',')
                .Append(Csv(issue.Remediation))
                .AppendLine();
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public static byte[] WritePdf(ScanResultDto scan)
    {
        var lines = new List<string>
        {
            "ProdFalcon Security Assessment",
            $"Project: {scan.ProjectName}",
            $"Source: {scan.SourceType} {scan.RepositoryUrl} {scan.Branch}".Trim(),
            $"Score: {scan.OverallScore}/100  Risk: {scan.RiskLevel}",
            scan.PreviousScore.HasValue ? $"Previous score: {scan.PreviousScore}  Change: {scan.ScoreChange}" : "Previous score: none",
            $"Files analyzed: {scan.FileCount}",
            "Technologies: " + string.Join(", ", scan.Technologies),
            "",
            "This score is an indicative assessment of detected issues, not proof that the project is secure.",
            SecurityScoreMethodology.Summary,
            "",
            "Findings"
        };

        foreach (var issue in scan.Issues.Take(200))
        {
            lines.Add($"{issue.Severity} {issue.Status} {issue.Title}");
            lines.Add($"  {issue.FilePath}:{issue.LineNumber}  {issue.Cwe} {issue.Cve}".Trim());
            lines.Add("  " + issue.Description);
            if (!string.IsNullOrWhiteSpace(issue.Evidence))
                lines.Add("  Evidence: " + issue.Evidence);
            if (!string.IsNullOrWhiteSpace(issue.Remediation))
                lines.Add("  Fix: " + issue.Remediation);
            lines.Add("");
        }

        if (scan.Issues.Count == 0)
            lines.Add("No findings were recorded for this scan.");

        return SimplePdf.Render(lines);
    }

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}

internal static class SimplePdf
{
    public static byte[] Render(IReadOnlyList<string> lines)
    {
        var pages = new List<string>();
        var current = new List<string>();
        foreach (var line in lines.SelectMany(Wrap))
        {
            current.Add(Escape(line));
            if (current.Count == 46)
            {
                pages.Add(string.Join("\n", current));
                current = new List<string>();
            }
        }

        if (current.Count > 0 || pages.Count == 0)
            pages.Add(string.Join("\n", current));

        var objects = new List<byte[]>();
        void Add(string value) => objects.Add(Encoding.ASCII.GetBytes(value));

        Add("1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n");
        var pageIds = new List<int>();
        var fontId = 3 + pages.Count * 2;
        for (var i = 0; i < pages.Count; i++)
        {
            var pageObject = 3 + i * 2;
            var contentObject = pageObject + 1;
            pageIds.Add(pageObject);
            var commands = new StringBuilder();
            commands.Append("BT /F1 10 Tf 48 760 Td 14 TL\n");
            foreach (var row in pages[i].Split('\n'))
                commands.Append('(').Append(row).Append(") '\n");
            commands.Append("ET");
            var stream = commands.ToString();
            Add($"{pageObject} 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents {contentObject} 0 R /Resources << /Font << /F1 {fontId} 0 R >> >> >>endobj\n");
            Add($"{contentObject} 0 obj<< /Length {stream.Length} >>stream\n{stream}\nendstream\nendobj\n");
        }

        var kids = string.Join(" ", pageIds.Select(id => $"{id} 0 R"));
        objects.Insert(1, Encoding.ASCII.GetBytes($"2 0 obj<< /Type /Pages /Count {pages.Count} /Kids [{kids}] >>endobj\n"));
        Add($"{fontId} 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n");

        using var output = new MemoryStream();
        void Write(string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            output.Write(bytes, 0, bytes.Length);
        }

        Write("%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        foreach (var obj in objects)
        {
            offsets.Add(output.Length);
            output.Write(obj, 0, obj.Length);
        }

        var xref = output.Length;
        Write($"xref\n0 {objects.Count + 1}\n");
        Write("0000000000 65535 f \n");
        for (var i = 1; i < offsets.Count; i++)
            Write(offsets[i].ToString("0000000000") + " 00000 n \n");

        Write($"trailer<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return output.ToArray();
    }

    private static IEnumerable<string> Wrap(string line)
    {
        var text = line ?? string.Empty;
        if (text.Length <= 90)
        {
            yield return text;
            yield break;
        }

        for (var i = 0; i < text.Length; i += 90)
            yield return text.Substring(i, Math.Min(90, text.Length - i));
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
