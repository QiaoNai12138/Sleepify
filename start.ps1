# Sleepify - device check and sleep plan generator
# ASCII only on purpose: PowerShell 5.1 mis-parses non-ASCII in BOM-less files.
$ErrorActionPreference = 'SilentlyContinue'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$pd   = Join-Path $here 'config'
New-Item -ItemType Directory -Force -Path $pd | Out-Null
$logDir = Join-Path $here 'logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logFile  = Join-Path $logDir 'setup-log.txt'
$planFile = Join-Path $logDir 'sleep-plan.txt'

if (Test-Path $logFile) { Remove-Item $logFile -Force -ErrorAction SilentlyContinue }

function Say([string]$m) {
    Write-Host $m
    Add-Content -Path $logFile -Value $m -Encoding UTF8 -ErrorAction SilentlyContinue
}

Say '============================================================'
Say ' Sleepify - device check and sleep plan'
Say (' Time : ' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))
Say '============================================================'
Say ''

$os  = Get-CimInstance Win32_OperatingSystem
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$cs  = Get-CimInstance Win32_ComputerSystem

Say '[ Hardware ]'
Say ('  OS        : ' + $os.Caption + '  (build ' + $os.BuildNumber + ')')
Say ('  Model     : ' + $cs.Manufacturer.Trim() + ' ' + $cs.Model.Trim())
Say ('  CPU       : ' + $cpu.Name.Trim())
Say ('  Cores     : ' + $cpu.NumberOfCores + 'C / ' + $cpu.NumberOfLogicalProcessors + 'T   max ' + $cpu.MaxClockSpeed + ' MHz')
Say ('  RAM       : ' + [math]::Round($os.TotalVisibleMemorySize / 1MB, 1) + ' GB')

$disk = Get-PhysicalDisk | Select-Object -First 1
if ($disk) {
    Say ('  Disk      : ' + $disk.MediaType + '  ' + $disk.FriendlyName + '  ' + [math]::Round($disk.Size / 1GB, 0) + ' GB')
}

$gpus = @(Get-CimInstance Win32_VideoController)
foreach ($g in $gpus) {
    Say ('  GPU       : ' + $g.Name + '  driver ' + $g.DriverVersion)
}

$bat = Get-CimInstance Win32_Battery | Select-Object -First 1
$isLaptop = $false
if ($bat) {
    $isLaptop = $true
    Say ('  Battery   : present   design ' + [math]::Round($bat.DesignCapacity / 1000, 1) + ' Wh')
} else {
    Say '  Battery   : none (desktop?)'
}

$wifi = @(Get-NetAdapter -Physical | Where-Object { $_.PhysicalMediaType -like '*802.11*' })
if ($wifi.Count -gt 0) {
    Say ('  WLAN      : ' + (($wifi | ForEach-Object { $_.Name }) -join ', '))
} else {
    Say '  WLAN      : none'
}

$bt = @(Get-PnpDevice -Class Bluetooth | Where-Object { $_.InstanceId -notmatch '^BTH\\MS_' -and $_.InstanceId -notmatch '^SWD\\' -and $_.Status -eq 'OK' })
Say ('  Bluetooth : ' + $bt.Count + ' radio(s)')

# ---- hard platform block -------------------------------------------------
# AMD Ryzen 5000/6000/7000 mobile H-series (H/HS/HX) degrade over time. Once
# degraded the SoC - the memory controller in particular - is unstable at LOW
# load and LOW voltage, which is exactly the regime this tool holds for hours.
# Abort before touching anything.
$cpuUpper = $cpu.Name.ToUpperInvariant()
$forbidden = $false
if ($cpuUpper -match 'RYZEN') {
    foreach ($t in ($cpuUpper -split '\s+')) {
        if ($t.Length -lt 5) { continue }
        $c0 = $t[0]
        if ($c0 -lt [char]'5' -or $c0 -gt [char]'7') { continue }
        $isDigits = $true
        for ($d = 1; $d -lt 4; $d++) {
            if ($t[$d] -lt [char]'0' -or $t[$d] -gt [char]'9') { $isDigits = $false; break }
        }
        if (-not $isDigits) { continue }
        $rest = $t.Substring(4)
        if ($rest -eq 'H' -or $rest -eq 'HS' -or $rest -eq 'HX') { $forbidden = $true; break }
    }
}

if ($forbidden) {
    Say ''
    Say '############################################################'
    Say '  WARNING - RISKY PLATFORM DETECTED'
    Say ''
    Say '  This tool forces a very low CPU load for hours. On AMD Ryzen'
    Say '  5000 / 6000 / 7000 mobile H-series (H / HS / HX) that is'
    Say '  exactly the regime where a degraded SoC - the memory'
    Say '  controller in particular - becomes unstable or crashes.'
    Say ''
    Say ('  Detected: ' + $cpu.Name.Trim())
    Say '  Nothing has been changed yet.'
    Say '############################################################'
    Say ''

    Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
    $i18nPath = Join-Path $here 'i18n.ps1'
    if (Test-Path $i18nPath) { . $i18nPath }
    $title = 'Sleepify'
    $msg = 'This tool forces a very low CPU load for hours.' + "`r`n`r`n" + 'Continue anyway?'
    if (Get-Command Get-SleepifyString -ErrorAction SilentlyContinue) {
        $title = Get-SleepifyString 'startTitle'
        $msg = (Get-SleepifyString 'startBody').Replace('{CPU}', $cpu.Name.Trim())
    }
    $answer = [System.Windows.Forms.MessageBox]::Show($msg, $title, 'YesNo', 'Warning')
    if ($answer -ne 'Yes') {
        Say '  User declined the risk. Aborted - nothing was changed.'
        Start-Sleep -Seconds 4
        exit 1
    }
    Say '  User accepted the risk. Continuing.'
    Say ''
}

Say ''
Say '[ Sleep support ]'
$sleepOut = (& powercfg /a) -join "`n"
$canS3  = $sleepOut -match 'S3|Standby \(S3\)'
$canHib = $sleepOut -notmatch 'Hibernation has not been enabled|not available'
Say ('  S3 sleep  : ' + $(if ($canS3) { 'reported available' } else { 'not reported' }))
Say ('  Hibernate : ' + $(if ($canHib) { 'reported available' } else { 'not available' }))
Say '  NOTE: this tool is for machines where those paths HANG. Availability != reliability.'

Say ''
Say '[ Building sleep plan ]'

# --- heuristic: cap scales with the number of logical CPUs ---
$lpc = [int]$cpu.NumberOfLogicalProcessors
if ($lpc -le 4)      { $cap = 30 }
elseif ($lpc -le 8)  { $cap = 50 }
else                 { $cap = 60 }

$keepWlan  = 1
$freezeAll = 1

Say ('  Logical CPUs  : ' + $lpc)
Say ('  CPU cap       : ' + $cap + '%  (during lid-close)')
Say ('  Keep WLAN     : ' + $(if ($keepWlan -eq 1) { 'yes (only Bluetooth is switched off)' } else { 'no' }))
Say ('  Freeze all    : ' + $(if ($freezeAll -eq 1) { 'yes (whitelist respected)' } else { 'no' }))

$cfg = Join-Path $pd 'config.ini'
$lines = @(
    'cpucap=' + $cap,
    'keepwlan=' + $keepWlan,
    'freezeall=' + $freezeAll
)
[IO.File]::WriteAllLines($cfg, $lines)
Say ('  Written       : ' + $cfg)

Say ''
Say '[ Writing device profile ]'
# Device names differ between models, so they are detected once here and reused
# by Sleepify.exe instead of being probed on every lid event.
$brightInst = ''
try {
    $bm = Get-CimInstance -Namespace root\wmi -ClassName WmiMonitorBrightness | Select-Object -First 1
    if ($bm) { $brightInst = $bm.InstanceName }
} catch { }

$devLines = @()
$devLines += 'model=' + (($cs.Manufacturer.Trim() + ' ' + $cs.Model.Trim()) -replace '[^\x20-\x7E]', '?')
$devLines += 'cpu=' + ($cpu.Name.Trim() -replace '[^\x20-\x7E]', '?')
$devLines += 'os=' + (($os.Caption + ' build ' + $os.BuildNumber) -replace '[^\x20-\x7E]', '?')
$devLines += 'wlan=' + ((($wifi | ForEach-Object { $_.Name }) -join '|') -replace '[^\x20-\x7E]', '?')
$devLines += 'bluetooth=' + (((($bt | ForEach-Object { $_.InstanceId }) -join '|')) -replace '[^\x20-\x7E]', '?')
$devLines += 'brightness=' + ($brightInst -replace '[^\x20-\x7E]', '?')
$devLines += 'lid=' + $(if ($isLaptop) { 'yes' } else { 'no' })
$devLines += 'generated=' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
$devFile = Join-Path $pd 'device.ini'
[IO.File]::WriteAllText($devFile, ($devLines -join "`r`n"), (New-Object Text.ASCIIEncoding))
Say ('  Written       : ' + $devFile)
Say ('  WLAN          : ' + (($wifi | ForEach-Object { $_.Name }) -join ', '))
Say ('  Bluetooth     : ' + $bt.Count + ' radio(s)')
Say ('  Brightness    : ' + $(if ($brightInst) { 'WMI supported' } else { 'not supported' }))
Say ''

Say '[ Lid-close action -> Do nothing ]'
$subLid = '4f971e89-eebd-4455-a8de-9e59040e7347'
$setLid = '5ca83367-6e45-459f-a27b-476b1d01c936'
& powercfg /setacvalueindex SCHEME_CURRENT $subLid $setLid 0
& powercfg /setdcvalueindex SCHEME_CURRENT $subLid $setLid 0
& powercfg /setactive SCHEME_CURRENT
$q = (& powercfg /q SCHEME_CURRENT $subLid $setLid) -join "`n"
Say '  Applied for AC and DC.'
Say ''

$plan = @()
$plan += 'Sleepify sleep plan'
$plan += ('Generated : ' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))
$plan += ('Machine   : ' + $cs.Manufacturer.Trim() + ' ' + $cs.Model.Trim())
$plan += ('CPU       : ' + $cpu.Name.Trim() + '  (' + $lpc + ' threads)')
$plan += ('OS        : ' + $os.Caption + ' build ' + $os.BuildNumber)
$plan += ''
$plan += 'Settings applied:'
$plan += ('  cpucap    = ' + $cap + '   (max CPU state while the lid is closed)')
$plan += ('  keepwlan  = ' + $keepWlan + '   (1 = keep Wi-Fi radio on)')
$plan += ('  freezeall = ' + $freezeAll + '   (1 = freeze every freezable process, whitelist respected)')
$plan += '  lidaction = do nothing (AC and DC)'
$plan += ''
$plan += 'Config file: ' + $cfg
[IO.File]::WriteAllLines($planFile, $plan)
Say ('Plan saved to: ' + $planFile)

Say ''
Say '[ Starting Sleepify ]'
$exe = Join-Path $here 'Sleepify.exe'
if (Test-Path $exe) {
    # A fresh install has no state flag, so the app would come up DISABLED while the
    # lid action is already "do nothing" - the lid would appear to do nothing at all.
    # Enable it here so a freshly deployed machine behaves exactly like a configured one.
    $flag = Join-Path $pd 'enabled.flag'
    if (-not (Test-Path $flag)) {
        [IO.File]::WriteAllText($flag, '1')
        Say '  Service enabled (enabled.flag created).'
    } else {
        Say '  Service flag already present.'
    }
    Start-Process -FilePath $exe -WorkingDirectory $here
    Say '  Sleepify.exe started.'
} else {
    Say '  Sleepify.exe not found next to this script - skipped.'
}

Say ''
Say '============================================================'
Say ' Done. Closing in 8 seconds...'
Say '============================================================'
Start-Sleep -Seconds 8
