# Installs or updates outta.exe from the latest GitHub release.
#   irm https://github.com/mezomi/aew-outta-context/releases/latest/download/install.ps1 | iex
# Run it again any time to update. Your clips, episodes and settings are not touched.

$ErrorActionPreference = "Stop"
$repo = "mezomi/aew-outta-context"
$dir = Join-Path $env:LOCALAPPDATA "Programs\outta"
$exe = Join-Path $dir "outta.exe"
$url = "https://github.com/$repo/releases/latest/download/outta.exe"

New-Item -ItemType Directory -Force -Path $dir | Out-Null

Write-Host "-> Downloading the latest outta.exe..."
$tmp = "$exe.download"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -Uri $url -OutFile $tmp -UseBasicParsing
Unblock-File -Path $tmp

if (Test-Path $exe) {
    # A running exe can't be overwritten, but it can be renamed out of the way.
    $old = "$exe.old"
    Remove-Item -Force -ErrorAction SilentlyContinue $old
    Rename-Item -Path $exe -NewName "outta.exe.old"
}
Move-Item -Force -Path $tmp -Destination $exe
Remove-Item -Force -ErrorAction SilentlyContinue "$exe.old"

$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
if (-not (($userPath -split ";") -contains $dir)) {
    [Environment]::SetEnvironmentVariable("Path", (($userPath.TrimEnd(";") + ";" + $dir).TrimStart(";")), "User")
    Write-Host "-> Added $dir to your PATH"
}
$env:Path = "$env:Path;$dir"

$others = Get-Command outta.exe -All -ErrorAction SilentlyContinue | Where-Object { $_.Source -ne $exe }
foreach ($o in $others) {
    Write-Host "!  An older copy is at $($o.Source) and may be used instead. Delete it: Remove-Item '$($o.Source)'"
}

Write-Host "OK Installed: outta $(& $exe --version)  ($exe)"

Write-Host "-> Getting yt-dlp (downloads and finds the videos)..."
& $exe update --yt-dlp-only
if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) {
    Write-Host "!  ffmpeg is not installed yet. Run: winget install ffmpeg"
}
Write-Host "Open a NEW terminal, then run: outta doctor"
