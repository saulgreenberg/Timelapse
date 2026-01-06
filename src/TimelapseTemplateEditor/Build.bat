@echo off
echo Building TimelapseTemplateEditor wrapper...

REM Accept configuration as parameter (Debug or Release), default to Release
if "%~1"=="" (
    set CONFIG=Release
) else (
    set CONFIG=%~1
)

REM Set output directory to main Timelapse build folder
set OUTPUT_DIR=..\Timelapse\bin\%CONFIG%\net10.0-windows\win-x64

REM Initialize Visual Studio environment
call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"

REM Compile resource file
echo Compiling resource file...
rc TimelapseTemplateEditor.rc
if %ERRORLEVEL% NEQ 0 (
    echo Failed to compile resource file
    exit /b 1
)

REM Compile the wrapper executable
echo Compiling TimelapseTemplateEditor.exe...
cl.exe /Fe:"%OUTPUT_DIR%\TimelapseTemplateEditor.exe" TimelapseTemplateEditor.cpp TimelapseTemplateEditor.res /link /SUBSYSTEM:WINDOWS
if %ERRORLEVEL% NEQ 0 (
    echo Failed to compile executable
    exit /b 1
)

REM Clean up temporary files
del *.obj *.res >NUL 2>&1

echo TimelapseTemplateEditor.exe built successfully!
echo Configuration: %CONFIG%
echo Output: %OUTPUT_DIR%\TimelapseTemplateEditor.exe
