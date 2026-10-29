<#
.SYNOPSIS
    Cuts a new Blink release.

.DESCRIPTION
    Writes a new version into Directory.Build.props (the single source of truth for
    both Blink and Blink Setup), rebuilds the app, rebuilds the installer so the new
    app is embedded inside it, and drops a ready-to-ship
    release/BlinkSetup-<version>.exe on disk.

    Published releases are what the in-app updater reads, so every user who already
    installed Blink is offered the new version the next time it checks.

.PARAMETER Bump
    Which part of the version to increment: major, minor, patch, or none to rebuild
    the current version.

.PARAMETER Version
    An explicit version such as 1.2.0. Overrides -Bump.

.PARAMETER Notes
    Release notes. Defaults to a short generated message.

.PARAMETER Publish
    Also commit the version bump, tag it, push, and create the GitHub release with
    the installer attached as an asset.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\Release.ps1 patch

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\Release.ps1 minor -Publish
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('major', 'minor', 'patch', 'none')]
    [string] $Bump = 'patch',

    [string] $Version,

    [string] $Notes,

    [switch] $Publish
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$propsPath = Join-Path $root 'Directory.Build.props'
$solution = Join-Path $root 'Blink.sln'
$appProject = Join-Path $root 'src\Blink\Blink.csproj'
$setupProject = Join-Path $root 'src\Blink.Setup\BlinkSetup.csproj'
$appOut = Join-Path $root 'dist'
$setupOut = Join-Path $root 'src\Blink.Setup\dist-setup'
$releaseOut = Join-Path $root 'release'
$repository = 'iammohitraj/Blink'

function Read-PropsText {
    return [System.IO.File]::ReadAllText($propsPath)
}

function Read-CurrentVersion {
    $text = Read-PropsText
    if ($text -notmatch '<Version>\s*([0-9]+\.[0-9]+\.[0-9]+)\s*</Version>') {
        throw "Could not find a <Version> element in $propsPath."
    }

    return $Matches[1]
}

function Write-Version([string] $newVersion) {
    $text = Read-PropsText
    $updated = [regex]::Replace(
        $text,
        '<Version>\s*[0-9]+\.[0-9]+\.[0-9]+\s*</Version>',
        "<Version>$newVersion</Version>",
        1)

    if ($updated -eq $text) {
        throw "Could not update the <Version> element in $propsPath."
    }

    [System.IO.File]::WriteAllText($propsPath, $updated)
}

function Step-Version([string] $current, [string] $kind) {
    $parts = $current.Split('.')
    $major = [int] $parts[0]
    $minor = [int] $parts[1]
    $patch = [int] $parts[2]

    switch ($kind) {
        'major' { $major++; $minor = 0; $patch = 0 }
        'minor' { $minor++; $patch = 0 }
        'patch' { $patch++ }
    }

    return "$major.$minor.$patch"
}

function Invoke-DotNet([string[]] $arguments) {
    Write-Host "  dotnet $($arguments -join ' ')" -ForegroundColor DarkGray
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet exited with code $LASTEXITCODE."
    }
}

$currentVersion = Read-CurrentVersion

if ($PSBoundParameters.ContainsKey('Version')) {
    if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
        throw "Version must look like 1.2.3 (got '$Version')."
    }

    $newVersion = $Version
}
else {
    $newVersion = Step-Version $currentVersion $Bump
}

Write-Host ''
Write-Host "Blink release" -ForegroundColor Cyan
Write-Host "  current version : $currentVersion"
Write-Host "  new version     : $newVersion"
Write-Host ''

if ($newVersion -ne $currentVersion) {
    Write-Version $newVersion
    Write-Host "Updated Directory.Build.props." -ForegroundColor Green
}
else {
    Write-Host "Version unchanged; rebuilding the current version." -ForegroundColor Yellow
}

# The app has to be published first: the installer embeds dist\Blink.exe at build time.
Write-Host ''
Write-Host 'Step 1/4  Running the test suite' -ForegroundColor Cyan
Invoke-DotNet @('test', $solution, '-c', 'Release', '--nologo')

Write-Host ''
Write-Host 'Step 2/4  Building Blink.exe' -ForegroundColor Cyan
Invoke-DotNet @(
    'publish', $appProject,
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    '-o', $appOut
)

$appExe = Join-Path $appOut 'Blink.exe'
if (-not (Test-Path $appExe)) {
    throw "Blink.exe was not produced at $appExe."
}

Write-Host ''
Write-Host 'Step 3/4  Building BlinkSetup.exe' -ForegroundColor Cyan
Invoke-DotNet @(
    'publish', $setupProject,
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    '-o', $setupOut
)

$setupExe = Join-Path $setupOut 'BlinkSetup.exe'
if (-not (Test-Path $setupExe)) {
    throw "BlinkSetup.exe was not produced at $setupExe."
}

Write-Host ''
Write-Host 'Step 4/4  Packaging the release asset' -ForegroundColor Cyan
if (-not (Test-Path $releaseOut)) {
    New-Item -ItemType Directory -Path $releaseOut | Out-Null
}

# Keep the folder to a single asset so an upload can never pick up a stale build.
Get-ChildItem -Path $releaseOut -Filter 'BlinkSetup-*.exe' -ErrorAction SilentlyContinue |
    Remove-Item -Force

$assetName = "BlinkSetup-$newVersion.exe"
$assetPath = Join-Path $releaseOut $assetName
Copy-Item -LiteralPath $setupExe -Destination $assetPath -Force

$sizeMb = [Math]::Round((Get-Item $assetPath).Length / 1MB, 1)
Write-Host "  $assetName  ($sizeMb MB)" -ForegroundColor Green

if (-not $Publish) {
    Write-Host ''
    Write-Host 'Done. Upload this file to a GitHub release to reach every installed copy:' -ForegroundColor Yellow
    Write-Host "  $assetPath"
    Write-Host "  tag: v$newVersion"
    Write-Host ''
    Write-Host 'Or re-run with -Publish to commit, tag, push and create the release.'
    return
}

Write-Host ''
Write-Host "Publishing v$newVersion to $repository" -ForegroundColor Cyan

$tag = "v$newVersion"
$pending = @(git -C $root status --porcelain)
$unexpected = @($pending | Where-Object { $_ -notmatch 'Directory\.Build\.props$' })
if ($unexpected.Count -gt 0) {
    throw "Commit or stash your other changes before publishing:`n$($unexpected -join "`n")"
}

if ([string]::IsNullOrWhiteSpace($Notes)) {
    $Notes = @"
Blink $newVersion

Run ``$assetName`` to install or update Blink on Windows 10/11 (64-bit, no admin rights required).

Already have Blink? It updates itself: open the tray menu and choose **Check for updates**, or let the background check pick this release up automatically.
"@
}

& git -C $root add -- Directory.Build.props
if ($LASTEXITCODE -ne 0) { throw 'git add failed.' }

& git -C $root commit -m "Release $newVersion"
if ($LASTEXITCODE -ne 0) { throw 'git commit failed.' }

& git -C $root tag $tag
if ($LASTEXITCODE -ne 0) { throw "git tag $tag failed (does it already exist?)." }

& git -C $root push
if ($LASTEXITCODE -ne 0) { throw 'git push failed.' }

& git -C $root push origin $tag
if ($LASTEXITCODE -ne 0) { throw 'git push of the tag failed.' }

& gh release create $tag $assetPath --repo $repository --title "Blink $newVersion" --notes $Notes
if ($LASTEXITCODE -ne 0) { throw 'gh release create failed. Is the GitHub CLI signed in?' }

Write-Host ''
Write-Host "Released Blink $newVersion." -ForegroundColor Green
Write-Host "  https://github.com/$repository/releases/tag/$tag"
