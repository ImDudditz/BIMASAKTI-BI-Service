# Bimasakti Reports - Windows Scheduler SQLite Database Synchronization Job
# This script executes the modern, transaction-safe .NET sync job natively.

$CompanyId = "ASHMD"
$LogPath = Join-Path $PSScriptRoot "${CompanyId}_history.log"

Function Write-Log {
    Param([string]$Level, [string]$Message)
    $Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $Line = "$Timestamp - $Level - $Message"
    Add-Content -Path $LogPath -Value $Line
}

Write-Log "INFO" "=== Windows Scheduler Sync Job Triggered ==="

# Find backend directory relative to this script
$CurrentDir = $PSScriptRoot
$BackendDir = $null
While ($CurrentDir) {
    If (Test-Path (Join-Path $CurrentDir "BiPortal.csproj")) {
        $BackendDir = $CurrentDir
        Break
    }
    $Parent = Split-Path $CurrentDir -Parent
    If ($Parent -eq $CurrentDir) { Break }
    $CurrentDir = $Parent
}

If ($null -eq $BackendDir) {
    Write-Log "CRITICAL" "Could not find BiPortal backend project directory."
    Exit 1
}

Write-Log "INFO" "Running standalone C# database sync via dotnet CLI..."
Try {
    $Output = dotnet run --project "$BackendDir\BiPortal.csproj" -- --sync $CompanyId 2>&1
    $ExitCode = $LASTEXITCODE
    
    If ($ExitCode -eq 0) {
        Write-Log "INFO" "Sync Job Completed successfully: $Output"
    } Else {
        Write-Log "ERROR" "Sync Job failed with exit code $ExitCode. Output: $Output"
    }
} Catch {
    Write-Log "CRITICAL" "Failed to execute dotnet command: $_"
}

Write-Log "INFO" "=== Windows Scheduler Sync Job Ended ==="
