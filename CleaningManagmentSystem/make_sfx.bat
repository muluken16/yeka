@echo off
echo === Step 1: Zip the app ===
powershell -NoProfile -Command "Compress-Archive -Path 'c:\yeka_clean_publish\*' -DestinationPath 'c:\yeka_sfx\app.zip' -Force"
if %errorlevel% neq 0 ( echo ZIP FAILED & exit /b 1 )

echo === Step 2: Convert zip to base64 ===
powershell -NoProfile -Command "$bytes = [System.IO.File]::ReadAllBytes('c:\yeka_sfx\app.zip'); $b64 = [Convert]::ToBase64String($bytes); [System.IO.File]::WriteAllText('c:\yeka_sfx\app_b64.txt', $b64)"
if %errorlevel% neq 0 ( echo BASE64 FAILED & exit /b 1 )

echo === Step 3: Build installer .ps1 ===
powershell -NoProfile -ExecutionPolicy Bypass -File "c:\Users\mezig\Music\bimrew 3d\yeka-main\CleaningManagmentSystem\build_ps1.ps1"
if %errorlevel% neq 0 ( echo PS1 BUILD FAILED & exit /b 1 )

echo === DONE! ===
echo Installer: c:\yeka_sfx\Install-YekaCleaning.ps1
