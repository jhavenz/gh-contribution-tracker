using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GitHubIssueTracker.Models;

namespace GitHubIssueTracker.Reports;

public interface IReportFormatter
{
    void Format(List<ContributionReport> reports, TextWriter writer);
}