using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GitHubIssueTracker.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "labels",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    color = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_labels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "repositories",
                columns: table => new
                {
                    name_with_owner = table.Column<string>(type: "TEXT", nullable: false),
                    id = table.Column<string>(type: "TEXT", nullable: true),
                    name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_repositories", x => x.name_with_owner);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    login = table.Column<string>(type: "TEXT", nullable: false),
                    url = table.Column<string>(type: "TEXT", nullable: false),
                    type = table.Column<string>(type: "TEXT", nullable: false),
                    is_bot = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "issues",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    number = table.Column<int>(type: "INTEGER", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    state = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    closed_at = table.Column<string>(type: "TEXT", nullable: true),
                    is_pull_request = table.Column<bool>(type: "INTEGER", nullable: false),
                    author_id = table.Column<string>(type: "TEXT", nullable: false),
                    repository_name_with_owner = table.Column<string>(type: "TEXT", nullable: false),
                    git_hub_issue_id = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_issues", x => x.id);
                    table.ForeignKey(
                        name: "fk_issues_issues_git_hub_issue_id",
                        column: x => x.git_hub_issue_id,
                        principalTable: "issues",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_issues_repositories_repository_name_with_owner",
                        column: x => x.repository_name_with_owner,
                        principalTable: "repositories",
                        principalColumn: "name_with_owner",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_issues_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "git_hub_issue_git_hub_label",
                columns: table => new
                {
                    issues_id = table.Column<string>(type: "TEXT", nullable: false),
                    labels_id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_git_hub_issue_git_hub_label", x => new { x.issues_id, x.labels_id });
                    table.ForeignKey(
                        name: "fk_git_hub_issue_git_hub_label_issues_issues_id",
                        column: x => x.issues_id,
                        principalTable: "issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_git_hub_issue_git_hub_label_labels_labels_id",
                        column: x => x.labels_id,
                        principalTable: "labels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "git_hub_issue_git_hub_user",
                columns: table => new
                {
                    assignees_id = table.Column<string>(type: "TEXT", nullable: false),
                    git_hub_issue_id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_git_hub_issue_git_hub_user", x => new { x.assignees_id, x.git_hub_issue_id });
                    table.ForeignKey(
                        name: "fk_git_hub_issue_git_hub_user_issues_git_hub_issue_id",
                        column: x => x.git_hub_issue_id,
                        principalTable: "issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_git_hub_issue_git_hub_user_users_assignees_id",
                        column: x => x.assignees_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_git_hub_issue_git_hub_label_labels_id",
                table: "git_hub_issue_git_hub_label",
                column: "labels_id");

            migrationBuilder.CreateIndex(
                name: "ix_git_hub_issue_git_hub_user_git_hub_issue_id",
                table: "git_hub_issue_git_hub_user",
                column: "git_hub_issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_issues_author_id",
                table: "issues",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_issues_git_hub_issue_id",
                table: "issues",
                column: "git_hub_issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_issues_repository_name_with_owner",
                table: "issues",
                column: "repository_name_with_owner");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "git_hub_issue_git_hub_label");

            migrationBuilder.DropTable(
                name: "git_hub_issue_git_hub_user");

            migrationBuilder.DropTable(
                name: "labels");

            migrationBuilder.DropTable(
                name: "issues");

            migrationBuilder.DropTable(
                name: "repositories");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
