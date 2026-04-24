<#
.SYNOPSIS
    Generates a SHA256SUMS.txt manifest for every Tabu installer in a folder.

.DESCRIPTION
    Produces a file in the canonical `sha256sum -b` format expected by
    GitHubUpdateService: one line per installer, lower-case 64-char hex
    digest, two spaces, asterisk, file name. The auto-updater downloads
    this file alongside the installer and refuses to launch the .exe if
    the computed digest does not match the manifest entry.

.PARAMETER Path
    Folder containing the installer artifacts. Defaults to ./publish-output.

.PARAMETER Pattern
    Glob filter used to pick installers. Defaults to "Tabu*Setup*.exe".

.EXAMPLE
    pwsh tools/Generate-Checksums.ps1
    pwsh tools/Generate-Checksums.ps1 -Path .\publish-output -Pattern '*.exe'

.NOTES
    Audit hardening v1.6 — supply-chain integrity for the auto-update flow.
#>
[CmdletBinding()]
param(
    [string]$Path    = (Join-Path -Path $PSScriptRoot -ChildPath '..\publish-output'),
    [string]$Pattern = 'Tabu*Setup*.exe',
    [string]$OutputName = 'SHA256SUMS.txt'
)

$ErrorActionPreference = 'Stop'

$resolved = Resolve-Path -Path $Path
$installers = Get-ChildItem -Path $resolved -Filter $Pattern -File

if ($installers.Count -eq 0) {
    throw "No installer matching '$Pattern' found in '$resolved'."
}

$lines = foreach ($file in $installers) {
    $hash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "{0}  *{1}" -f $hash, $file.Name
}

$outputPath = Join-Path -Path $resolved -ChildPath $OutputName
# ASCII + LF ending matches sha256sum on Linux so cross-tool verification
# (`sha256sum -c SHA256SUMS.txt`) keeps working from WSL or git-bash.
[System.IO.File]::WriteAllText($outputPath, ($lines -join "`n") + "`n", [System.Text.Encoding]::ASCII)

Write-Host "Wrote manifest with $($installers.Count) entr$(if ($installers.Count -eq 1) {'y'} else {'ies'}) to:" -ForegroundColor Green
Write-Host "  $outputPath"
$lines | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
