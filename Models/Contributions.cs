using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GitHubIssueTracker.Models
{
    public class ContributionReport
    {
        public required string DeveloperLogin { get; set; }
        public required List<ContributionDetail> Contributions { get; set; }
    }

    public class ContributionDetail
    {
        public required string IssueId { get; set; }
        public int Number { get; set; }
        public required string Title { get; set; }
        public required string State { get; set; }
        public DateTime? ClosedAt { get; set; }
        public required DateTime CreatedAt { get; set; }
        public bool IsPullRequest { get; set; }
        public required string Repository { get; set; }
        public required List<string> Labels { get; set; }
    }
}