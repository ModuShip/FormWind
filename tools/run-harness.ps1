<#
.SYNOPSIS
    Bir .NET (Core) WinForms uygulamasını FormWind harness'iyle, kaynak kodunu değiştirmeden çalıştırır.

.DESCRIPTION
    tools\FormWind.Harness'i derler ve uygulamayı DOTNET_STARTUP_HOOKS ile başlatır. Açılan her forma kontrol tipine göre
    FormWind class'ları uygulanır; stil süreleri, donmalar, çizim fırtınaları, bilinmeyen class'lar, exception'lar ve
    kontrast sorunları kaydedilir. Bittiğinde log özetini yazar.

    .NET Framework uygulamalarında startup hook yoktur; onlarda harness'in uygulamanın koduna eklenmesi gerekir.

.EXAMPLE
    .\tools\run-harness.ps1 -Exe C:\apps\MyApp.exe -SelfTest -ExitAfter 60
.EXAMPLE
    .\tools\run-harness.ps1 -Exe C:\apps\MyApp.exe -Arguments '--file','a.txt' -Dark -Skip DataGridView
.EXAMPLE
    .\tools\run-harness.ps1 -Exe C:\apps\MyApp.exe -SelfTest -Dpi 192   # formları %200'e taşınmış gibi ölçekler (PerMonitorV2)
.EXAMPLE
    .\tools\run-harness.ps1 -Exe C:\apps\MyApp.exe -Dump -ExitAfter 20   # stil uygulamadan değerleri yazar
#>
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string[]]$Arguments = @(),
    [switch]$Dark,
    [switch]$SelfTest,
    [switch]$Dump,
    [int]$ExitAfter = 0,
    [string]$Skip = "",
    [string]$Dpi = "",
    [string]$WorkingDirectory = ""
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'FormWind.Harness\FormWind.Harness.csproj'
$output = Join-Path $PSScriptRoot 'FormWind.Harness\bin\Release\net8.0-windows'

dotnet build $project -c Release -v quiet --nologo | Out-Null
if ($LASTEXITCODE -ne 0) { throw "FormWind.Harness derlenemedi." }
$hook = Join-Path $output 'FormWind.Harness.dll'

$environment = @{
    DOTNET_STARTUP_HOOKS = $hook
    FORMWIND_DARK        = $(if ($Dark) { '1' } else { $null })
    FORMWIND_SELFTEST    = $(if ($SelfTest) { '1' } else { $null })
    FORMWIND_DUMP        = $(if ($Dump) { '1' } else { $null })
    FORMWIND_EXIT_AFTER  = $(if ($ExitAfter -gt 0) { "$ExitAfter" } else { $null })
    FORMWIND_SKIP        = $(if ($Skip) { $Skip } else { $null })
    FORMWIND_DPI         = $(if ($Dpi) { $Dpi } else { $null })
}
$saved = @{}
foreach ($key in $environment.Keys) {
    $saved[$key] = [Environment]::GetEnvironmentVariable($key)
    [Environment]::SetEnvironmentVariable($key, $environment[$key])
}

$started = Get-Date
try {
    $start = @{ FilePath = $Exe; PassThru = $true }
    if ($Arguments.Count -gt 0) { $start.ArgumentList = $Arguments }
    if ($WorkingDirectory) { $start.WorkingDirectory = $WorkingDirectory }
    $process = Start-Process @start
}
finally {
    foreach ($key in $saved.Keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key]) }
}

Write-Host "Başlatıldı: $Exe (PID $($process.Id)). Çıkış bekleniyor..."
$process.WaitForExit()

$root = Join-Path ([IO.Path]::GetTempPath()) 'FormWind-Harness'
$run = Get-ChildItem $root -Directory -ErrorAction SilentlyContinue | Where-Object { $_.CreationTime -ge $started } |
    Sort-Object CreationTime | Select-Object -Last 1
if (-not $run) { Write-Warning "Harness log'u yok: hook yüklenmedi ya da hiç form açılmadı."; return }

$lines = Get-Content (Join-Path $run.FullName 'log.txt')
Write-Host "`nLog: $($run.FullName)"
$lines | Where-Object { $_ -match ' (STARTED|FORM|EXCEPTION|UNKNOWN|OWNERDRAW|SCREEN|TIMER STARVATION|UI BLOCKED|EXIT) ' }
$count = { param($pattern) ($lines | Where-Object { $_ -cmatch $pattern }).Count }
Write-Host ("`nÖzet: {0} form, {1} exception, {2} bilinmeyen class, {3} kontrast sorunu ({7} görüntüden), {4} kendi çizen kontrol, {5} donma (>250 ms), {6} çizim fırtınası, {8} zamanlayıcı açlığı" -f `
    (& $count ' FORM '), (& $count ' EXCEPTION '), (& $count ' UNKNOWN '), (& $count ' CONTRAST '), (& $count ' OWNERDRAW '), (& $count ' UI STALL '), (& $count ' PAINT STORM '), (& $count ' SCREEN '), (& $count ' TIMER STARVATION \d'))
