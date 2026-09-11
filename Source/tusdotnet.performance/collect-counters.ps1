# collect-counters.ps1
# Collects dotnet-counters System.Runtime metrics for a running tusdotnet server process.
#
# Usage:
#   .\collect-counters.ps1 -OutputFile main
#   .\collect-counters.ps1 -OutputFile feature
#
# Output: <OutputFile>.csv in the current directory.
# Stop collection with Ctrl+C when the performance test has completed.

param(
    [Parameter(Mandatory = $true)]
    [string]$OutputFile
)

Write-Host ""
Write-Host "Listing running .NET processes..." -ForegroundColor Cyan
Write-Host ""

dotnet-counters ps

Write-Host ""
$pid = Read-Host "Enter the PID of the tusdotnet server process"

if (-not ($pid -match '^\d+$')) {
    Write-Error "Invalid PID: '$pid'. Must be a number."
    exit 1
}

Write-Host ""
Write-Host "Starting collection -> $OutputFile.csv" -ForegroundColor Green
Write-Host "Run your performance test now, then press Ctrl+C here to stop collection." -ForegroundColor Yellow
Write-Host ""

dotnet-counters collect --process-id $pid --counters System.Runtime --format csv --output $OutputFile
