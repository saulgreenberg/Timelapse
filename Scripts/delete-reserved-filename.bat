@echo off
REM Script to delete files with reserved Windows names (nul, con, prn, aux, etc.)
REM Usage: delete-reserved-filename.bat <full-path-to-file>
REM Example: delete-reserved-filename.bat "D:\@Timelapse\Timelapse\nul"

if "%~1"=="" (
    echo Usage: %~nx0 ^<full-path-to-file^>
    echo Example: %~nx0 "D:\@Timelapse\Timelapse\nul"
    exit /b 1
)

echo Checking if file exists: %~1
if exist "\\?\%~1" (
    echo File found. Attempting to delete...
    del "\\?\%~1" 2>nul
    if %errorlevel% equ 0 (
        echo Successfully deleted: %~1
    ) else (
        echo DEL failed. Trying RMDIR...
        rmdir "\\?\%~1" 2>nul
        if %errorlevel% equ 0 (
            echo Successfully removed directory: %~1
        ) else (
            echo ERROR: Failed to delete: %~1
            exit /b 1
        )
    )
) else (
    echo File does not exist: %~1
    echo Trying alternate method...
    del "\\?\%~1" 2>nul
    rmdir "\\?\%~1" 2>nul
    echo Deletion commands executed. Please verify manually.
)
