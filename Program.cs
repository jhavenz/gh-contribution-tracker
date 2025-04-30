using CommandLine;
using System;
using System.IO;
using System.Text.Json;
using GitHubIssueTracker.Database; // Keep this for AppDbContext
using GitHubIssueTracker.Models;
using GitHubIssueTracker.Reports;
using System.Reflection;
using Microsoft.EntityFrameworkCore; // Add EF Core namespace
using System.Linq; // Add Linq namespace
using System.Globalization; // Add for date formatting

namespace GitHubIssueTracker
{
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

        // Removed OutputFile option

        [Option('u', "user", Required = false, HelpText = "Developer login to filter the report (optional).")]
        public string? DeveloperLogin { get; set; }

        [Option('s', "start", Required = false, HelpText = "Start date for the report (yyyy-MM-dd).")]
        public string? StartDate { get; set; }

        [Option('e', "end", Required = false, HelpText = "End date for the report (yyyy-MM-dd).")]
        public string? EndDate { get; set; }
    }


    class Program
    {
        // Define the database path relative to the project root
        private static readonly string DatabasePath = Path.Combine(
            AppContext.BaseDirectory, // Gets the base directory of the application
            "..", // Up one level from bin/Debug/net9.0
            "..", // Up one level from Debug
            "..", // Up one level from bin
            "..", // Up one level from GitHubIssueTracker project folder
            "github-tracker.db");

        static void Main(string[] args)
        {
            // Ensure the database exists and apply migrations
            using (var context = new AppDbContext(DatabasePath))
            {
                try
                {
                    context.Database.Migrate(); // Apply pending migrations
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error applying migrations: {ex.Message}");
                    // Decide if the application should exit or continue
                    return; // Exit if migrations fail
                }
            }

            Parser.Default.ParseArguments<SaveOptions, ReportOptions>(args)
                .WithParsed<SaveOptions>(opts => RunSaveCommand(opts))
                .WithParsed<ReportOptions>(opts => RunReportCommand(opts))
                .WithNotParsed(errs => Console.WriteLine("Error parsing arguments. Use --help for usage."));
        }

        static void RunSaveCommand(SaveOptions opts)
        {
            try
            {
                // ... (Input reading logic remains the same) ...
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
                    Console.WriteLine("Error: No input provided. Please pipe input or specify a JSON input file path with -i or --input.");
                    return;
                }

                var issues = JsonSerializer.Deserialize<List<GitHubIssue>>(jsonInput,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (issues == null || issues.Count == 0)
                {
                    Console.WriteLine("No issues found in the input.");
                    return;
                }

                using var context = new AppDbContext(DatabasePath);

                // Keep track of entities processed in this run to avoid redundant DB checks
                var processedUsers = new Dictionary<string, GitHubUser>();
                var processedLabels = new Dictionary<string, GitHubLabel>();
                var processedRepos = new Dictionary<string, GitHubRepository>();

                foreach (var issue in issues)
                {
                    // --- Process Repository ---
                    GitHubRepository trackedRepo;
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

                    // --- Process Author ---
                    GitHubUser trackedAuthor;
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

                    // --- Process Assignees ---
                    var trackedAssignees = new List<GitHubUser>();
                    foreach (var assignee in issue.Assignees)
                    {
                        GitHubUser trackedAssignee;
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

                    // --- Process Labels ---
                    var trackedLabels = new List<GitHubLabel>();
                    foreach (var label in issue.Labels)
                    {
                        GitHubLabel trackedLabel;
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

                    // --- Process Issue (Add or Update) ---
                    var existingIssue = context.Issues
                                            .Include(i => i.Assignees) // Include collections for update
                                            .Include(i => i.Labels)
                                            .FirstOrDefault(i => i.Id == issue.Id);

                    if (existingIssue != null)
                    {
                        // Update existing issue
                        context.Entry(existingIssue).CurrentValues.SetValues(issue);
                        // Update collections by replacing them
                        existingIssue.Assignees = issue.Assignees;
                        existingIssue.Labels = issue.Labels;
                    }
                    else
                    {
                        // Add new issue - EF Core will handle relationships
                        context.Issues.Add(issue);
                    }
                }

                context.SaveChanges(); // Save all changes at once
                Console.WriteLine($"Successfully saved/updated {issues.Count} issues/pull requests to {DatabasePath}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving data: {ex.Message}");
                // Log inner exception details if available
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                // Consider logging the full exception details including stack trace for deeper debugging
                // Console.WriteLine(ex.ToString());
            }
        }

        static void RunReportCommand(ReportOptions opts)
        {
            try
            {
                string format = opts.Format?.ToLower() ?? "csv";
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

                // Parse optional date filters
                DateTime? startDate = null;
                DateTime? endDate = null;
                if (!string.IsNullOrEmpty(opts.StartDate) && DateTime.TryParse(opts.StartDate, out var parsedStartDate))
                {
                    startDate = parsedStartDate;
                }
                 if (!string.IsNullOrEmpty(opts.EndDate) && DateTime.TryParse(opts.EndDate, out var parsedEndDate))
                {
                    endDate = parsedEndDate;
                }


                // Generate the report using AppDbContext
                using var context = new AppDbContext(DatabasePath);
                var reports = GenerateContributionReportEF(context, opts.DeveloperLogin, startDate, endDate);

                if (!reports.Any() || !reports.SelectMany(r => r.Contributions).Any())
                {
                    Console.WriteLine("No contribution data found for the specified criteria.");
                    return;
                }

                // Determine date range string for filename
                string dateRangeString;
                if (startDate.HasValue && endDate.HasValue)
                {
                    dateRangeString = $"{startDate.Value.ToString("yyyyMMdd")}-{endDate.Value.ToString("yyyyMMdd")}";
                }
                else
                {
                    var allDates = reports.SelectMany(r => r.Contributions)
                                          .Select(c => c.CreatedAt)
                                          .Where(d => d.HasValue)
                                          .Select(d => d.Value)
                                          .ToList();

                    if (allDates.Any())
                    {
                        var minDate = allDates.Min();
                        var maxDate = allDates.Max();
                        dateRangeString = $"{minDate.ToString("yyyyMMdd")}-{maxDate.ToString("yyyyMMdd")}";
                    }
                    else
                    {
                        dateRangeString = "alltime"; // Fallback if no dates found
                    }
                }

                // Determine output path and writer
                TextWriter writer;
                string? outputFilePath = null;

                if (format != "console")
                {
                    // Construct path: <project_root>/src/GitHubIssueTracker/<format>/gh-contributions-<date_range>.<format>
                    // Note: AppContext.BaseDirectory points to the *output* directory (e.g., bin/Debug/net9.0)
                    string projectSourceDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
                    string outputDir = Path.Combine(projectSourceDir, format);
                    Directory.CreateDirectory(outputDir); // Ensure directory exists
                    outputFilePath = Path.Combine(outputDir, $"gh-contributions-{dateRangeString}.{format}");
                    writer = new StreamWriter(outputFilePath);
                    Console.WriteLine($"Generating report to: {outputFilePath}");
                }
                else
                {
                    writer = Console.Out; // Write to console if format is "console"
                }

                try
                {
                    formatter.Format(reports, writer);
                }
                finally
                {
                    if (writer != Console.Out)
                    {
                        writer.Dispose(); // Dispose writer only if it's a file stream
                    }
                }
                 if (outputFilePath != null)
                {
                    Console.WriteLine($"Report successfully saved to: {outputFilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating report: {ex.Message}");
                 if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                // Console.WriteLine(ex.ToString()); // Uncomment for full stack trace
            }
        }

        // New method to generate report using EF Core context
        static List<ContributionReport> GenerateContributionReportEF(AppDbContext context, string? developerLogin = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            // Base query for issues, including related data
            var query = context.Issues
                .Include(i => i.Repository)
                .Include(i => i.Assignees) // Include assignees
                .Include(i => i.Labels)    // Include labels
                .AsQueryable();

            // Apply filters
            if (startDate.HasValue)
            {
                query = query.Where(i => i.CreatedAt >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                query = query.Where(i => i.CreatedAt < endDate.Value.AddDays(1));
            }

            // Filter by assignee login *after* fetching if a specific user is requested
            // This avoids the APPLY issue but might fetch more initial data if filtering by user.
            // If not filtering by user, this filter is effectively skipped later.

            // Execute the query to bring data into memory
            var issuesFromDb = query.ToList();

            // Perform filtering and flattening in memory
            var contributions = issuesFromDb
                .SelectMany(issue => issue.Assignees.Select(assignee => new { AssigneeLogin = assignee.Login, Issue = issue })) // Flatten by assignee
                .Where(x => string.IsNullOrEmpty(developerLogin) || x.AssigneeLogin == developerLogin) // Apply user filter here
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
                        Labels = x.Issue.Labels.Select(l => l.Name).ToList() // Project label names now
                    }
                })
                .ToList();

            // Group the results by developer login in memory
            var reports = contributions
                .GroupBy(c => c.AssigneeLogin)
                .Select(g => new ContributionReport
                {
                    DeveloperLogin = g.Key,
                    Contributions = g.Select(c => c.Detail).OrderBy(d => d.CreatedAt ?? DateTime.MinValue).ThenBy(d => d.Number).ToList()
                })
                .OrderBy(r => r.DeveloperLogin)
                .ToList();

            return reports;
        }
    }
}