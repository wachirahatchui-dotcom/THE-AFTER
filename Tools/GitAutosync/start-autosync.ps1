# Re-enables the scheduled task and starts the watcher immediately, without
# waiting for the next login.

Enable-ScheduledTask -TaskName "THE AFTER - Git Autosync" -ErrorAction SilentlyContinue | Out-Null
Start-ScheduledTask -TaskName "THE AFTER - Git Autosync"
Write-Host "Autosync started. Check Tools\GitAutosync\autosync.log for activity."
