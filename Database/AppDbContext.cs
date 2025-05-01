using GitHubIssueTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace GitHubIssueTracker.Database;

public class AppDbContext : DbContext
{
    public DbSet<GitHubRepository> Repositories { get; set; }
    public DbSet<GitHubUser> Users { get; set; }
    public DbSet<GitHubLabel> Labels { get; set; }
    public DbSet<GitHubIssue> Issues { get; set; }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSqlite($"Data Source={DatabaseManager.DatabasePath}")
            .UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure primary keys explicitly (though convention might handle some)
        modelBuilder.Entity<GitHubRepository>().HasKey(r => r.NameWithOwner);
        modelBuilder.Entity<GitHubUser>().HasKey(u => u.Id);
        modelBuilder.Entity<GitHubLabel>().HasKey(l => l.Id);
        modelBuilder.Entity<GitHubIssue>().HasKey(i => i.Id);

        // Configure relationships

        // Issue -> Author (One-to-Many: User can author many issues)
        modelBuilder.Entity<GitHubIssue>()
            .HasOne(i => i.Author)
            .WithMany("Issues")
            .HasForeignKey(i => i.AuthorId)
            .IsRequired();

        modelBuilder.Entity<GitHubIssue>()
            .HasOne(i => i.Repository)
            .WithMany("Issues")
            .HasForeignKey(i => i.RepositoryNameWithOwner)
            .IsRequired();

        modelBuilder.Entity<GitHubIssue>()
            .HasMany(i => i.Assignees)
            .WithMany();

        // Issue <-> Labels (Many-to-Many)
        modelBuilder.Entity<GitHubIssue>()
            .HasMany(i => i.Labels)
            .WithMany("Issues");

        // Configure DateTime properties to be stored as TEXT in SQLite
        // This ensures compatibility with the existing format if needed, though EF Core handles DateTime well.
        // modelBuilder.Entity<GitHubIssue>()
        //     .Property(i => i.CreatedAt)
        //     .HasConversion(v => v.ToString("o"),
        //                    v => v != null ? DateTime.Parse(v) : (DateTime?)null);

        modelBuilder.Entity<GitHubIssue>()
            .Property(i => i.ClosedAt)
            .HasConversion(v => v.HasValue ? v.Value.ToString("o") : null,
                v => v != null ? DateTime.Parse(v) : null);
    }
}