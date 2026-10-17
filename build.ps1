<#
.SYNOPSIS
    Builds, tests and packs FormWind the same way CI does (.github/workflows/ci.yml).

.DESCRIPTION
    Builds the solution, runs the tests on every target framework (with code coverage on net472 and net8.0-windows),
    runs the process-isolated categories (ColorMode, UIAutomation), checks the coverage threshold and packs the
    packages into artifacts/packages. Test results go to TestResults/.

.EXAMPLE
    .\build.ps1

.EXAMPLE
    .\build.ps1 -Quick
    Builds and tests on net8.0-windows only, without coverage or packing (fast check before a commit).
#>
param(
    [string]$Configuration = "Release",
    [switch]$Quick,
    [switch]$SkipPack,
    [double]$MinimumCoverage = 0.80
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$env:DOTNET_NOLOGO = "true"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "true"

function Invoke-Step([string]$Name, [scriptblock]$Command) {
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Name failed (exit code $LASTEXITCODE)." }
}

$tests = "tests/FormWind.Tests"

if ($Quick) {
    Invoke-Step "Build" { dotnet build FormWind.sln -c $Configuration }
    Invoke-Step "Test (net8.0-windows)" { dotnet test $tests -c $Configuration --no-build -f net8.0-windows }
    return
}

if (Test-Path TestResults) { Remove-Item TestResults -Recurse -Force }

Invoke-Step "Build" { dotnet build FormWind.sln -c $Configuration }

foreach ($framework in "net472", "net8.0-windows") {
    Invoke-Step "Test ($framework, coverage)" {
        dotnet test $tests -c $Configuration --no-build -f $framework --logger trx --settings tests/coverage.runsettings `
            --collect "Code Coverage" --results-directory "TestResults/$framework"
    }
}
foreach ($framework in "net9.0-windows", "net10.0-windows") {
    Invoke-Step "Test ($framework)" {
        dotnet test $tests -c $Configuration --no-build -f $framework --logger trx --results-directory "TestResults/$framework"
    }
}

# Application.SetColorMode ve UI Automation süreç geneline etki eder; ayrı süreçte çalışırlar.
$env:FORMWIND_COLORMODE = "1"
try { Invoke-Step "Test (Application.SetColorMode)" { dotnet test $tests -c $Configuration --no-build -f net10.0-windows --filter Category=ColorMode } }
finally { Remove-Item Env:FORMWIND_COLORMODE }

$env:FORMWIND_UIA = "1"
try { Invoke-Step "Test (UI Automation)" { dotnet test $tests -c $Configuration --no-build -f net472 --filter Category=UIAutomation } }
finally { Remove-Item Env:FORMWIND_UIA }

# Krypton sürüm matrisi: en düşük desteklenen sürümle derlenen FormWind.Krypton en yeni Krypton ile de denenir.
Invoke-Step "Build (Krypton 105)" { dotnet build $tests -c $Configuration -p:KryptonTestVersion=105.26.7.201 }
foreach ($framework in "net472", "net8.0-windows") {
    Invoke-Step "Test (Krypton 105, $framework)" { dotnet test $tests -c $Configuration --no-build -f $framework --filter "FullyQualifiedName~KryptonTests" }
}

Invoke-Step "Coverage" { & "$PSScriptRoot/tools/check-coverage.ps1" -Results TestResults -Minimum $MinimumCoverage }

if (-not $SkipPack) {
    Invoke-Step "Pack" { dotnet pack FormWind.sln -c $Configuration --no-build }
    Write-Host "Packages: $(Join-Path $PSScriptRoot 'artifacts/packages')"
}
