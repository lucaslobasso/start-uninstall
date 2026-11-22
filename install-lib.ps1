# Shared by install.ps1 (build from source) and uninstall.ps1.
# Works in Windows PowerShell 5.1 and PowerShell 7.

$RunKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$RunName = 'StartUninstall'
$ShortcutName = 'Start Uninstall.lnk'

function Get-SuDir { Join-Path $env:LOCALAPPDATA 'Programs\StartUninstall' }
function Get-SuLogDir { Join-Path $env:LOCALAPPDATA 'StartUninstall' }

# Only the installed copy: a test copy started from elsewhere keeps running.
function Stop-StartUninstall {
    $exe = Join-Path (Get-SuDir) 'StartUninstall.exe'
    $running = @(Get-Process StartUninstall -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe })
    if ($running.Count -eq 0) { return }
    & $exe --quit | Out-Null # asks it to unregister its UI Automation handlers and exit
    Start-Sleep -Milliseconds 500
    $running | Where-Object { -not $_.HasExited } | Stop-Process -Force -ErrorAction SilentlyContinue
}

# Start menu shortcut, so a stopped helper can be started again from Search ("Start Uninstall").
function Set-StartMenuShortcut([string]$Exe) {
    $lnk = Join-Path ([Environment]::GetFolderPath('Programs')) $ShortcutName
    $s = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk)
    $s.TargetPath = $Exe
    $s.WorkingDirectory = Split-Path $Exe
    $s.Description = 'Runs an app''s own uninstaller when you click Uninstall in Start'
    $s.Save()
}

function Remove-StartMenuShortcut {
    Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) $ShortcutName) -Force -ErrorAction SilentlyContinue
}

# Copies the exe into place, sets autostart and starts it.
function Install-StartUninstallExe([string]$Source, [switch]$NoAutostart) {
    $dest = Get-SuDir
    $exe = Join-Path $dest 'StartUninstall.exe'
    Stop-StartUninstall
    New-Item -ItemType Directory -Force $dest | Out-Null
    Copy-Item $Source $exe -Force

    if ($NoAutostart) { Remove-ItemProperty $RunKey -Name $RunName -ErrorAction SilentlyContinue }
    else { Set-ItemProperty $RunKey -Name $RunName -Value "`"$exe`"" }
    Set-StartMenuShortcut $exe
    Start-Process $exe
    $size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host "Installed $exe ($size MB) and started it." -ForegroundColor Green
    if (-not $NoAutostart) { Write-Host 'It starts with Windows.' }
    Write-Host "Log: $(Join-Path (Get-SuLogDir) 'log.txt')"
}

function Uninstall-StartUninstall([switch]$Purge) {
    Stop-StartUninstall
    Remove-ItemProperty $RunKey -Name $RunName -ErrorAction SilentlyContinue
    Remove-StartMenuShortcut
    Remove-Item (Get-SuDir) -Recurse -Force -ErrorAction SilentlyContinue
    if ($Purge) { Remove-Item (Get-SuLogDir) -Recurse -Force -ErrorAction SilentlyContinue }
    Write-Host 'Start Uninstall removed.' -ForegroundColor Green
}
