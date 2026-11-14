using System.Text;
using System.Text.Json;
using ProdFalcon.Application.Scanning;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Application.Scanning.Remediation;

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
                i.ColumnNumber,
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
        builder.AppendLine("Id,Severity,Status,Confidence,Category,Cwe,Cve,Title,File,Line,Column,Detection,Description,Remediation");
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
                .Append(issue.ColumnNumber).Append(',')
                .Append(Csv(issue.DetectionMethod)).Append(',')
                .Append(Csv(issue.Description)).Append(',')
                .Append(Csv(issue.Remediation))
                .AppendLine();
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public static byte[] WritePdf(ScanResultDto scan)
    {
        var counts = scan.Issues
            .GroupBy(issue => string.IsNullOrWhiteSpace(issue.Severity) ? "Info" : issue.Severity)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        string Count(string severity) => counts.TryGetValue(severity, out var count) ? count.ToString() : "0";
        var lines = new List<string>
        {
            "PRODFALCON SECURITY AUDIT",
            "Indicative report of stored static-analysis findings",
            "",
            $"Project: {Ascii(scan.ProjectName)}",
            $"Source: {Ascii(scan.SourceType)} {Ascii(scan.RepositoryUrl)} {Ascii(scan.Branch)}".Trim(),
            $"Score: {scan.OverallScore}/100  Risk: {Ascii(scan.RiskLevel)}",
            scan.PreviousScore.HasValue ? $"Previous score: {scan.PreviousScore}  Change: {scan.ScoreChange}" : "Previous score: none",
            $"Files analyzed: {scan.FileCount}",
            "Technologies: " + Ascii(string.Join(", ", scan.Technologies)),
            $"Severity: Critical {Count("Critical")}, High {Count("High")}, Medium {Count("Medium")}, Low {Count("Low")}, Info {Count("Info")}",
            "",
            "Scope: secrets, SQL injection, XSS, CSRF, SSRF, authentication, and dependency advisories.",
            "Each finding cites a file, line, and column when the scanner recorded them.",
            "This score is an indicative assessment of detected issues, not proof that the project is secure.",
            Ascii(SecurityScoreMethodology.Summary),
            "",
            "Findings"
        };

        foreach (var issue in scan.Issues.Take(80))
        {
            var draft = RemediationComposer.Draft(issue);
            var column = issue.ColumnNumber > 0 ? ":" + issue.ColumnNumber : string.Empty;
            lines.Add($"{Ascii(issue.Severity)} {Ascii(issue.Status)} {Ascii(issue.Title)}");
            lines.Add($"  Location: {Ascii(issue.FilePath)}:{issue.LineNumber}{column}  {Ascii(issue.Cwe)} {Ascii(issue.Cve)}".Trim());
            lines.Add("  " + Ascii(issue.Description));
            if (!string.IsNullOrWhiteSpace(issue.Evidence))
                lines.Add("  Evidence: " + Ascii(issue.Evidence));
            if (!string.IsNullOrWhiteSpace(issue.Impact))
                lines.Add("  Impact: " + Ascii(issue.Impact));
            lines.Add("  Why: " + Ascii(draft.Explanation));
            if (!string.IsNullOrWhiteSpace(issue.Remediation))
                lines.Add("  Fix: " + Ascii(issue.Remediation));
            if (!string.IsNullOrWhiteSpace(draft.PatchedSnippet))
                lines.Add("  Suggested patch: " + Ascii(draft.PatchedSnippet));
            lines.Add("  Patch status: Suggested. Approval is recorded separately and does not modify the repository.");
            lines.Add("");
        }

        if (scan.Issues.Count == 0)
            lines.Add("No findings were recorded for this scan.");
        else if (scan.Issues.Count > 80)
            lines.Add($"The PDF includes the first 80 of {scan.Issues.Count} findings. Download JSON or CSV for the full set.");

        lines.Add("");
        lines.Add("End of audit. A clean report does not mean the project is free of vulnerabilities.");
        return SimplePdf.Render(lines);
    }

    private static string Ascii(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
            builder.Append(character is >= ' ' and <= '~' ? character : ' ');
        return builder.ToString().Trim();
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
