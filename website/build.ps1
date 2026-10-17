<#
.SYNOPSIS
    Builds the FormWind website (landing page, documentation and class reference) into a static folder.

.DESCRIPTION
    The site is bilingual: website/en and website/tr hold the same pages (English is written to the site root, Turkish to
    tr/). Documentation pages hold only their content and a metadata comment (title, description); this script wraps them
    in website/partials/docs.html, writes the sidebar, previous/next links and the shared header and footer, fills the
    {{t:key}} texts from website/strings.json, and copies the assets and the screenshots from docs/. With -Reference it also runs tools/FormWind.Docs, which renders the class
    reference with real FormWind controls (Windows and the .NET SDK required), into reference/.

.EXAMPLE
    .\website\build.ps1
.EXAMPLE
    .\website\build.ps1 -Reference -Output artifacts/website
#>
param(
    [string]$Output,
    [switch]$Reference
)

$ErrorActionPreference = "Stop"
$site = $PSScriptRoot
$repo = Split-Path $site -Parent
if (-not $Output) { $Output = Join-Path $repo "artifacts/website" }
$Output = [IO.Path]::GetFullPath($Output)
$utf8 = New-Object System.Text.UTF8Encoding($false)

# Languages: sources in website/<lang>/, output under <prefix>. English is served at the root, Turkish under tr/.
# Every page exists in both languages with the same file name, so the header links each page to its translation.
$languages = @(
    @{ Code = "en"; Prefix = "" }
    @{ Code = "tr"; Prefix = "tr/" }
)

# Sidebar order. Group and external titles are keys in strings.json.
$docs = @(
    @{ Group = "groupStart"; File = "index.html" }
    @{ Group = "groupStart"; File = "presets.html" }
    @{ Group = "groupStart"; File = "theme.html" }
    @{ Group = "groupStyling"; File = "classes.html" }
    @{ Group = "groupStyling"; File = "layout.html" }
    @{ Group = "groupControls"; File = "controls.html" }
    @{ Group = "groupControls"; File = "datagridview.html" }
    @{ Group = "groupControls"; File = "charts.html" }
    @{ Group = "groupControls"; File = "forms.html" }
    @{ Group = "groupIntegration"; File = "krypton.html" }
    @{ Group = "groupIntegration"; File = "designer.html" }
    @{ Group = "groupIntegration"; File = "dpi.html" }
    @{ Group = "groupIntegration"; File = "accessibility.html" }
    @{ Group = "groupTools"; File = "tools.html" }
    @{ Group = "groupTools"; File = "performance.html" }
    @{ Group = "groupTools"; File = "stability.html" }
)
$external = @(
    @{ Group = "groupReference"; Title = "extReference"; Href = "{{root}}reference/" }
    @{ Group = "groupReference"; Title = "extChangelog"; Href = "https://github.com/ModuShip/FormWind/blob/main/CHANGELOG.md" }
    @{ Group = "groupReference"; Title = "extArchitecture"; Href = "https://github.com/ModuShip/FormWind/blob/main/ARCHITECTURE.md" }
)

function Read-Text([string]$path) { [IO.File]::ReadAllText($path, $utf8) }
function Write-Text([string]$path, [string]$text) {
    New-Item -ItemType Directory -Force (Split-Path $path -Parent) | Out-Null
    [IO.File]::WriteAllText($path, $text, $utf8)
}
function Encode([string]$text) { [Net.WebUtility]::HtmlEncode($text) }

function Read-Page([string]$path) {
    $text = Read-Text $path
    $match = [regex]::Match($text, '^\s*<!--(?<meta>[\s\S]*?)-->')
    if (-not $match.Success) { throw "$path has no metadata comment." }
    $meta = @{}
    foreach ($line in $match.Groups["meta"].Value -split "`n") {
        if ($line -match '^\s*(\w+)\s*:\s*(.+?)\s*$') { $meta[$Matches[1]] = $Matches[2] }
    }
    if (-not $meta.title) { throw "$path has no title." }
    @{ Title = $meta.title; Description = $meta.description; Body = $text.Substring($match.Length).Trim() }
}

function Expand([string]$template, [hashtable]$values) {
    foreach ($key in $values.Keys) { $template = $template.Replace("{{$key}}", [string]$values[$key]) }
    $template
}

# Replaces {{t:key}} with the language's string; an unknown key fails the build.
function Translate([string]$template, $strings) {
    [regex]::Replace($template, '\{\{t:(\w+)\}\}', {
        param($m)
        $value = $strings.($m.Groups[1].Value)
        if ($null -eq $value) { throw "strings.json has no '$($m.Groups[1].Value)'." }
        Encode $value
    })
}

# Fills the placeholders of a page at <prefix><path>: root is the way back to the site root, base to the language's root.
function Finish([string]$html, $lang, [string]$path, $strings, [string]$alt) {
    $depth = ($lang.Prefix + $path).Split("/").Count - 1
    $root = "../" * $depth
    $other = $languages | Where-Object { $_.Code -ne $lang.Code } | Select-Object -First 1
    $html = Translate $html $strings
    if (-not $alt) { $alt = "$root$($other.Prefix)$path" }
    Expand $html @{ alt = $alt; base = "$root$($lang.Prefix)"; root = $root }
}

if (Test-Path $Output) { Remove-Item $Output -Recurse -Force }
foreach ($dir in "assets", "img") { New-Item -ItemType Directory -Force (Join-Path $Output $dir) | Out-Null }

Copy-Item (Join-Path $site "assets/*") (Join-Path $Output "assets") -Recurse
Copy-Item (Join-Path $repo "docs/*.png") (Join-Path $Output "img")
Write-Text (Join-Path $Output ".nojekyll") ""

$strings = (Read-Text (Join-Path $site "strings.json")) | ConvertFrom-Json
$header = Read-Text (Join-Path $site "partials/header.html")
$footer = Read-Text (Join-Path $site "partials/footer.html")
$layout = Read-Text (Join-Path $site "partials/docs.html")

foreach ($lang in $languages) {
    $t = $strings.($lang.Code)
    $source = Join-Path $site $lang.Code
    $pages = @{}
    foreach ($doc in $docs) { $pages[$doc.File] = Read-Page (Join-Path $source "docs/$($doc.File)") }

    # Sidebar: one list per group; the current page is marked.
    $navFor = {
        param([string]$current)
        $html = New-Object System.Text.StringBuilder
        $groups = @($docs + $external | ForEach-Object { $_.Group } | Select-Object -Unique)
        foreach ($group in $groups) {
            [void]$html.Append("<p class=""nav-group"">{{t:$group}}</p><ul>")
            foreach ($doc in $docs | Where-Object { $_.Group -eq $group }) {
                $attr = if ($doc.File -eq $current) { ' aria-current="page"' } else { "" }
                [void]$html.Append("<li><a href=""$($doc.File)""$attr>$(Encode $pages[$doc.File].Title)</a></li>")
            }
            foreach ($link in $external | Where-Object { $_.Group -eq $group }) {
                $rel = if ($link.Href -like "http*") { ' rel="noopener" target="_blank"' } else { "" }
                [void]$html.Append("<li><a href=""$($link.Href)""$rel>{{t:$($link.Title)}}</a></li>")
            }
            [void]$html.Append("</ul>")
        }
        $html.ToString()
    }

    for ($i = 0; $i -lt $docs.Count; $i++) {
        $doc = $docs[$i]
        $page = $pages[$doc.File]
        $pager = '<nav class="pager" aria-label="{{t:pager}}">'
        if ($i -gt 0) {
            $prev = $docs[$i - 1].File
            $pager += "<a class=""prev"" href=""$prev""><span>{{t:prev}}</span>$(Encode $pages[$prev].Title)</a>"
        }
        if ($i -lt $docs.Count - 1) {
            $next = $docs[$i + 1].File
            $pager += "<a class=""next"" href=""$next""><span>{{t:next}}</span>$(Encode $pages[$next].Title)</a>"
        }
        $pager += "</nav>"

        $html = Expand $layout @{ header = $header; footer = $footer; nav = (& $navFor $doc.File) }
        $html = Expand $html @{
            title = Encode $page.Title
            description = Encode $page.Description
            content = $page.Body
            pager = $pager
            edit = "https://github.com/ModuShip/FormWind/edit/main/website/$($lang.Code)/docs/$($doc.File)"
            section = "docs"
        }
        $path = "docs/$($doc.File)"
        Write-Text (Join-Path $Output "$($lang.Prefix)$path") (Finish $html $lang $path $t)
    }

    $landing = Expand (Read-Text (Join-Path $source "index.html")) @{ header = $header; footer = $footer; section = "home" }
    Write-Text (Join-Path $Output "$($lang.Prefix)index.html") (Finish $landing $lang "index.html" $t)
}

if ($Reference) {
    dotnet run --project (Join-Path $repo "tools/FormWind.Docs") -c Release -- (Join-Path $Output "reference")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
else {
    # Without the generator the reference link still resolves to a page that explains how to build it.
    $placeholder = Expand (Read-Text (Join-Path $site "reference-placeholder.html")) @{ header = $header; footer = $footer; section = "docs" }
    Write-Text (Join-Path $Output "reference/index.html") (Finish $placeholder $languages[0] "reference/index.html" $strings.en "../tr/index.html")
}

Write-Output "Website: $Output"
