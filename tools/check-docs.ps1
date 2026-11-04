<#
.SYNOPSIS
    Web sitesi ve belgelerin tutarlılığını denetler (CI'da docs.yml ve release.yml çalıştırır).

.DESCRIPTION
    Kaynak denetimleri (her zaman):
      - website/en ve website/tr aynı sayfaları içerir; her sayfanın title ve description'ı vardır.
      - website/build.ps1'deki kenar çubuğu listesi ($docs) ile website/en/docs klasörü aynıdır.
      - website/strings.json'da en ve tr aynı anahtarlara sahiptir ve hiçbir değer boş değildir.
      - README.md ve README.en.md aynı sayıda ve düzeyde başlık içerir.
      - README'ler, docs/nuget, website ve llms.txt'de geçen ön sürüm numaraları Directory.Build.props'taki <Version>'dır.
    -Site verilirse derlenmiş sitede iç linkler (href/src ve #çapa) denetlenir.

    Class'ların ve kontrollerin belgelenmesi testlerdedir (LlmsTxtTests, DocsCoverageTests).

.EXAMPLE
    .\tools\check-docs.ps1
.EXAMPLE
    .\website\build.ps1 -Output artifacts/website; .\tools\check-docs.ps1 -Site artifacts/website
#>
param(
    [string]$Site
)

$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
$website = Join-Path $repo "website"
$utf8 = New-Object System.Text.UTF8Encoding($false)
$errors = New-Object System.Collections.Generic.List[string]

function Read-Text([string]$path) { [IO.File]::ReadAllText($path, $utf8) }
function Relative([string]$path) { $path.Substring($repo.Length + 1).Replace("\", "/") }
function Fail([string]$file, [string]$message) {
    $errors.Add("${file}: $message")
    if ($env:GITHUB_ACTIONS -eq "true") { Write-Output "::error file=$file::$message" }
    else { Write-Host "  $file`: $message" -ForegroundColor Red }
}
function Step([string]$name) { Write-Host "==> $name" -ForegroundColor Cyan }

# --- Sayfalar: iki dilde aynı dosyalar, her birinde title ve description ---
Step "Pages (en/tr)"
$pages = @{}
foreach ($lang in "en", "tr") {
    $folder = Join-Path $website "$lang/docs"
    $pages[$lang] = @(Get-ChildItem $folder -Filter *.html | ForEach-Object { $_.Name } | Sort-Object)
    foreach ($file in Get-ChildItem $folder -Filter *.html) {
        $match = [regex]::Match((Read-Text $file.FullName), '^\s*<!--(?<meta>[\s\S]*?)-->')
        foreach ($key in "title", "description") {
            if (-not $match.Success -or $match.Groups["meta"].Value -notmatch "(?m)^\s*$key\s*:\s*\S") {
                Fail (Relative $file.FullName) "Metadata comment has no '$key'."
            }
        }
    }
    if (-not (Test-Path (Join-Path $website "$lang/index.html"))) { Fail "website/$lang" "index.html is missing." }
}
foreach ($file in $pages.en | Where-Object { $pages.tr -notcontains $_ }) { Fail "website/tr/docs/$file" "Missing (exists in website/en/docs)." }
foreach ($file in $pages.tr | Where-Object { $pages.en -notcontains $_ }) { Fail "website/en/docs/$file" "Missing (exists in website/tr/docs)." }

# --- Kenar çubuğu: build.ps1'deki $docs listesi klasörle aynı ---
Step "Sidebar"
$build = Read-Text (Join-Path $website "build.ps1")
$listed = @([regex]::Matches($build, 'Group\s*=\s*"\w+";\s*File\s*=\s*"([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
foreach ($file in $pages.en | Where-Object { $listed -notcontains $_ }) { Fail "website/build.ps1" "website/en/docs/$file is not in the sidebar list (`$docs)." }
foreach ($file in $listed | Where-Object { $pages.en -notcontains $_ }) { Fail "website/build.ps1" "Sidebar lists $file, which is not in website/en/docs." }
foreach ($file in $listed | Group-Object | Where-Object { $_.Count -gt 1 }) { Fail "website/build.ps1" "Sidebar lists $($file.Name) more than once." }

# --- strings.json: aynı anahtarlar, boş değer yok ---
Step "strings.json"
$strings = (Read-Text (Join-Path $website "strings.json")) | ConvertFrom-Json
$keys = @{}
foreach ($lang in "en", "tr") {
    $keys[$lang] = @($strings.$lang.PSObject.Properties | ForEach-Object { $_.Name })
    foreach ($property in $strings.$lang.PSObject.Properties) {
        if ([string]::IsNullOrWhiteSpace([string]$property.Value)) { Fail "website/strings.json" "$lang.$($property.Name) is empty." }
    }
}
foreach ($key in $keys.en | Where-Object { $keys.tr -notcontains $_ }) { Fail "website/strings.json" "tr has no '$key'." }
foreach ($key in $keys.tr | Where-Object { $keys.en -notcontains $_ }) { Fail "website/strings.json" "en has no '$key'." }

# --- README: aynı başlık yapısı (kod bloklarının içi sayılmaz) ---
Step "README headings"
function Heading-Levels([string]$path) {
    $inCode = $false
    foreach ($line in [IO.File]::ReadAllLines($path, $utf8)) {
        if ($line -match '^\s*(```|~~~)') { $inCode = -not $inCode; continue }
        if (-not $inCode -and $line -match '^(#{1,6})\s') { $Matches[1].Length }
    }
}
$tr = @(Heading-Levels (Join-Path $repo "README.md"))
$en = @(Heading-Levels (Join-Path $repo "README.en.md"))
if (($tr -join ",") -ne ($en -join ",")) {
    $count = [Math]::Min($tr.Count, $en.Count)
    $at = 0
    while ($at -lt $count -and $tr[$at] -eq $en[$at]) { $at++ }
    Fail "README.en.md" "Headings differ from README.md ($($tr.Count) vs $($en.Count) headings; first difference at heading $($at + 1))."
}

# --- Sürüm numaraları ---
Step "Versions"
$props = [xml](Read-Text (Join-Path $repo "Directory.Build.props"))
$version = @($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
$files = @(
    Get-Item (Join-Path $repo "README.md"), (Join-Path $repo "README.en.md"), (Join-Path $website "llms.txt"), (Join-Path $website "strings.json")
    Get-ChildItem (Join-Path $repo "docs/nuget") -Filter *.md
    Get-ChildItem $website -Recurse -Include *.html
)
foreach ($file in $files) {
    foreach ($m in [regex]::Matches((Read-Text $file.FullName), '(?<![\w.])\d+\.\d+\.\d+-(?:preview|alpha|beta|rc)\.\d+(?![\w.])')) {
        if ($m.Value -ne $version) { Fail (Relative $file.FullName) "Mentions $($m.Value); the current version is $version (tools/bump-version.ps1 updates it)." }
    }
}

# --- Derlenmiş sitede iç linkler ---
if ($Site) {
    Step "Internal links ($Site)"
    $root = [IO.Path]::GetFullPath($Site)
    if (-not (Test-Path $root)) { throw "$root does not exist; build the site first (website/build.ps1)." }
    # Yalnızca yer tutucu varsa (website/build.ps1 -Reference olmadan) referansın çapaları denetlenmez.
    $placeholderReference = @(Get-ChildItem (Join-Path $root "reference") -Recurse -File -ErrorAction SilentlyContinue).Count -le 1
    $fold = @{ [char]0x00E7 = "c"; [char]0x011F = "g"; [char]0x0131 = "i"; [char]0x00F6 = "o"; [char]0x015F = "s"; [char]0x00FC = "u" }
    $ids = @{}
    function Ids-Of([string]$path) {
        if (-not $ids.ContainsKey($path)) {
            $html = Read-Text $path
            $set = New-Object System.Collections.Generic.HashSet[string]
            foreach ($m in [regex]::Matches($html, '\s(?:id|name)\s*=\s*"([^"]+)"')) { [void]$set.Add([Net.WebUtility]::HtmlDecode($m.Groups[1].Value)) }
            # site.js, id'si olmayan h2/h3 başlıklarına metinlerinden id verir; aynı kural burada uygulanır.
            $culture = if ($html -match '<html[^>]*\slang="tr"') { "tr-TR" } else { "en-US" }
            foreach ($m in [regex]::Matches($html, '<h[23](?<attrs>[^>]*)>(?<text>[\s\S]*?)</h[23]>')) {
                if ($m.Groups["attrs"].Value -match '\sid\s*=') { continue }
                $text = [Net.WebUtility]::HtmlDecode(($m.Groups["text"].Value -replace '<[^>]+>', ''))
                $text = $text.ToLower([Globalization.CultureInfo]::GetCultureInfo($culture))
                $text = -join ($text.ToCharArray() | ForEach-Object { if ($fold.ContainsKey($_)) { $fold[$_] } else { $_ } })
                $base = ($text -creplace '[^a-z0-9]+', '-').Trim('-')
                if (-not $base) { $base = "section" }
                $id = $base; $n = 2
                while ($set.Contains($id)) { $id = "$base-$n"; $n++ }
                [void]$set.Add($id)
            }
            $ids[$path] = $set
        }
        $ids[$path]
    }
    $checked = 0
    foreach ($page in Get-ChildItem $root -Recurse -Filter *.html) {
        $html = Read-Text $page.FullName
        $name = $page.FullName.Substring($root.Length + 1).Replace("\", "/")
        foreach ($m in [regex]::Matches($html, '\s(?:href|src)\s*=\s*"([^"]*)"')) {
            $link = [Net.WebUtility]::HtmlDecode($m.Groups[1].Value)
            if ($link -match '^(?:[a-z][a-z0-9+.-]*:|//)' -or $link -eq "") { continue }
            $checked++
            $hash = $link.IndexOf("#")
            $fragment = if ($hash -ge 0) { [Uri]::UnescapeDataString($link.Substring($hash + 1)) } else { "" }
            $path = if ($hash -ge 0) { $link.Substring(0, $hash) } else { $link }
            $path = $path.Split("?")[0]
            $target = if ($path -eq "") { $page.FullName } else { [IO.Path]::GetFullPath((Join-Path $page.DirectoryName ([Uri]::UnescapeDataString($path)))) }
            if (Test-Path $target -PathType Container) { $target = Join-Path $target "index.html" }
            if (-not $target.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path $target -PathType Leaf)) {
                Fail "website" "$name links to '$link', which does not exist."
            }
            elseif ($placeholderReference -and $target.StartsWith((Join-Path $root "reference"), [StringComparison]::OrdinalIgnoreCase)) { continue }
            elseif ($fragment -and $target.EndsWith(".html") -and -not (Ids-Of $target).Contains($fragment)) {
                Fail "website" "$name links to '$link'; the page has no id '$fragment'."
            }
        }
    }
    Write-Host "  $checked links"
}

if ($errors.Count -gt 0) {
    Write-Host "$($errors.Count) problem(s)." -ForegroundColor Red
    exit 1
}
Write-Host "Docs OK (version $version)." -ForegroundColor Green
