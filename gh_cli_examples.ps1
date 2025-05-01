# Note: 
# All examples assume you have the GitHub CLI installed and authenticated.
# It's also important that the --json fields are list in the correct order.

$yesAnswers = @("Y", "y", "Yes", "yes", "YES")
$noAnswers = @("N", "n", "No", "no", "NO")

# April 1st to April 20th, 2025
Write-Host "Would you like to fetch Apr 1st to Apr 20th, 2025 issues and save as json? (y/n)"
$Q1 = Read-Host
if ($yesAnswers -contains $Q1)
{
    gh search issues `
        --include-prs `
        --owner=team-thoroughbreds `
        --created="2025-04-01..2025-04-20" `
        -L 1000 `
        --json id,closedAt,number,title,author,isPullRequest,assignees,repository,state,labels,createdAt `
        --archived=false | Out-String | ConvertFrom-Json | ConvertTo-Json -Depth 10 > "./samples/gh-contributors-20250401-20250420.json"
}

# Jan 1st to Yesterday
Write-Host "Would you like to fetch Jan 1st to Yesterday issues and save as json? (y/n)"
$Q2 = Read-Host
if ($yesAnswers -contains $Q2)
{
    gh search issues `
        --include-prs `
        --owner=team-thoroughbreds `
        --created="2025-01-01..$( Get-Date -Format "yyyy-MM-dd" )" `
        -L 1000 `
        --json id,closedAt,number,title,author,isPullRequest,assignees,repository,state,labels,createdAt `
        --archived=false | Out-String | ConvertFrom-Json | ConvertTo-Json -Depth 10 > "./samples/gh-contributors-20250101-$( Get-Date -Format "yyyyMMdd" ).json"
}

$newestJsonFilePath = Get-ChildItem -Path "./samples" -Filter "*.json" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

Write-Host "Would you like to save the $( $newestJsonFilePath.Name ) file to the database? (y/n)"
$Q3 = Read-Host
if ($yesAnswers -contains $Q3)
{
    # Get the newest json file
    if ($newestJsonFilePath)
    {
        # Save the file to the database
        $dbPath = Get-Location + "/github-tracker.db"
        dotnet run --project ./src/GitHubTracker/GitHubTracker.csproj `
            --configuration Release `
            -- "save" `
            -i $( $newestJsonFilePath.FullName )
        Write-Host "File saved to database."
    }
}

# Fetch from GH, and save direct to db
Write-Host "Would you like to fetch issues from GH (since Jan 1st) and save direct to db? (y/n)"
$Q4 = Read-Host
if ($yesAnswers -contains $Q4)
{
    # Fetch issues from GH and save direct to db
    gh search issues `
        --include-prs `
        --owner=team-thoroughbreds `
        --created="2025-04-01..2025-04-20" `
        -L 1000 `
        --json id,closedAt,number,title,author,isPullRequest,assignees,repository,state,labels,createdAt `
        --archived=false | Out-String | ConvertFrom-Json | ConvertTo-Json -Depth 10 | dotnet run --project ./src/GitHubTracker/GitHubTracker.csproj `
        --configuration Release `
        -- "save"
}

Write-Host "No more options available. Exiting..."