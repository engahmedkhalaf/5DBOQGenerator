# release.ps1 — one-command release for RUKN 5D BOQ Manager.
#
# Usage:
#   .\release.ps1 -Version 1.1.0                            # interactive notes
#   .\release.ps1 -Version 1.1.0 -Notes "Bug fix in trial"  # explicit notes
#   .\release.ps1 -Version 1.1.0 -DryRun                    # show plan, don't execute
#
# What it does, in order:
#   1. Validates version format
#   2. Updates <Version>, <AssemblyVersion>, <FileVersion> in RuknBoqMapper.csproj
#   3. Updates #define MyAppVersion in RuknBoqMapperInstaller.iss
#   4. dotnet build (Release)
#   5. ISCC.exe -> compiles Output/RUKN_5D_BOQ_Manager_Setup.exe
#   6. git commit + tag v<version>
#   7. If `gh` CLI is installed: create GitHub release, upload installer, push tag
#      Otherwise: push tag and open the Releases page in the browser for manual upload

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Notes = "",

    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = $PSScriptRoot
$CsprojPath = Join-Path $ProjectRoot 'RuknBoqMapper\RuknBoqMapper.csproj'
$IssPath = Join-Path $ProjectRoot 'RuknBoqMapperInstaller.iss'
$InstallerOutput = Join-Path $ProjectRoot 'Output\RUKN_5D_BOQ_Manager_Setup.exe'
$IsccPath = 'C:\Program Files\Inno Setup 7\ISCC.exe'
$GitHubRepo = 'engahmedkhalaf/RUKN_5D_BOQ_Manager_Setup'

function Write-Step($msg) {
    Write-Host ""
    Write-Host "==> $msg" -ForegroundColor Cyan
}

function Write-Ok($msg) { Write-Host "    [OK] $msg" -ForegroundColor Green }
function Write-Skip($msg) { Write-Host "    [SKIP] $msg" -ForegroundColor Yellow }
function Fail($msg) { Write-Host "    [FAIL] $msg" -ForegroundColor Red; exit 1 }

# 1. Validate version
Write-Step "Validating version $Version"
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Fail "Version must look like 1.2.3 (got: $Version)"
}
$Tag = "v$Version"
$AssemblyVersion = "$Version.0"
Write-Ok "Version $Version, tag $Tag, assembly $AssemblyVersion"

# 2. Pre-flight checks
Write-Step "Pre-flight checks"
if (-not (Test-Path $CsprojPath)) { Fail "csproj not found at $CsprojPath" }
if (-not (Test-Path $IssPath)) { Fail "Installer script not found at $IssPath" }
if (-not (Test-Path $IsccPath)) { Fail "Inno Setup compiler not found at $IsccPath" }
$gitStatus = git status --short
if ($gitStatus -and -not $DryRun) {
    Write-Host "    Uncommitted changes detected:" -ForegroundColor Yellow
    Write-Host $gitStatus
    $ans = Read-Host "    Continue anyway? (y/N)"
    if ($ans -ne 'y') { Fail "Aborted by user" }
}
Write-Ok "Tools and repo state look good"

if ($DryRun) {
    Write-Host ""
    Write-Host "DRY RUN - would:" -ForegroundColor Magenta
    Write-Host "  1. Update csproj to Version=$Version, AssemblyVersion=$AssemblyVersion"
    Write-Host "  2. Update .iss MyAppVersion=$Version"
    Write-Host "  3. dotnet build (Release)"
    Write-Host "  4. ISCC.exe -> $InstallerOutput"
    Write-Host "  5. git commit + tag $Tag + push"
    Write-Host "  6. gh release create $Tag --notes '$Notes'"
    exit 0
}

# 3. Bump csproj
Write-Step "Bumping csproj version"
$csproj = Get-Content $CsprojPath -Raw
$csproj = $csproj -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
$csproj = $csproj -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$AssemblyVersion</AssemblyVersion>"
$csproj = $csproj -replace '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$AssemblyVersion</FileVersion>"
Set-Content -Path $CsprojPath -Value $csproj -NoNewline
Write-Ok "csproj updated"

# 4. Bump .iss
Write-Step "Bumping installer script version"
$iss = Get-Content $IssPath -Raw
$iss = $iss -replace '#define MyAppVersion ".*?"', "#define MyAppVersion `"$Version`""
Set-Content -Path $IssPath -Value $iss -NoNewline
Write-Ok ".iss updated"

# 5. Build add-in (Release)
Write-Step "Building add-in (Release)"
dotnet build $CsprojPath -c Release -nologo --no-incremental | Out-Host
if ($LASTEXITCODE -ne 0) { Fail "dotnet build failed" }
Write-Ok "Build succeeded"

# 6. Compile installer
Write-Step "Compiling installer with Inno Setup"
& $IsccPath $IssPath | Out-Host
if ($LASTEXITCODE -ne 0) { Fail "Inno Setup compile failed" }
if (-not (Test-Path $InstallerOutput)) { Fail "Installer output not found: $InstallerOutput" }
$sizeText = '{0:N2} MB' -f ((Get-Item $InstallerOutput).Length / 1MB)
Write-Ok "Installer built: $InstallerOutput ($sizeText)"

# 7. Get release notes if not supplied
if (-not $Notes) {
    Write-Step "Release notes"
    Write-Host "    Enter notes (one per line). Empty line to finish:" -ForegroundColor Yellow
    $lines = @()
    while ($true) {
        $line = Read-Host "    "
        if ([string]::IsNullOrWhiteSpace($line)) { break }
        $lines += "- $line"
    }
    if ($lines.Count -gt 0) { $Notes = $lines -join [Environment]::NewLine } else { $Notes = "Release $Version" }
}

# 8. Commit + tag
Write-Step "Committing version bump"
git add $CsprojPath $IssPath | Out-Null
git commit -m "Release $Version" | Out-Host
if ($LASTEXITCODE -ne 0) { Fail "git commit failed" }
git tag -a $Tag -m "Release $Version" | Out-Host
if ($LASTEXITCODE -ne 0) { Fail "git tag failed" }
Write-Ok "Committed and tagged $Tag"

# 9. Push
Write-Step "Pushing main + tag to origin"
git push origin main | Out-Host
git push origin $Tag | Out-Host
Write-Ok "Pushed"

# 10. Create GitHub release
Write-Step "Creating GitHub release"
$gh = Get-Command gh -ErrorAction SilentlyContinue
if ($gh) {
    $notesFile = New-TemporaryFile
    Set-Content -Path $notesFile -Value $Notes
    & gh release create $Tag $InstallerOutput --repo $GitHubRepo --title "Version $Version" --notes-file $notesFile | Out-Host
    Remove-Item $notesFile -ErrorAction SilentlyContinue
    if ($LASTEXITCODE -ne 0) { Fail "gh release create failed" }
    Write-Ok "Release published: https://github.com/$GitHubRepo/releases/tag/$Tag"
} else {
    Write-Skip "gh CLI not installed. Opening GitHub Releases page for manual upload."
    Write-Host ""
    Write-Host "    Upload this file as the release asset:" -ForegroundColor Yellow
    Write-Host "      $InstallerOutput" -ForegroundColor White
    Write-Host ""
    Write-Host "    Release notes:" -ForegroundColor Yellow
    Write-Host $Notes
    Write-Host ""
    $url = 'https://github.com/' + $GitHubRepo + '/releases/new?tag=' + $Tag + '&title=Version+' + $Version
    Start-Process $url
}

Write-Host ""
Write-Host "Done. Users will see the update prompt on next Revit launch." -ForegroundColor Green
