# Stops the running autosync watcher and disables the scheduled task that
# restarts it at logon. Run start-autosync.ps1 (or just log in again) to
# bring it back.

Get-CimInstance Win32_Process -Filter "Name = 'powershell.exe'" |
    Where-Object { $_.CommandLine -like '*GitAutosync\autosync.ps1*' } |
    ForEach-Object {
        Write-Host "Stopping autosync process (PID $($_.ProcessId))"
        Stop-Process -Id $_.ProcessId -Force
    }

Disable-ScheduledTask -TaskName "THE AFTER - Git Autosync" -ErrorAction SilentlyContinue | Out-Null
Write-Host "Autosync stopped. The scheduled task is disabled, so it will not restart at your next login."
Write-Host "Run start-autosync.ps1 to turn it back on."
