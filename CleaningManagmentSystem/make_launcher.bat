@echo off
echo Creating Install.bat launcher for the PS1...
(
echo @echo off
echo powershell -NoProfile -ExecutionPolicy Bypass -File "%%~dp0Install-YekaCleaning.ps1"
) > "c:\yeka_sfx\Install.bat"
echo Done.
