@echo off
echo === Step 1: Clean publish ===
set PROJ=c:\Users\mezig\Music\bimrew 3d\yeka-main\CleaningManagmentSystem\CleaningManagmentSystem.csproj
set SRC=c:\Users\mezig\Music\bimrew 3d\yeka-main\CleaningManagmentSystem
set OUT=c:\yeka_clean_publish

rmdir /s /q "%OUT%" 2>nul
dotnet publish "%PROJ%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:RazorCompileOnPublish=true -o "%OUT%"
if %errorlevel% neq 0 (
    echo PUBLISH FAILED
    exit /b 1
)

echo === Step 2: Remove stale nested folders from publish output ===
rmdir /s /q "%OUT%\publish" 2>nul
rmdir /s /q "%OUT%\publish_new" 2>nul
rmdir /s /q "%OUT%\setup_output" 2>nul
rmdir /s /q "%OUT%\installer_output" 2>nul

echo === Step 3: Copy launcher scripts ===
copy /y "%SRC%\setup_output\app\Start-YekaCleaning.bat" "%OUT%\"
copy /y "%SRC%\setup_output\app\Open-Browser.bat" "%OUT%\"
copy /y "%SRC%\setup_output\app\Install-Service.bat" "%OUT%\"

echo === Step 4: Compile Inno Setup installer ===
set ISS=%SRC%\setup_output\YekaCleaningSetup2.iss
set ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe

"%ISCC%" "%ISS%"
if %errorlevel% neq 0 (
    echo INNO SETUP COMPILE FAILED
    exit /b 1
)

echo.
echo === DONE! Installer is ready. ===

