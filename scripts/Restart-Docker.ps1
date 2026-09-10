param(
    [switch]$NoCache,
    [switch]$FollowLogs
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

Write-Host "Stopping and removing Docker containers..."
docker compose down --remove-orphans

Write-Host "Building Docker images..."
if ($NoCache) {
    docker compose build --no-cache
}
else {
    docker compose build
}

Write-Host "Starting Docker containers..."
docker compose up -d --force-recreate

if ($FollowLogs) {
    Write-Host "Streaming logs (Ctrl+C to stop)..."
    docker compose logs -f
}
else {
    Write-Host "Done. App should be running on http://localhost:8080"
    Write-Host "Use: docker compose logs -f to inspect startup logs"
}
