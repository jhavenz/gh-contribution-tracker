using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using GitHubIssueTracker.Models;
using GitHubIssueTracker.Reports;

public class ContributionCsvRow
{
    public required string DeveloperLogin { get; set; }
    public required string IssueId { get; set; }
    public int Number { get; set; }
    public required string Type { get; set; }
    public required string Title { get; set; }
    public required string State { get; set; }
    public required string ClosedAt { get; set; }
    public required string Repository { get; set; }
    public required string Labels { get; set; }
}

public class CsvReportFormatter : IReportFormatter
{
    public void Format(List<ContributionReport> reports, TextWriter writer)
    {
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        var rows = new List<ContributionCsvRow>();

        foreach (var report in reports)
        {
            foreach (var contribution in report.Contributions)
            {
                rows.Add(new ContributionCsvRow
                {
                    DeveloperLogin = report.DeveloperLogin,
                    IssueId = contribution.IssueId,
                    Number = contribution.Number,
                    Type = contribution.IsPullRequest ? "Pull Request" : "Issue",
                    Title = contribution.Title,
                    State = contribution.State,
                    ClosedAt = contribution.ClosedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A",
                    Repository = contribution.Repository,
                    Labels = string.Join(", ", contribution.Labels)
                });
            }
        }

        csv.WriteRecords(rows);
    }
}