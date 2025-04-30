using GitHubIssueTracker.Models;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace GitHubIssueTracker.Database
{
    public class AppDbContext : DbContext
    {
        public DbSet<GitHubRepository> Repositories { get; set; }
        public DbSet<GitHubUser> Users { get; set; }
        public DbSet<GitHubLabel> Labels { get; set; }
        public DbSet<GitHubIssue> Issues { get; set; }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={DatabaseManager.DatabasePath}");
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
                .WithMany() // Assuming a User doesn't need a navigation property back to their authored Issues
                .HasForeignKey(i => i.AuthorId)
                .IsRequired();

            // Issue -> Repository (One-to-Many: Repository can have many issues)
            modelBuilder.Entity<GitHubIssue>()
                .HasOne(i => i.Repository)
                .WithMany() // Assuming a Repository doesn't need a direct navigation property back to Issues
                .HasForeignKey(i => i.RepositoryNameWithOwner)
                .IsRequired();

            // Issue <-> Assignees (Many-to-Many)
            modelBuilder.Entity<GitHubIssue>()
                .HasMany(i => i.Assignees)
                .WithMany("AssignedIssues"); // EF Core will create a join table implicitly. "AssignedIssues" is a placeholder for the navigation property on the User side if needed.

            // Issue <-> Labels (Many-to-Many)
            modelBuilder.Entity<GitHubIssue>()
                .HasMany(i => i.Labels)
                .WithMany("LabeledIssues"); // EF Core will create a join table implicitly. "LabeledIssues" is a placeholder for the navigation property on the Label side if needed.

             // Configure DateTime properties to be stored as TEXT in SQLite
             // This ensures compatibility with the existing format if needed, though EF Core handles DateTime well.
            modelBuilder.Entity<GitHubIssue>()
                .Property(i => i.CreatedAt)
                .HasConversion(v => v.HasValue ? v.Value.ToString("o") : null,
                               v => v != null ? DateTime.Parse(v) : (DateTime?)null);

            modelBuilder.Entity<GitHubIssue>()
                .Property(i => i.ClosedAt)
                .HasConversion(v => v.HasValue ? v.Value.ToString("o") : null,
                               v => v != null ? DateTime.Parse(v) : (DateTime?)null);
        }
    }
}