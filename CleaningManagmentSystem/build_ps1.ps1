# Build the self-extracting PowerShell installer

$installDir = "C:\Program Files\YekaCleaning"
$b64 = [System.IO.File]::ReadAllText("c:\yeka_sfx\app_b64.txt")

$header = @'
# Yeka Cleaning Management System - Installer
# Run with: Right-click -> Run with PowerShell  (or double-click Install.bat)
# No code signing required.

param([string]$InstallPath = "C:\Program Files\YekaCleaning")

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Yeka Cleaning Management System - Installer" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Check admin
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Restarting as Administrator..." -ForegroundColor Yellow
    Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    exit
}

Write-Host "Installing to: $InstallPath" -ForegroundColor Green

# Create install directory
if (!(Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

# Extract app from embedded base64
Write-Host "Extracting application files..." -ForegroundColor Yellow
$zipPath = "$env:TEMP\yeka_app.zip"

'@

$footer = @'

[System.IO.File]::WriteAllBytes($zipPath, [Convert]::FromBase64String($b64Data))

Write-Host "Copying files to $InstallPath ..." -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $InstallPath) 2>$null
# Overwrite if exists
if (Test-Path "$InstallPath\_extracted") { Remove-Item "$InstallPath\_extracted" -Recurse -Force }
Get-ChildItem $zipPath -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue

Write-Host "Creating Start Menu shortcuts..." -ForegroundColor Yellow
$startMenu = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Yeka Cleaning"
if (!(Test-Path $startMenu)) { New-Item -ItemType Directory -Path $startMenu -Force | Out-Null }

$WshShell = New-Object -ComObject WScript.Shell

# Start Server shortcut
$sc1 = $WshShell.CreateShortcut("$startMenu\Yeka Cleaning - Start Server.lnk")
$sc1.TargetPath = "$InstallPath\Start-YekaCleaning.bat"
$sc1.WorkingDirectory = $InstallPath
$sc1.Description = "Start Yeka Cleaning Server"
$sc1.Save()

# Open Browser shortcut  
$sc2 = $WshShell.CreateShortcut("$startMenu\Yeka Cleaning - Open Browser.lnk")
$sc2.TargetPath = "$InstallPath\Open-Browser.bat"
$sc2.WorkingDirectory = $InstallPath
$sc2.Description = "Open Yeka Cleaning in Browser"
$sc2.Save()

# Desktop shortcut
$desktop = [Environment]::GetFolderPath("CommonDesktopDirectory")
$sc3 = $WshShell.CreateShortcut("$desktop\Yeka Cleaning.lnk")
$sc3.TargetPath = "$InstallPath\Start-YekaCleaning.bat"
$sc3.WorkingDirectory = $InstallPath
$sc3.Description = "Yeka Cleaning Management System"
$sc3.Save()

Write-Host ""
Write-Host "Installation complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Starting Yeka Cleaning server..." -ForegroundColor Cyan
Start-Process "$InstallPath\CleaningManagmentSystem.exe" -WorkingDirectory $InstallPath

Start-Sleep -Seconds 3
Write-Host "Opening browser at http://localhost:5000 ..." -ForegroundColor Cyan
Start-Process "http://localhost:5000"

Write-Host ""
Write-Host "Done! Yeka Cleaning is running at http://localhost:5000" -ForegroundColor Green
Write-Host "A shortcut has been added to your Desktop and Start Menu." -ForegroundColor Green
Write-Host ""
Read-Host "Press Enter to close"
'@

# Build full script content
$b64Line = "`$b64Data = `"$b64`""

$fullScript = $header + "`n" + $b64Line + "`n" + $footer

# Ensure output dir exists
if (!(Test-Path "c:\yeka_sfx")) { New-Item -ItemType Directory -Path "c:\yeka_sfx" -Force | Out-Null }

[System.IO.File]::WriteAllText("c:\yeka_sfx\Install-YekaCleaning.ps1", $fullScript, [System.Text.Encoding]::UTF8)

Write-Host "PS1 installer created: c:\yeka_sfx\Install-YekaCleaning.ps1"
Write-Host "Size: $([Math]::Round((Get-Item 'c:\yeka_sfx\Install-YekaCleaning.ps1').Length / 1MB, 1)) MB"
