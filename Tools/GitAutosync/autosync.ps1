# Auto-sync THE AFTER project to GitHub.
#
# Polls the repo every $IntervalSeconds. Whenever there is anything staged
# or unstaged (git status is not clean) it commits everything and pushes.
# Polling rather than a file-system watcher on purpose: Unity writes dozens
# of files in quick succession on every asset reimport, and a watcher fires
# once per file - a poll loop collapses a whole burst of Editor writes into
# one commit instead of a hundred.
#
# Every commit is a point you can roll back to. See ROLLBACK.md next to
# this script for how.

$RepoPath        = "C:\Users\omgpo\THE AFTER"
$IntervalSeconds = 90

$LogPath = Join-Path $PSScriptRoot "autosync.log"

function Write-Log {
    param([string]$Message)
    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Message
    Add-Content -Path $LogPath -Value $line
}

Set-Location $RepoPath
Write-Log "=== autosync started (PID $PID), polling every ${IntervalSeconds}s ==="

while ($true) {
    try {
        $status = git status --porcelain 2>&1
        if ($LASTEXITCODE -eq 0 -and $status) {
            git add -A *> $null

            $stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
            $filesChanged = (git diff --cached --name-only | Measure-Object -Line).Lines
            git commit -m "Auto-sync $stamp ($filesChanged file(s))" -q 2>&1 | Out-Null

            $pushOut = git push 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Log "committed + pushed - $filesChanged file(s) - $stamp"
            } else {
                # Left committed locally either way - nothing is lost, the
                # next successful push carries this commit along with it.
                Write-Log "PUSH FAILED (kept the local commit, will retry next cycle): $pushOut"
            }
        }
    } catch {
        Write-Log "ERROR: $_"
    }

    Start-Sleep -Seconds $IntervalSeconds
}
