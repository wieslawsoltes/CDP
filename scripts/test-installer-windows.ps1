param (
    [string]$MsiPath
)

if (-not $MsiPath) {
    Write-Error "Usage: .\test-installer-windows.ps1 -MsiPath <path-to-msi>"
    exit 1
}

$MsiPath = [System.IO.Path]::GetFullPath($MsiPath)
Write-Host "Resolved MSI absolute path: $MsiPath"

Write-Host "Installing $MsiPath..."
$installProcess = Start-Process msiexec.exe -ArgumentList "/i `"$MsiPath`" /qn /norestart /L*v msi-install.log" -NoNewWindow -PassThru -Wait
$exitCode = $installProcess.ExitCode
Write-Host "msiexec install exited with code: $exitCode"

if ($exitCode -ne 0) {
    Write-Error "Failed to install MSI. Exit code: $exitCode"
    if (Test-Path msi-install.log) {
        Write-Host "msiexec install log content:"
        Get-Content msi-install.log -Tail 100
    }
    exit 1
}

$exePath = "${env:ProgramFiles}\CdpInspectorApp\CdpInspectorApp.exe"
Write-Host "Verifying executable path at: $exePath"
if (-not (Test-Path $exePath)) {
    Write-Error "Executable not found at $exePath after installation!"
    exit 1
}

Write-Host "Launching CdpInspectorApp headlessly..."
$proc = Start-Process $exePath -ArgumentList "--headless --port 9223" -PassThru -NoNewWindow

Write-Host "Spawned process with ID: $($proc.Id)"

# Poll CDP endpoint
$success = $false
for ($i = 1; $i -le 30; $i++) {
    Write-Host "Polling http://127.0.0.1:9223/json (attempt $i)..."
    try {
        $response = Invoke-RestMethod -Uri "http://127.0.0.1:9223/json" -TimeoutSec 2
        if ($response -and $response.webSocketDebuggerUrl) {
            Write-Host "CDP server is active and responding!"
            $success = $true
            break
        }
    }
    catch {
        # ignore connection errors during polling
    }
    Start-Sleep -Seconds 1
}

Write-Host "Terminating CdpInspectorApp process..."
Stop-Process -Id $proc.Id -Force

if (-not $success) {
    Write-Error "CDP server did not respond within timeout!"
    exit 1
}

Write-Host "Uninstalling package..."
$uninstallProcess = Start-Process msiexec.exe -ArgumentList "/x `"$MsiPath`" /qn /norestart /L*v msi-uninstall.log" -NoNewWindow -PassThru -Wait
$exitCode = $uninstallProcess.ExitCode
Write-Host "msiexec uninstall exited with code: $exitCode"

if ($exitCode -ne 0) {
    Write-Error "Failed to uninstall MSI. Exit code: $exitCode"
    if (Test-Path msi-uninstall.log) {
        Write-Host "msiexec uninstall log content:"
        Get-Content msi-uninstall.log -Tail 100
    }
    exit 1
}

Write-Host "Verifying clean removal..."
if (Test-Path $exePath) {
    Write-Error "Executable still exists at $exePath after uninstall!"
    exit 1
}

Write-Host "Windows Installer Integration Test PASSED!"
