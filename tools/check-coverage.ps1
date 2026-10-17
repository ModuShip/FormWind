<#
.SYNOPSIS
    Test sonuçlarındaki Cobertura kapsam raporlarından FormWind'in satır ve dal kapsamını yazar; satır kapsamı eşiğin
    altındaysa hata döner.

.EXAMPLE
    dotnet test tests/FormWind.Tests -c Release -f net472 --settings tests/coverage.runsettings --collect "Code Coverage" --results-directory TestResults/net472
    .\tools\check-coverage.ps1 -Results TestResults -Minimum 0.80
#>
param(
    [string]$Results = "TestResults",
    [double]$Minimum = 0.80,
    [string]$Package = "FormWind"
)

$files = Get-ChildItem $Results -Recurse -Filter *.cobertura.xml -ErrorAction SilentlyContinue
if (-not $files) {
    Write-Output "::error::Kapsam raporu bulunamadı ($Results)."
    exit 1
}

$root = (Resolve-Path $Results).Path
$invariant = [Globalization.CultureInfo]::InvariantCulture
$rows = @("| Rapor | $Package satır | $Package dal |", "| --- | --- | --- |")
$failed = $false
foreach ($file in $files) {
    [xml]$xml = Get-Content $file.FullName
    # PowerShell değişken adları büyük/küçük harf ayırmaz; $Package parametresiyle karışmasın diye $node.
    $node = $xml.coverage.packages.package | Where-Object { $_.name -eq $Package } | Select-Object -First 1
    if (-not $node) { continue }

    $line = [double]::Parse($node.'line-rate', $invariant)
    $branch = [double]::Parse($node.'branch-rate', $invariant)
    # Rapor klasörü hedef çerçeveye göre adlandırılır (TestResults/net472/...).
    $name = ($file.FullName.Substring($root.Length).TrimStart([char[]]'\/') -split '[\\/]')[0]
    $rows += "| $name | {0:P1} | {1:P1} |" -f $line, $branch
    if ($line -lt $Minimum) {
        Write-Output ("::error::{0} satır kapsamı {1:P1}, eşik {2:P0} ({3})" -f $Package, $line, $Minimum, $name)
        $failed = $true
    }
}

$rows | Write-Output
if ($env:GITHUB_STEP_SUMMARY) { $rows | Add-Content $env:GITHUB_STEP_SUMMARY }
if ($failed) { exit 1 }
