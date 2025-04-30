using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GitHubIssueTracker.Models;

namespace GitHubIssueTracker.Reports;

public class JsonReportFormatter : IReportFormatter
{
    public void Format(List<ContributionReport> reports, TextWriter writer)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };

        var json = JsonSerializer.Serialize(reports, options);

        writer.WriteLine(json);
    }
}
