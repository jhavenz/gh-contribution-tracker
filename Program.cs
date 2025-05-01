using System.Text.Json;
using CommandLine;
using GitHubIssueTracker.Database;
using GitHubIssueTracker.Models;
using GitHubIssueTracker.Reports;
using Microsoft.EntityFrameworkCore;

// Keep this for AppDbContext
// Add EF Core namespace
// Add Linq namespace

// Add for date formatting

namespace GitHubIssueTracker;

[Verb("save", HelpText = "Save GitHub issues from JSON input to the database.")]
public class SaveOptions
{
    [Option('i', "input", Required = false, HelpText = "Path to the JSON input file (optional if input is piped).")]
    public string? InputFile { get; set; }
}

[Verb("report", HelpText = "Generate a contribution report from the database.")]
public class ReportOptions
{
    [Option('f', "format", Required = false, Default = "csv", HelpText = "Output format: csv, console, json.")]
    public string? Format { get; set; }

    [Option('u', "user", Required = false, HelpText = "Developer login to filter the report (optional).")]
    public string? DeveloperLogin { get; set; }

    [Option('s', "start", Required = false, HelpText = "Start date for the report (yyyy-MM-dd).")]
    public string? StartDate { get; set; }

    [Option('e', "end", Required = false, HelpText = "End date for the report (yyyy-MM-dd).")]
    public string? EndDate { get; set; }
}

internal class Program
{
    private static void Main(string[] args)
    {
        using (var context = new AppDbContext())
        {
            try
            {
                context.Database.Migrate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying migrations: {ex.Message}");
                return;
            }
        }

        Parser.Default.ParseArguments<SaveOptions, ReportOptions>(args)
            .WithParsed<SaveOptions>(RunSaveCommand)
            .WithParsed<ReportOptions>(RunReportCommand)
            .WithNotParsed(_ => Console.WriteLine("Error parsing arguments. Use --help for usage."));
    }

    private static void RunSaveCommand(SaveOptions opts)
    {
        try
        {
            string jsonInput;
            if (Console.IsInputRedirected)
            {
                jsonInput = Console.In.ReadToEnd();
            }
            else if (!string.IsNullOrEmpty(opts.InputFile))
            {
                if (!File.Exists(opts.InputFile))
                {
                    Console.WriteLine($"Error: Input file '{opts.InputFile}' does not exist.");
                    return;
                }

                jsonInput = File.ReadAllText(opts.InputFile);
            }
            else
            {
                Console.WriteLine(
                    "Error: No input provided. Please pipe input or specify a JSON input file path with -i or --input.");
                return;
            }

            var issues = JsonSerializer.Deserialize<List<GitHubIssue>>(jsonInput,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (issues == null || issues.Count == 0)
            {
                Console.WriteLine("No issues found in the input.");
                return;
            }

            using var context = new AppDbContext();

            var processedUsers = new Dictionary<string, GitHubUser>();
            var processedLabels = new Dictionary<string, GitHubLabel>();
            var processedRepos = new Dictionary<string, GitHubRepository>();

            foreach (var issue in issues)
            {
                GitHubRepository? trackedRepo;
                if (processedRepos.TryGetValue(issue.Repository.NameWithOwner, out var existingRepo))
                {
                    trackedRepo = existingRepo;
                }
                else
                {
                    trackedRepo = context.Repositories.Find(issue.Repository.NameWithOwner);
                    if (trackedRepo == null)
                    {
                        trackedRepo = issue.Repository;
                        context.Repositories.Add(trackedRepo);
                    }

                    processedRepos.Add(trackedRepo.NameWithOwner, trackedRepo);
                }

                issue.Repository = trackedRepo;
                issue.RepositoryNameWithOwner = trackedRepo.NameWithOwner;

                GitHubUser? trackedAuthor;
                if (processedUsers.TryGetValue(issue.Author.Id, out var existingAuthor))
                {
                    trackedAuthor = existingAuthor;
                }
                else
                {
                    trackedAuthor = context.Users.Find(issue.Author.Id);
                    if (trackedAuthor == null)
                    {
                        trackedAuthor = issue.Author;
                        context.Users.Add(trackedAuthor);
                    }

                    processedUsers.Add(trackedAuthor.Id, trackedAuthor);
                }

                issue.Author = trackedAuthor;
                issue.AuthorId = trackedAuthor.Id;

                var trackedAssignees = new List<GitHubUser>();
                foreach (var assignee in issue.Assignees)
                {
                    GitHubUser? trackedAssignee;
                    if (processedUsers.TryGetValue(assignee.Id, out var existingAssignee))
                    {
                        trackedAssignee = existingAssignee;
                    }
                    else
                    {
                        trackedAssignee = context.Users.Find(assignee.Id);
                        if (trackedAssignee == null)
                        {
                            trackedAssignee = assignee;
                            context.Users.Add(trackedAssignee);
                        }

                        processedUsers.Add(trackedAssignee.Id, trackedAssignee);
                    }

                    trackedAssignees.Add(trackedAssignee);
                }

                issue.Assignees = trackedAssignees;

                var trackedLabels = new List<GitHubLabel>();
                foreach (var label in issue.Labels)
                {
                    GitHubLabel? trackedLabel;
                    if (processedLabels.TryGetValue(label.Id, out var existingLabel))
                    {
                        trackedLabel = existingLabel;
                    }
                    else
                    {
                        trackedLabel = context.Labels.Find(label.Id);
                        if (trackedLabel == null)
                        {
                            trackedLabel = label;
                            context.Labels.Add(trackedLabel);
                        }

                        processedLabels.Add(trackedLabel.Id, trackedLabel);
                    }

                    trackedLabels.Add(trackedLabel);
                }

                issue.Labels = trackedLabels;

                var existingIssue = context.Issues
                    .Include(i => i.Assignees) // Include collections for update
                    .Include(i => i.Labels)
                    .FirstOrDefault(i => i.Id == issue.Id);

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
            Console.WriteLine(
                $"Successfully saved/updated {issues.Count} issues/pull requests to {DatabaseManager.DatabasePath}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving data: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            Console.WriteLine(ex.ToString());
        }
    }

    private static void RunReportCommand(ReportOptions opts)
    {
        try
        {
            var format = opts.Format?.ToLower() ?? "csv";
            IReportFormatter formatter;
            switch (format)
            {
                case "csv":
                    formatter = new CsvReportFormatter();
                    break;
                case "console":
                    formatter = new ConsoleReportFormatter();
                    break;
                case "json":
                    formatter = new JsonReportFormatter();
                    break;
                default:
                    Console.WriteLine($"Error: Invalid format '{opts.Format}'. Supported formats: csv, console, json.");
                    return;
            }

            DateTime? startDate = null;
            DateTime? endDate = null;
            if (!string.IsNullOrEmpty(opts.StartDate) && DateTime.TryParse(opts.StartDate, out var parsedStartDate))
                startDate = parsedStartDate;
            if (!string.IsNullOrEmpty(opts.EndDate) && DateTime.TryParse(opts.EndDate, out var parsedEndDate))
                endDate = parsedEndDate;


            using var context = new AppDbContext();
            var reports = GenerateContributionReportEf(context, opts.DeveloperLogin, startDate, endDate);

            if (!reports.Any() || !reports.SelectMany(r => r.Contributions).Any())
            {
                Console.WriteLine("No contribution data found for the specified criteria.");
                return;
            }

            string dateRangeString;
            if (startDate.HasValue && endDate.HasValue)
            {
                dateRangeString = $"{startDate.Value.ToString("yyyyMMdd")}-{endDate.Value.ToString("yyyyMMdd")}";
            }
            else
            {
                var allDates = reports.SelectMany(r => r.Contributions)
                    .Select(c => c.CreatedAt)
                    .ToList();

                if (allDates.Any())
                {
                    var minDate = allDates.Min();
                    var maxDate = allDates.Max();
                    dateRangeString = $"{minDate.ToString("yyyyMMdd")}-{maxDate.ToString("yyyyMMdd")}";
                }
                else
                {
                    dateRangeString = "alltime";
                }
            }

            TextWriter writer;
            string? outputFilePath = null;

            if (format != "console")
            {
                var projectSourceDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
                var outputDir = Path.Combine(projectSourceDir, format);
                Directory.CreateDirectory(outputDir);
                outputFilePath = Path.Combine(outputDir, $"gh-contributions-{dateRangeString}.{format}");
                writer = new StreamWriter(outputFilePath);
                Console.WriteLine($"Generating report to: {outputFilePath}");
            }
            else
            {
                writer = Console.Out;
            }

            try
            {
                formatter.Format(reports, writer);
            }
            finally
            {
                if (writer != Console.Out) writer.Dispose();
            }

            if (outputFilePath != null) Console.WriteLine($"Report successfully saved to: {outputFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating report: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            Console.WriteLine(ex.ToString());
        }
    }

    private static List<ContributionReport> GenerateContributionReportEf(AppDbContext context,
        string? developerLogin = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = context.Issues
            .Include(i => i.Repository)
            .Include(i => i.Assignees)
            .Include(i => i.Labels)
            .AsQueryable();

        if (startDate.HasValue) query = query.Where(i => i.CreatedAt >= startDate.Value);
        if (endDate.HasValue) query = query.Where(i => i.CreatedAt < endDate.Value.AddDays(1));

        // Filter by assignee login *after* fetching if a specific user is requested
        // This avoids the APPLY issue but might fetch more initial data if filtering by user.
        // If not filtering by user, this filter is effectively skipped later.
        var issuesFromDb = query.ToList();

        var contributions = issuesFromDb
            .SelectMany(issue =>
                issue.Assignees.Select(assignee => new { AssigneeLogin = assignee.Login, Issue = issue }))
            .Where(x => string.IsNullOrEmpty(developerLogin) || x.AssigneeLogin == developerLogin)
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

        // Group the results by developer login in memory
        var reports = contributions
            .GroupBy(c => c.AssigneeLogin)
            .Select(g => new ContributionReport
            {
                DeveloperLogin = g.Key,
                Contributions = g.Select(c => c.Detail).OrderBy(d => d.CreatedAt).ThenBy(d => d.Number).ToList()
            })
            .OrderBy(r => r.DeveloperLogin)
            .ToList();

        return reports;
    }
}