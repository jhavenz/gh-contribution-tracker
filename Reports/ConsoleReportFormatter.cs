using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GitHubIssueTracker.Reports;

using System.Collections.Generic;
using System.IO;
using GitHubIssueTracker.Models;

public class ConsoleReportFormatter : IReportFormatter
{
    public void Format(List<ContributionReport> reports, TextWriter writer)
    {
        foreach (var report in reports)
        {
            writer.WriteLine($"\nDeveloper: {report.DeveloperLogin}");
            writer.WriteLine(new string('-', 50));
            if (report.Contributions.Count == 0)
            {
                writer.WriteLine("No contributions found.");
                continue;
            }

            foreach (var contribution in report.Contributions)
            {
                writer.WriteLine($"ID: {contribution.IssueId}");
                writer.WriteLine($"Number: {contribution.Number}");
                writer.WriteLine($"Type: {(contribution.IsPullRequest ? "Pull Request" : "Issue")}");
                writer.WriteLine($"Title: {contribution.Title}");
                writer.WriteLine($"State: {contribution.State}");
                writer.WriteLine($"Closed At: {(contribution.ClosedAt.HasValue ? contribution.ClosedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "N/A")}");
                writer.WriteLine($"Repository: {contribution.Repository}");
                writer.WriteLine($"Labels: {(contribution.Labels.Any() ? string.Join(", ", contribution.Labels) : "None")}");
                writer.WriteLine(new string('=', 50));
            }
        }
    }
}