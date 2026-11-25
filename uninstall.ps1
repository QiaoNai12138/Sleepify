# Sleepify - uninstall / full revert
# ASCII only on purpose.
$ErrorActionPreference = 'SilentlyContinue'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$logDir = Join-Path $here 'logs'
New-Item -ItemType Directory -Force -Path $logDir -ErrorAction SilentlyContinue | Out-Null
$logFile = Join-Path $logDir 'uninstall-log.txt'
if (Test-Path $logFile) { Remove-Item $logFile -Force -ErrorAction SilentlyContinue }

function Say([string]$m) {
    Write-Host $m
    Add-Content -Path $logFile -Value $m -Encoding UTF8 -ErrorAction SilentlyContinue
}

Say '============================================================'
Say ' Sleepify - uninstall and revert'
Say (' Time : ' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))
Say '============================================================'
Say ''

# ---- 1. stop the app ----
Say '[1/8] Stopping Sleepify ...'
Get-Process Sleepify -ErrorAction SilentlyContinue | ForEach-Object {
    Say ('      killing pid ' + $_.Id)
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Milliseconds 800

# ---- 2. remove the autostart task ----
Say '[2/8] Removing scheduled task "Sleepify" ...'
$out = (& schtasks /delete /tn "Sleepify" /f 2>&1) -join ' '
Say ('      ' + $out)

# ---- 3. lid action back to Sleep ----
Say '[3/8] Restoring lid-close action to "Sleep" (AC and DC) ...'
$subLid = '4f971e89-eebd-4455-a8de-9e59040e7347'
$setLid = '5ca83367-6e45-459f-a27b-476b1d01c936'
& powercfg /setacvalueindex SCHEME_CURRENT $subLid $setLid 1
& powercfg /setdcvalueindex SCHEME_CURRENT $subLid $setLid 1

# ---- 4. CPU max + cooling policy back to Windows defaults ----
Say '[4/8] Restoring CPU max (100%) and cooling policy (active) ...'
& powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 100
& powercfg /setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 100
& powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR SYSCOOLPOL 1
& powercfg /setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR SYSCOOLPOL 1
& powercfg /setactive SCHEME_CURRENT

# ---- 4b. restore the suspend entries recorded by start.ps1 ----
Say '[4b/8] Restoring power entries recorded by start.ps1 ...'
$orgFile = Join-Path $here 'config\power.orig'
if (Test-Path $orgFile) {
    $orig = @{}
    Get-Content $orgFile | ForEach-Object {
        $i = $_.IndexOf('=')
        if ($i -gt 0) { $orig[$_.Substring(0, $i).Trim()] = $_.Substring($i + 1).Trim() }
    }
    $SUB_SLEEP = '238C9FA8-0AAD-41ED-83F4-97BE242C8F20'
    $SUB_BTN   = '4F971E89-EEBD-4455-A8DE-9E59040E7347'
    $PWR_SETS = @(
        @('STANDBYIDLE',   $SUB_SLEEP, '29F6C1DB-86DA-48C5-9FDB-F2B67B1F44DA'),
        @('HIBERNATEIDLE', $SUB_SLEEP, '9D7815A6-7EE4-497E-8888-515A05F02364'),
        @('UNATTENDSLEEP', $SUB_SLEEP, '7bc4a2f9-d8fc-4469-b07b-33eb785aaca0'),
        @('SBUTTONACTION', $SUB_BTN,   '97D6F4A0-C1D8-4E1E-8C2D-5CB8B0B4C0D8'),
        @('PBUTTONACTION', $SUB_BTN,   '7648EFA3-DD9C-4E3E-B566-50F929386280')
    )
    $root = 'HKLM:\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes'
    $scheme = (Get-ItemProperty $root -Name ActivePowerScheme -EA SilentlyContinue).ActivePowerScheme
    foreach ($s in $PWR_SETS) {
        foreach ($kind in @('AC', 'DC')) {
            $key = if ($kind -eq 'AC') { $s[0] } else { $s[0] + '_DC' }
            $val = $orig[$key]
            if (-not $val) { continue }
            if ($val -eq 'unset') {
                $prop = $kind + 'SettingIndex'
                Remove-ItemProperty -Path "$root\$scheme\$($s[1])\$($s[2])" -Name $prop -Force -ErrorAction SilentlyContinue
                Say ('      ' + $s[0] + ' ' + $kind + ' -> unset')
            } else {
                $sw = '/set' + $kind.ToLower() + 'valueindex'
                & powercfg $sw SCHEME_CURRENT $s[1] $s[2] $val | Out-Null
                Say ('      ' + $s[0] + ' ' + $kind + ' -> ' + $val)
            }
        }
    }
    if ($orig['hibernate'] -eq '1') {
        & powercfg /h on | Out-Null
        Say '      hibernation re-enabled'
    }
    & powercfg /setactive SCHEME_CURRENT | Out-Null
} else {
    Say '      no recorded values found - leaving power settings untouched'
}

# ---- 5. re-enable the automatic maintenance window ----
Say '[5/8] Re-enabling the automatic maintenance window ...'
$mk = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance'
if (Test-Path $mk) {
    Remove-ItemProperty -Path $mk -Name 'MaintenanceDisabled' -Force -ErrorAction SilentlyContinue
    Say '      MaintenanceDisabled removed'
} else {
    Say '      maintenance key not present'
}

# ---- 6. restart background services we may have stopped ----
Say '[6/8] Restarting background services ...'
foreach ($s in @('SysMain', 'WSearch', 'DoSvc', 'DiagTrack', 'UsoSvc', 'wuauserv')) {
    $svc = Get-Service $s -ErrorAction SilentlyContinue
    if ($svc -and $svc.Status -ne 'Running') {
        Start-Service $s -ErrorAction SilentlyContinue
        $n = (Get-Service $s).Status
        Say ('      ' + $s + ' -> ' + $n)
    }
}

# ---- 7. turn the radios back on ----
Say '[7/8] Turning radios back on ...'
try {
    Add-Type -AssemblyName System.Runtime.WindowsRuntime -ErrorAction Stop
    $asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
        $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and
        $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
    function Await($op, $t) { $asTask.MakeGenericMethod($t).Invoke($null, @($op)).GetAwaiter().GetResult() }
    [Windows.Devices.Radios.Radio, Windows.Devices.Radios, ContentType = WindowsRuntime] | Out-Null
    $rs = Await ([Windows.Devices.Radios.Radio]::GetRadiosAsync()) ([System.Collections.Generic.IReadOnlyList[Windows.Devices.Radios.Radio]])
    foreach ($r in $rs) {
        if ($r.State.ToString() -eq 'Off') {
            Await ($r.SetStateAsync([Windows.Devices.Radios.RadioState]::On)) ([Windows.Devices.Radios.RadioAccessStatus]) | Out-Null
            Say ('      ' + $r.Kind + ' -> On')
        }
    }
    Start-Service bthserv -ErrorAction SilentlyContinue
} catch {
    Say ('      radio restore skipped: ' + $_.Exception.Message)
}

# ---- 8. delete app data ----
Say '[8/8] Deleting application data ...'
$pd = Join-Path $env:ProgramData 'Sleepify'
if (Test-Path $pd) {
    Remove-Item $pd -Recurse -Force -ErrorAction SilentlyContinue
    Say ('      removed ' + $pd)
}
# Windows Firewall / power scheme left untouched on purpose.

Say ''
Say '============================================================'
Say ' Revert complete.'
Say '============================================================'

# ---- final dialog: delete everything, or keep the folder ----
$i18nPath = Join-Path $here 'i18n.ps1'
if (Test-Path $i18nPath) { . $i18nPath }

$title = 'Sleepify'
$msg = "All changes made by this application have been reverted.`r`n`r`nYou can now safely delete everything in this folder."
$btnDeleteText = 'Delete all'
$btnCloseText = 'Close'
if (Get-Command Get-SleepifyString -ErrorAction SilentlyContinue) {
    $title = Get-SleepifyString 'finalTitle'
    $msg = Get-SleepifyString 'finalBody'
    $btnDeleteText = Get-SleepifyString 'btnDelete'
    $btnCloseText = Get-SleepifyString 'btnClose'
}

Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.Drawing -ErrorAction SilentlyContinue

$form = New-Object System.Windows.Forms.Form
$form.Text = $title
$form.ClientSize = New-Object System.Drawing.Size(540, 250)
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedDialog
$form.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen
$form.MaximizeBox = $false
$form.MinimizeBox = $false
$form.Font = New-Object System.Drawing.Font('Microsoft YaHei UI', 9)

$label = New-Object System.Windows.Forms.Label
$label.SetBounds(22, 20, 496, 120)
$label.Text = $msg
$form.Controls.Add($label)

$btnDelete = New-Object System.Windows.Forms.Button
$btnDelete.Text = $btnDeleteText
$btnDelete.SetBounds(230, 185, 140, 34)
$btnDelete.DialogResult = [System.Windows.Forms.DialogResult]::Yes
$form.Controls.Add($btnDelete)

$btnClose = New-Object System.Windows.Forms.Button
$btnClose.Text = $btnCloseText
$btnClose.SetBounds(380, 185, 140, 34)
$btnClose.DialogResult = [System.Windows.Forms.DialogResult]::No
$form.Controls.Add($btnClose)

$form.AcceptButton = $btnDelete
$form.CancelButton = $btnClose

$answer = $form.ShowDialog()
$form.Dispose()

if ($answer -eq [System.Windows.Forms.DialogResult]::Yes) {
    Write-Host ''
    Write-Host 'Deleting the application folder ...'
    # The folder can only be removed after every process inside it has exited,
    # so hand the job to a detached cmd that waits a few seconds first.
    $arg = '/c timeout /t 3 /nobreak >nul & rd /s /q "' + $here + '"'
    Start-Process -FilePath 'cmd.exe' -ArgumentList $arg -WindowStyle Hidden -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    Write-Host 'Done. The folder will disappear in a few seconds.'
} else {
    Write-Host ''
    Write-Host 'Closed. The folder and its remaining files were kept.'
}

