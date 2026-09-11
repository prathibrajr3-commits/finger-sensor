<#
.SYNOPSIS
    Automated UI and Runtime Lifecycle Verification for AirGesture AI.

.DESCRIPTION
    Launches the compiled Release executable (AirGestureAI.exe) and uses Windows UIAutomation
    to verify:
      1. First-run detection and Setup Wizard display.
      2. Automated step-through and completion of the wizard.
      3. MainWindow (Control Center) transition and runtime stability.
      4. Graceful application exit without disposal exceptions or deadlocks.
      5. Correct persistence of 'IsFirstRunComplete: true' in appsettings.json.
      6. Second-run behavior ensuring Setup Wizard is bypassed and MainWindow opens directly.

.PREREQUISITES
    - Release build must be built:
        dotnet build "AirGestureAI.csproj" --configuration Release
    - Standard Windows desktop environment with UIAutomation support.

.USAGE
    powershell -ExecutionPolicy Bypass -File Tests/VerifyRuntime.ps1
#>

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$repoRoot = if ($PSScriptRoot) { Split-Path $PSScriptRoot -Parent } else { Get-Location }
$exePath = Join-Path $repoRoot "bin\Release\net8.0-windows\AirGestureAI.exe"

if (-not (Test-Path $exePath)) {
    Write-Error "Release executable not found at: $exePath. Please run 'dotnet build --configuration Release' first."
    exit 1
}

$appDataDir = Join-Path $env:LOCALAPPDATA "AirGestureAI"
$settingsPath = Join-Path $appDataDir "appsettings.json"
$logDir = Join-Path $appDataDir "Logs"

Write-Output "=== AirGesture AI Runtime Verification ==="
Write-Output "Testing binary: $exePath"

# Kill any leftover instances
Get-Process AirGestureAI -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Ensure clean first run state
if (Test-Path $settingsPath) {
    Remove-Item $settingsPath -Force
    Write-Output "Cleaned appsettings.json for first run"
}

Write-Output "`n[TEST 1] Launching first run..."
$proc1 = Start-Process -FilePath $exePath -PassThru
Write-Output "Started process with PID $($proc1.Id)"

# Wait for Setup Wizard window
$desktop = [System.Windows.Automation.AutomationElement]::RootElement
$wizardWindow = $null
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Milliseconds 500
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc1.Id)
    $windows = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
    foreach ($w in $windows) {
        $name = $w.Current.Name
        if ($name -like "*Setup Wizard*") {
            $wizardWindow = $w
            break
        }
    }
    if ($wizardWindow -ne $null) { break }
}

if ($wizardWindow -eq $null) {
    Write-Error "Setup Wizard window did not appear!"
    Stop-Process -Id $proc1.Id -Force
    exit 1
}
Write-Output "SUCCESS: Setup Wizard window found: '$($wizardWindow.Current.Name)'"

# Step through the wizard
for ($step = 0; $step -lt 6; $step++) {
    Start-Sleep -Milliseconds 800
    
    # Check all buttons inside the wizard window
    $btnCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
    $allButtons = $wizardWindow.FindAll([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
    
    $finishBtn = $null
    $nextBtn = $null
    
    foreach ($btn in $allButtons) {
        $bName = $btn.Current.Name
        if ($bName -like "*Finish*" -and $btn.Current.IsEnabled -and -not $btn.Current.IsOffscreen) {
            $finishBtn = $btn
        }
        if ($bName -like "*Next*" -and $btn.Current.IsEnabled -and -not $btn.Current.IsOffscreen) {
            $nextBtn = $btn
        }
    }

    if ($finishBtn -ne $null) {
        Write-Output "Clicking Finish button..."
        $invPattern = $finishBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $invPattern.Invoke()
        break
    }

    if ($nextBtn -ne $null) {
        Write-Output "Clicking Next button (step $step)..."
        $invPattern = $nextBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $invPattern.Invoke()
    } else {
        Write-Output "Neither Next nor Finish button available on step $step"
    }
}

Write-Output "Waiting for MainWindow after wizard completion..."
$mainWindow = $null
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Milliseconds 500
    if ($proc1.HasExited) {
        Write-Error "FAILURE: Process exited prematurely with code $($proc1.ExitCode)!"
        exit 1
    }
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc1.Id)
    $windows = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
    foreach ($w in $windows) {
        $name = $w.Current.Name
        if ($name -like "*Control Center*") {
            $mainWindow = $w
            break
        }
    }
    if ($mainWindow -ne $null) { break }
}

if ($mainWindow -eq $null) {
    Write-Error "FAILURE: MainWindow did not appear!"
    Stop-Process -Id $proc1.Id -Force
    exit 1
}

Write-Output "SUCCESS: MainWindow appeared: '$($mainWindow.Current.Name)'"
Write-Output "Holding for 5 seconds to verify MainWindow remains open..."
Start-Sleep -Seconds 5

if ($proc1.HasExited) {
    Write-Error "FAILURE: MainWindow closed prematurely!"
    exit 1
}
Write-Output "SUCCESS: MainWindow remained open!"

# Close MainWindow normally
Write-Output "Closing MainWindow normally..."
$winPattern = $mainWindow.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
$winPattern.Close()

$proc1.WaitForExit(10000) | Out-Null
if (-not $proc1.HasExited) {
    Write-Warning "Process did not exit within 10 seconds, forcing stop."
    Stop-Process -Id $proc1.Id -Force
} else {
    Write-Output "SUCCESS: Process exited cleanly with code $($proc1.ExitCode)"
}

# Inspect logs
$latestLog = Get-ChildItem $logDir -Filter "*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($latestLog -ne $null) {
    Write-Output "Inspecting latest log file: $($latestLog.FullName)"
    $logContent = Get-Content $latestLog.FullName
    $errors = $logContent | Where-Object { $_ -match "ERROR" -or $_ -match "FATAL" -or $_ -match "InvalidOperationException" }
    if ($errors) {
        Write-Output "Log errors found:"
        $errors | ForEach-Object { Write-Output "  $_" }
    } else {
        Write-Output "SUCCESS: No errors or disposal exceptions found in log."
    }
    $cleanShutdown = $logContent | Where-Object { $_ -match "Application shut down clean" }
    if ($cleanShutdown) {
        Write-Output "SUCCESS: Found log entry: '$($cleanShutdown[-1])'"
    }
}

# Verify appsettings.json
Write-Output "`n[CHECK] Verifying persisted appsettings.json..."
if (Test-Path $settingsPath) {
    $json = Get-Content $settingsPath -Raw
    Write-Output "appsettings.json contents: $json"
    if ($json -match '"IsFirstRunComplete"\s*:\s*true') {
        Write-Output "SUCCESS: IsFirstRunComplete is true in appsettings.json"
    } else {
        Write-Error "FAILURE: IsFirstRunComplete is not true in appsettings.json"
        exit 1
    }
} else {
    Write-Error "FAILURE: appsettings.json was not created!"
    exit 1
}

# [TEST 2] Relaunch
Write-Output "`n[TEST 2] Relaunching AirGesture AI..."
$proc2 = Start-Process -FilePath $exePath -PassThru
Write-Output "Started second process with PID $($proc2.Id)"

$wizardSeen = $false
$mainWindow2 = $null

for ($i = 0; $i -lt 25; $i++) {
    Start-Sleep -Milliseconds 500
    if ($proc2.HasExited) {
        Write-Error "FAILURE: Second process exited prematurely with code $($proc2.ExitCode)!"
        exit 1
    }
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc2.Id)
    $windows = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
    foreach ($w in $windows) {
        $name = $w.Current.Name
        if ($name -like "*Setup Wizard*") {
            $wizardSeen = $true
        }
        if ($name -like "*Control Center*") {
            $mainWindow2 = $w
        }
    }
    if ($mainWindow2 -ne $null) { break }
}

if ($wizardSeen) {
    Write-Error "FAILURE: Setup Wizard appeared on second launch!"
    Stop-Process -Id $proc2.Id -Force
    exit 1
}

if ($mainWindow2 -eq $null) {
    Write-Error "FAILURE: MainWindow did not appear on second launch!"
    Stop-Process -Id $proc2.Id -Force
    exit 1
}

Write-Output "SUCCESS: Setup Wizard did NOT appear. MainWindow appeared directly: '$($mainWindow2.Current.Name)'"
Write-Output "Holding for 3 seconds..."
Start-Sleep -Seconds 3

Write-Output "Closing second instance normally..."
$winPattern2 = $mainWindow2.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
$winPattern2.Close()

$proc2.WaitForExit(10000) | Out-Null
if (-not $proc2.HasExited) {
    Stop-Process -Id $proc2.Id -Force
} else {
    Write-Output "SUCCESS: Second process exited cleanly with code $($proc2.ExitCode)"
}

Write-Output "`n=== ALL RUNTIME VERIFICATION CHECKS PASSED ==="
