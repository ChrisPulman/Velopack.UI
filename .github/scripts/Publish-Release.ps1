Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Signing.ps1"

function Invoke-Checked([string] $Command, [string[]] $Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed with exit code $LASTEXITCODE." }
}

# Reject non-stable tags before any signing or publishing operations.
if ($env:RELEASE_TAG -notmatch '\A[vV]?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z') {
    throw 'Release tag must be MAJOR.MINOR.PATCH, vMAJOR.MINOR.PATCH or VMAJOR.MINOR.PATCH (stable only).'
}
$version = $env:RELEASE_TAG.TrimStart([char[]]'vV')
$numericVersion = [version]$version
if (@($numericVersion.Major, $numericVersion.Minor, $numericVersion.Build) | Where-Object { $_ -gt 65534 }) {
    throw 'Release version components must fit Windows assembly version fields (0-65534).'
}
Assert-SigningConfiguration
foreach ($name in @('GITHUB_REPOSITORY', 'GH_TOKEN', 'RUNNER_TEMP')) {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) { throw "Missing required environment variable: $name." }
}
Get-Command gh, dotnet, pwsh -ErrorAction Stop | Out-Null
$repository = $env:GITHUB_REPOSITORY
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location -LiteralPath $repoRoot

# Unique output prevents stale artifacts on persistent signing runners.
$workRoot = Join-Path $env:RUNNER_TEMP ("velopack-release-" + [guid]::NewGuid().ToString('N'))
$publishDir = Join-Path $workRoot 'publish'
$releaseDir = Join-Path $workRoot 'Releases'
$verifyDir = Join-Path $workRoot 'verify'
New-Item -ItemType Directory -Path $publishDir, $releaseDir | Out-Null

try {
    $releasesJson = & gh api --paginate --slurp "repos/$repository/releases?per_page=100"
    if ($LASTEXITCODE -ne 0) { throw 'Cannot list GitHub releases.' }
    $releases = @($releasesJson | ConvertFrom-Json | ForEach-Object { $_ } | ForEach-Object { $_ })
    $current = @($releases | Where-Object { $_.tag_name -eq $env:RELEASE_TAG })
    if ($current.Count -ne 1 -or $current[0].draft -or $current[0].prerelease) { throw 'Target must be an existing published stable GitHub release.' }

    # The new published release has no update assets yet. Select a completed prior
    # release explicitly rather than treating a download/network failure as bootstrap.
    $previous = $null
    foreach ($release in $releases) {
        if ($release.draft -or $release.prerelease -or $release.tag_name -eq $env:RELEASE_TAG) { continue }
        if ($release.tag_name -notmatch '\A[vV]?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z') { continue }
        if ('releases.win.json' -notin @($release.assets | ForEach-Object { $_.name })) { continue }
        $candidateVersion = [version]$release.tag_name.TrimStart([char[]]'vV')
        if ($candidateVersion -ge $numericVersion) { throw 'Release version must increase beyond every completed stable Windows release.' }
        if ($null -eq $previous -or $candidateVersion -gt [version]$previous.tag_name.TrimStart([char[]]'vV')) { $previous = $release }
    }
    if ($null -ne $previous) {
        Invoke-Checked 'gh' @('release', 'download', $previous.tag_name, '--repo', $repository, '--pattern', 'Velopack.UI*-full.nupkg', '--pattern', 'releases.win.json', '--dir', $releaseDir)
        if (@(Get-ChildItem -LiteralPath $releaseDir -Filter '*-full.nupkg').Count -eq 0) { throw 'Prior stable release has no full package for delta generation.' }
    }

    Invoke-Checked 'dotnet' @('restore', 'src/Velopack.UI.Solution.slnx')
    Invoke-Checked 'dotnet' @('build', 'src/Velopack.UI.Solution.slnx', '--configuration', 'Release', '--no-restore', '--warnaserror')
    Invoke-Checked 'dotnet' @('run', '--project', 'src/Velopack.UI.Tests/Velopack.UI.Tests.csproj', '--configuration', 'Release', '--no-build', '--', '--report-trx', '--results-directory', (Join-Path $workRoot 'test-results'))
    # ReleaseVersion selects SDK attributes instead of Git-derived attributes.
    # Verify the actual binary before any signing or packaging operation.
    $versionProperties = @("-p:ReleaseVersion=$version")
    Invoke-Checked 'dotnet' (@('publish', 'src/Velopack.UI/Velopack.UI.csproj', '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true', '--output', $publishDir, '--warnaserror', '-p:PublishSingleFile=false', '-p:PublishTrimmed=false') + $versionProperties)
    foreach ($binary in @('Velopack.UI.dll', 'Velopack.UI.exe')) {
        $info = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $publishDir $binary))
        if ($info.FileVersion -ne "$version.0" -or $info.ProductVersion -ne $version) { throw "Published $binary version does not match the release tag." }
    }

    # Sign additional payloads before packing, including scripts and nested installers that
    # Velopack's PE-only payload scan does not visit. Keep valid vendor signatures.
    $signableExtensions = @('.efi', '.msi', '.msp', '.mst', '.cab', '.cat', '.ps1', '.psm1', '.psd1', '.ps1xml', '.vbs', '.vbe', '.js', '.wsf')
    foreach ($file in Get-ChildItem -LiteralPath $publishDir -Recurse -File | Where-Object { $_.Extension.ToLowerInvariant() -in $signableExtensions }) {
        Invoke-ReleaseSigning $file.FullName
    }
    Invoke-Checked 'dotnet' @('tool', 'install', 'vpk', '--version', '1.2.161', '--tool-path', (Join-Path $workRoot 'tools'))
    $vpk = Join-Path $workRoot 'tools/vpk.exe'
    # Velopack signs EXE/DLL after resource edits, and signs installer artifacts.
    # Velopack supplies a quoted path for {{file}}. Secrets stay in inherited env.
    $signScript = Join-Path $PSScriptRoot 'Sign-ReleaseFile.ps1'
    $env:VPK_SIGN_TEMPLATE = 'pwsh -NoProfile -NonInteractive -File "' + $signScript + '" -FilePath {{file}}'
    Invoke-Checked $vpk @('pack', '--packId', 'Velopack.UI', '--packVersion', $version, '--packDir', $publishDir, '--mainExe', 'Velopack.UI.exe', '--packTitle', 'Velopack.UI', '--packAuthors', 'Chris Pulman', '--runtime', 'win-x64', '--channel', 'win', '--icon', 'src/Velopack.UI/icon.ico', '--outputDir', $releaseDir, '--yes', '--skip-updates')

    $fullPackages = @(Get-ChildItem -LiteralPath $releaseDir -Filter '*-full.nupkg' | Where-Object { $_.Name -match ('-' + [regex]::Escape($version) + '-full\.nupkg$') })
    $deltaPackages = @(Get-ChildItem -LiteralPath $releaseDir -Filter '*-delta.nupkg' | Where-Object { $_.Name -match ('-' + [regex]::Escape($version) + '-delta\.nupkg$') })
    $installers = @(Get-ChildItem -LiteralPath $releaseDir -Filter '*-Setup.exe')
    if ($fullPackages.Count -ne 1 -or $installers.Count -ne 1) { throw 'Expected exactly one new full package and Setup installer.' }
    if ($null -ne $previous -and $deltaPackages.Count -ne 1) { throw 'A prior release exists but no delta package was produced.' }
    $manifestPath = Join-Path $releaseDir 'releases.win.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $newAssets = @($manifest.Assets | Where-Object { $_.Version -eq $version })
    if (@($newAssets | Where-Object { $_.Type -eq 'Full' }).Count -ne 1) { throw 'Update manifest does not advertise the new full package.' }
    if ($null -ne $previous -and @($newAssets | Where-Object { $_.Type -eq 'Delta' }).Count -ne 1) { throw 'Update manifest does not advertise the new delta package.' }
    # GithubSource binds each manifest entry to the release containing that index.
    # Prior versions remain on their original releases, so omit them from this index.
    $manifest.Assets = $newAssets
    $manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM

    # Verify the distributed payload, not just intermediate publish files.
    [IO.Compression.ZipFile]::ExtractToDirectory($fullPackages[0].FullName, $verifyDir)
    foreach ($name in @('Velopack.UI.exe', 'Velopack.UI.dll')) {
        $files = @(Get-ChildItem -LiteralPath $verifyDir -Recurse -File -Filter $name)
        if ($files.Count -ne 1) { throw "Missing or ambiguous packaged application binary: $name." }
        Assert-ReleaseSignature $files[0].FullName
    }
    Assert-ReleaseSignature $installers[0].FullName

    # Upload payloads first and the index last. Keep the release title/body/tag;
    # never create a second release or replace its other assets.
    $payloads = @($fullPackages + $deltaPackages + $installers + @(Get-ChildItem -LiteralPath $releaseDir -Filter '*-Portable.zip'))
    Invoke-Checked 'gh' (@('release', 'upload', $env:RELEASE_TAG, '--repo', $repository, '--clobber') + @($payloads.FullName))
    Invoke-Checked 'gh' @('release', 'upload', $env:RELEASE_TAG, $manifestPath, '--repo', $repository, '--clobber')
    Write-Host "Signed Windows $version published to https://github.com/$repository/releases/tag/$($env:RELEASE_TAG)"
}
finally {
    Remove-Item Env:VPK_SIGN_TEMPLATE -ErrorAction SilentlyContinue
}
