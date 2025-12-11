@echo off
REM Script to delete files with reserved Windows names (nul, con, prn, aux, etc.)
REM Usage: delete-reserved-filename.bat <full-path-to-file>
REM Example: delete-reserved-filename.bat "D:\@Timelapse\Timelapse\nul"

if "%~1"=="" (
    echo Usage: %~nx0 ^<full-path-to-file^>
    echo Example: %~nx0 "D:\@Timelapse\Timelapse\nul"
    exit /b 1
)

echo Attempting to delete: %~1
del "\\?\%~1"

if %errorlevel% equ 0 (
    echo Successfully deleted: %~1
) else (
    echo Failed to delete: %~1
    exit /b 1
)
