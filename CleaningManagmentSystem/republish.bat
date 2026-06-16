@echo off
set PROJ=c:\Users\mezig\Music\bimrew 3d\yeka-main\CleaningManagmentSystem\CleaningManagmentSystem.csproj
set OUT=c:\yeka_publish
dotnet publish "%PROJ%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:RazorCompileOnPublish=true -o "%OUT%"
echo EXITCODE=%errorlevel%
