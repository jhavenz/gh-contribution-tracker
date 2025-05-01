using GitHubIssueTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace GitHubIssueTracker.Database;

// No longer needs IDisposable if DbContext is managed with 'using'
public class DatabaseManager
{
    public static string DatabasePath = Path.Combine(
        AppContext.BaseDirectory, // Gets the base directory of the application
        "..", // Up one level from bin/Debug/net9.0
        "..", // Up one level from Debug
        "..", // Up one level from bin
        "github-tracker.db");

    public DatabaseManager()
    {
        var filePath = Path.GetDirectoryName(DatabasePath);
        if (filePath != null && !Directory.Exists(filePath))
            Directory.CreateDirectory(filePath);
    }

    public void SaveIssues(List<GitHubIssue> issues)
    {
        using var context = new AppDbContext();

        // Track existing entities to avoid duplicates
        var existingUsers = context.Users.AsNoTracking().ToDictionary(u => u.Id);
        var existingLabels = context.Labels.AsNoTracking().ToDictionary(l => l.Id);
        var existingRepos = context.Repositories.AsNoTracking().ToDictionary(r => r.NameWithOwner);

        foreach (var issue in issues)
        {
            // Attach or Add Repository
            if (existingRepos.TryGetValue(issue.Repository.NameWithOwner, out var repo))
            {
                context.Attach(repo); // Attach existing
                issue.Repository = repo; // Ensure the issue points to the tracked entity
            }
            else
            {
                context.Add(issue.Repository); // Add new
                existingRepos.Add(issue.Repository.NameWithOwner, issue.Repository); // Track locally
            }

            // Attach or Add Author
            if (existingUsers.TryGetValue(issue.Author.Id, out var author))
            {
                context.Attach(author);
                issue.Author = author;
            }
            else
            {
                context.Add(issue.Author);
                existingUsers.Add(issue.Author.Id, issue.Author);
            }

            issue.AuthorId = issue.Author.Id; // Ensure FK is set

            // Attach or Add Assignees
            var assigneesToAdd = new List<GitHubUser>();
            foreach (var assignee in issue.Assignees)
                if (existingUsers.TryGetValue(assignee.Id, out var existingAssignee))
                {
                    context.Attach(existingAssignee);
                    assigneesToAdd.Add(existingAssignee);
                }
                else
                {
                    context.Add(assignee);
                    existingUsers.Add(assignee.Id, assignee);
                    assigneesToAdd.Add(assignee);
                }

            issue.Assignees = assigneesToAdd;

            // Attach or Add Labels
            var labelsToAdd = new List<GitHubLabel>();
            foreach (var label in issue.Labels)
                if (existingLabels.TryGetValue(label.Id, out var existingLabel))
                {
                    context.Attach(existingLabel);
                    labelsToAdd.Add(existingLabel);
                }
                else
                {
                    context.Add(label);
                    existingLabels.Add(label.Id, label);
                    labelsToAdd.Add(label);
                }

            issue.Labels = labelsToAdd;

            // Add or Update Issue
            // Use Add or Update based on whether the issue already exists
            var existingIssue = context.Issues.Local.FirstOrDefault(i => i.Id == issue.Id) ??
                                context.Issues.FirstOrDefault(i => i.Id == issue.Id);
            if (existingIssue != null)
            {
                context.Entry(existingIssue).CurrentValues.SetValues(issue);
                existingIssue.Assignees = issue.Assignees;
                existingIssue.Labels = issue.Labels;
            }
            else
            {
                context.Issues.Add(issue);
            }
        }

        context.SaveChanges();
    }

    public List<ContributionReport> GenerateContributionReport(string? developerLogin = null,
        DateTime? startDate = null, DateTime? endDate = null)
    {
        using var context = new AppDbContext();

        // Base query for issues, including related data
        var query = context.Issues
            .Include(i => i.Repository)
            .Include(i => i.Assignees) // Include assignees for filtering
            .Include(i => i.Labels)
            .AsQueryable(); // Use AsQueryable to build the query dynamically

        // Apply filters
        if (startDate.HasValue) query = query.Where(i => i.CreatedAt >= startDate.Value);
        if (endDate.HasValue)
            // Add 1 day to endDate to make it inclusive of the whole day
            query = query.Where(i => i.CreatedAt < endDate.Value.AddDays(1));

        // Filter by assignee login if provided
        if (!string.IsNullOrEmpty(developerLogin))
            query = query.Where(i => i.Assignees.Any(a => a.Login == developerLogin));

        // Select the necessary data and group by developer
        // We need to project into an intermediate structure before grouping
        var contributions = query
            .SelectMany(i => i.Assignees.Select(a => new { AssigneeLogin = a.Login, Issue = i })) // Flatten by assignee
            .Where(x => string.IsNullOrEmpty(developerLogin) ||
                        x.AssigneeLogin == developerLogin) // Ensure we only get relevant assignee's data
            .Select(x => new
            {
                x.AssigneeLogin,
                Detail = new ContributionDetail
                {
                    IssueId = x.Issue.Id,
                    Number = x.Issue.Number,
                    Title = x.Issue.Title,
                    State = x.Issue.State,
                    ClosedAt = x.Issue.ClosedAt,
                    CreatedAt = x.Issue.CreatedAt,
                    IsPullRequest = x.Issue.IsPullRequest,
                    Repository = x.Issue.Repository.NameWithOwner,
                    Labels = x.Issue.Labels.Select(l => l.Name).ToList()
                }
            })
            .ToList();

        var reports = contributions
            .GroupBy(c => c.AssigneeLogin)
            .Select(g => new ContributionReport
            {
                DeveloperLogin = g.Key,
                Contributions = g.Select(c => c.Detail).OrderBy(d => d.Number).ToList()
            })
            .OrderBy(r => r.DeveloperLogin)
            .ToList();

        return reports;
    }

    // Removed Dispose method as DbContext handles its own disposal with 'using'
}