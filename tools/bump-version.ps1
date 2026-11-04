<#
.SYNOPSIS
    Bir sürüm yayımını hazırlar: sürüm numarasını her yerde günceller, CHANGELOG'u ve PublicAPI dosyalarını taşır.

.DESCRIPTION
    Yapılanlar (dosyalar değişir, commit atılmaz; git diff ile gözden geçirin):
      - Directory.Build.props: <Version> yeni sürüm olur.
      - src/*/*.csproj: <PackageValidationBaselineVersion> önceki sürüm olur (paket doğrulaması ona göre yapılır).
      - README.md, README.en.md, docs/nuget/*.md, website (html, llms.txt, strings.json): eski sürüm numarası yenisiyle değişir.
      - CHANGELOG.md: [Unreleased] altındaki maddeler "## [<sürüm>] - <tarih>" başlığına geçer, karşılaştırma linkleri güncellenir.
      - src/*/PublicAPI.Unshipped.txt: satırlar PublicAPI.Shipped.txt'ye taşınır (*REMOVED* satırları Shipped'den siler).
      - src/FormWind.Analyzers/AnalyzerReleases.Unshipped.md: kurallar Shipped'e "## Release <x.y.z>" altında taşınır.
    Ardından tools/check-docs.ps1 çalışır. Sonra: commit ("chore: release <sürüm>"), etiket (v<sürüm>) ve push; release.yml
    paketleri yayımlar.

.EXAMPLE
    .\tools\bump-version.ps1 -Version 0.1.5-preview.1
.EXAMPLE
    .\tools\bump-version.ps1 -Version 0.2.0 -Date 2026-12-01
#>
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$Date = (Get-Date -Format "yyyy-MM-dd")
)

$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
$utf8 = New-Object System.Text.UTF8Encoding($false)

if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw "'$Version' is not a semantic version (for example 0.1.5-preview.1)." }
if ($Date -notmatch '^\d{4}-\d{2}-\d{2}$') { throw "'$Date' is not a date (yyyy-MM-dd)." }

function Read-Text([string]$path) { [IO.File]::ReadAllText($path, $utf8) }
function Write-Text([string]$path, [string]$text) { [IO.File]::WriteAllText($path, $text, $utf8) }
function NewLine([string]$text) { if ($text.Contains("`r`n")) { "`r`n" } else { "`n" } }
function Relative([string]$path) { $path.Substring($repo.Length + 1).Replace("\", "/") }
$changed = New-Object System.Collections.Generic.List[string]
function Save([string]$path, [string]$before, [string]$after) {
    if ($before -ne $after) { Write-Text $path $after; $changed.Add((Relative $path)) }
}

$propsPath = Join-Path $repo "Directory.Build.props"
$props = Read-Text $propsPath
$match = [regex]::Match($props, '<Version>([^<]+)</Version>')
if (-not $match.Success) { throw "Directory.Build.props has no <Version>." }
$old = $match.Groups[1].Value
if ($old -eq $Version) { throw "The version is already $Version." }

# --- CHANGELOG önce denetlenir: boş bir sürüm yayımlanmaz ---
$changelogPath = Join-Path $repo "CHANGELOG.md"
$changelog = Read-Text $changelogPath
$nl = NewLine $changelog
if ($changelog.Contains("## [$Version]")) { throw "CHANGELOG.md already has a $Version section." }
$unreleased = [regex]::Match($changelog, '(?m)^## \[Unreleased\][ \t]*\r?\n(?<body>[\s\S]*?)(?=^## \[)')
if (-not $unreleased.Success) { throw "CHANGELOG.md has no '## [Unreleased]' section followed by a release." }
if ($unreleased.Groups["body"].Value -notmatch '(?m)^\s*[-*] ') { throw "CHANGELOG.md: [Unreleased] has no entries." }

Write-Host "==> $old -> $Version" -ForegroundColor Cyan

# --- Sürüm ve paket doğrulama tabanı ---
Save $propsPath $props ($props.Replace($match.Value, "<Version>$Version</Version>"))
foreach ($project in Get-ChildItem (Join-Path $repo "src") -Recurse -Filter *.csproj) {
    $text = Read-Text $project.FullName
    Save $project.FullName $text ([regex]::Replace($text, '<PackageValidationBaselineVersion>[^<]*</PackageValidationBaselineVersion>', "<PackageValidationBaselineVersion>$old</PackageValidationBaselineVersion>"))
}

# --- Belgelerdeki sürüm numaraları (tools/check-docs.ps1 ile aynı dosyalar) ---
$website = Join-Path $repo "website"
$docs = @(
    Get-Item (Join-Path $repo "README.md"), (Join-Path $repo "README.en.md"), (Join-Path $website "llms.txt"), (Join-Path $website "strings.json")
    Get-ChildItem (Join-Path $repo "docs/nuget") -Filter *.md
    Get-ChildItem $website -Recurse -Include *.html
)
$pattern = '(?<![\w.])' + [regex]::Escape($old) + '(?![\w.])'
foreach ($file in $docs) {
    $text = Read-Text $file.FullName
    Save $file.FullName $text ([regex]::Replace($text, $pattern, $Version))
}

# --- CHANGELOG: yeni başlık ve karşılaştırma linkleri ---
$after = $changelog.Insert($unreleased.Index + $unreleased.Length - $unreleased.Groups["body"].Length, "$nl## [$Version] - $Date$nl")
$link = [regex]::Match($after, '(?m)^\[Unreleased\]: (?<base>\S+)/compare/(?<from>\S+)\.\.\.HEAD[ \t]*(?=\r?\n|\z)')
if ($link.Success) {
    $base = $link.Groups["base"].Value
    $after = $after.Remove($link.Index, $link.Length).Insert($link.Index,
        "[Unreleased]: $base/compare/v$Version...HEAD$nl[$Version]: $base/compare/v$old...v$Version")
}
else { Write-Warning "CHANGELOG.md has no '[Unreleased]: .../compare/...HEAD' link; add the links by hand." }
Save $changelogPath $changelog $after

# --- PublicAPI: Unshipped -> Shipped ---
foreach ($unshippedFile in Get-ChildItem (Join-Path $repo "src") -Recurse -Filter PublicAPI.Unshipped.txt) {
    $shippedPath = Join-Path $unshippedFile.DirectoryName "PublicAPI.Shipped.txt"
    $unshipped = Read-Text $unshippedFile.FullName
    $entries = @($unshipped -split '\r?\n' | Where-Object { $_.Trim() -and $_ -notmatch '^#nullable' })
    if ($entries.Count -eq 0) { continue }
    $shipped = Read-Text $shippedPath
    $snl = NewLine $shipped
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.AddRange([string[]]($shipped.TrimEnd() -split '\r?\n'))
    foreach ($entry in $entries) {
        if ($entry.StartsWith("*REMOVED*")) {
            if (-not $lines.Remove($entry.Substring(9))) { Write-Warning "$(Relative $shippedPath): '$($entry.Substring(9))' to remove was not found." }
        }
        else { $lines.Add($entry) }
    }
    Save $shippedPath $shipped (($lines -join $snl) + $snl)
    $header = @($unshipped -split '\r?\n' | Where-Object { $_ -match '^#nullable' })
    Save $unshippedFile.FullName $unshipped ((($header + "") -join (NewLine $unshipped)))
}

# --- Çözümleyici kuralları: Unshipped -> Shipped ---
$analyzers = Join-Path $repo "src/FormWind.Analyzers"
$unshippedRules = Join-Path $analyzers "AnalyzerReleases.Unshipped.md"
$shippedRules = Join-Path $analyzers "AnalyzerReleases.Shipped.md"
if ((Test-Path $unshippedRules) -and (Test-Path $shippedRules)) {
    $text = Read-Text $unshippedRules
    $rnl = NewLine $text
    $lines = @($text -split '\r?\n')
    $first = [Array]::FindIndex($lines, [Predicate[string]] { param($l) $l -match '^### ' })
    if ($first -ge 0 -and ($lines | Where-Object { $_ -match '^[A-Z]+\d+\s*\|' })) {
        # Release izleme yalnızca x.y.z kabul eder (RS2007); ön sürüm eki atılır.
        $release = ($Version -split '-')[0]
        $shipped = Read-Text $shippedRules
        if ($shipped -match "(?m)^## Release $([regex]::Escape($release))\s*$") {
            Write-Warning "AnalyzerReleases.Shipped.md already has 'Release $release'; move the rules in AnalyzerReleases.Unshipped.md by hand."
        }
        else {
            $body = ($lines[$first..($lines.Count - 1)] -join $rnl).TrimEnd()
            Save $shippedRules $shipped ($shipped.TrimEnd() + "$rnl$rnl## Release $release$rnl$rnl$body$rnl")
            Save $unshippedRules $text ((($lines[0..($first - 1)] -join $rnl).TrimEnd()) + $rnl)
        }
    }
}

Write-Host "Changed:" -ForegroundColor Cyan
$changed | ForEach-Object { Write-Host "  $_" }

& (Join-Path $PSScriptRoot "check-docs.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Next: review 'git diff', then commit 'chore: release $Version', tag v$Version and push the tag (release.yml publishes)." -ForegroundColor Green
