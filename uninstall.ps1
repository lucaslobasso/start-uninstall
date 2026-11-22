<#
.SYNOPSIS
  Stops Start Uninstall and removes the autostart entry, the Start menu shortcut and the exe.
.PARAMETER Purge
  Also delete the log (%LOCALAPPDATA%\StartUninstall).
#>
param([switch]$Purge)
. (Join-Path $PSScriptRoot 'install-lib.ps1')
Uninstall-StartUninstall -Purge:$Purge
