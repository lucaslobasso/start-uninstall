<#
.SYNOPSIS
  Builds Start Uninstall from source (Native AOT) and installs it to %LOCALAPPDATA%\Programs\StartUninstall,
  starting with Windows.
.PARAMETER NoAutostart
  Don't add the HKCU Run entry (start it from the "Start Uninstall" Start menu shortcut instead).
.NOTES
  Needs the .NET 11 SDK and the Visual Studio "Desktop development with C++" workload (MSVC linker + Windows SDK)
  for Native AOT. Run from the repo root: .\install.ps1
#>
param([switch]$NoAutostart)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'install-lib.ps1')

# Native AOT's linker setup looks for vswhere.exe on PATH.
$vsInstaller = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if ((Test-Path $vsInstaller) -and ($env:PATH -notlike "*$vsInstaller*")) { $env:PATH = "$env:PATH;$vsInstaller" }

$out = Join-Path ([IO.Path]::GetTempPath()) 'start-uninstall-publish'
Write-Host 'Building (Native AOT)...'
dotnet publish (Join-Path $PSScriptRoot 'src\StartUninstall\StartUninstall.csproj') -c Release -o $out --nologo | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Build failed. Native AOT needs the VS "Desktop development with C++" workload.' }

Install-StartUninstallExe (Join-Path $out 'StartUninstall.exe') -NoAutostart:$NoAutostart
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
