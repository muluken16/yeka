@echo off
echo === Creating Yeka Cleaning portable package ===

set SRC=c:\yeka_clean_publish
set PKG=c:\yeka_package

:: Clean and recreate package folder
rmdir /s /q "%PKG%" 2>nul
mkdir "%PKG%"

:: Copy all app files
xcopy /e /i /y "%SRC%\*" "%PKG%\app\"

:: Create the launcher script
echo Creating launcher...

:: Done - zip will be created by PowerShell
echo Packaging into zip...
powershell -NoProfile -Command "Compress-Archive -Path '%PKG%\app\*' -DestinationPath 'c:\yeka_package\YekaCleaning.zip' -Force"

echo Done.
