using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GitHubIssueTracker.Models;

public class GitHubIssue
{
    [JsonPropertyName("id")] public required string Id { get; set; }

    [JsonPropertyName("number")] public int Number { get; set; }

    [JsonPropertyName("title")] public required string Title { get; set; }

    [JsonPropertyName("state")] public required string State { get; set; }

    [JsonPropertyName("createdAt")] public required DateTime CreatedAt { get; set; }

    [JsonPropertyName("closedAt")] public DateTime? ClosedAt { get; set; }

    [JsonPropertyName("isPullRequest")] public bool IsPullRequest { get; set; }

    [JsonPropertyName("author")] public required GitHubUser Author { get; set; }

    // Foreign Key for Author - Made nullable, removed 'required'
    [ForeignKey("Author")] public string? AuthorId { get; set; }

    [JsonPropertyName("assignees")] public required List<GitHubUser> Assignees { get; set; }

    [JsonPropertyName("labels")] public required List<GitHubLabel> Labels { get; set; }

    [JsonPropertyName("repository")] public required GitHubRepository Repository { get; set; }

    // Foreign Key for Repository - Made nullable, removed 'required'
    [ForeignKey("Repository")] public string? RepositoryNameWithOwner { get; set; }

    [JsonIgnore] public List<GitHubIssue> Issues { get; set; } = new();
}

public class GitHubUser
{
    [Key] 
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("login")] public required string Login { get; set; }

    [JsonPropertyName("url")] public required string Url { get; set; }

    [JsonPropertyName("type")] public required string Type { get; set; }

    [JsonPropertyName("is_bot")] public bool IsBot { get; set; }

    [JsonIgnore] public List<GitHubIssue> Issues { get; set; } = new();
}

public class GitHubLabel
{
    [Key] // Explicitly mark Id as Primary Key
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("name")] public required string Name { get; set; }

    [JsonPropertyName("description")] public required string Description { get; set; }

    [JsonPropertyName("color")] public required string Color { get; set; }

    [JsonIgnore] public List<GitHubIssue> Issues { get; set; } = new();
}

public class GitHubRepository
{
    [Key]
    [JsonPropertyName("id")]
    public required string Id { get; set; }
    
    [JsonPropertyName("name")] public required string Name { get; set; }

    [JsonPropertyName("nameWithOwner")]
    public required string NameWithOwner { get; set; }
    
    [JsonIgnore]
    public List<GitHubIssue> Issues { get; set; } = new();
}