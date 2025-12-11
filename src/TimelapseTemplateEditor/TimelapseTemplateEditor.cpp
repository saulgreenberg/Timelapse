// ReSharper disable CppRedundantZeroInitializerInAggregateInitialization
#include <windows.h>
#include <shlwapi.h>
#include <shellapi.h>
#include <string>

#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "user32.lib")

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow)
{
    // Get the current executable directory
    wchar_t exePath[MAX_PATH];
    GetModuleFileNameW(nullptr, exePath, MAX_PATH);
    PathRemoveFileSpecW(exePath);

    // Build path to Timelapse.exe
    std::wstring timelapseExe = std::wstring(exePath) + L"\\Timelapse.exe";

    // Check if Timelapse.exe exists
    if (!PathFileExistsW(timelapseExe.c_str()))
    {
        MessageBoxW(nullptr,
                    L"Timelapse.exe not found in the same directory. Please reinstall Timelapse.",
                    L"Timelapse Template Editor",
                    MB_OK | MB_ICONERROR);
        return 1;
    }

    // Launch Timelapse.exe with -templateeditor argument
    SHELLEXECUTEINFOW sei = { 0 };  // NOLINT(clang-diagnostic-missing-field-initializers)
    sei.cbSize = sizeof(SHELLEXECUTEINFOW);
    sei.fMask = SEE_MASK_NOCLOSEPROCESS;
    sei.lpVerb = L"open";
    sei.lpFile = timelapseExe.c_str();
    sei.lpParameters = L"-templateeditor";
    sei.lpDirectory = exePath;
    sei.nShow = nCmdShow;

    if (!ShellExecuteExW(&sei))
    {
        DWORD error = GetLastError();
        wchar_t errorMsg[512];
        swprintf(errorMsg, 512,  // NOLINT(cert-err33-c)
                L"Failed to launch Timelapse.exe. Error code: %d", error);
        MessageBoxW(nullptr, errorMsg, L"Timelapse Template Editor", MB_OK | MB_ICONERROR);
        return 1;
    }

    // Wait for the process to finish
    if (sei.hProcess)
    {
        WaitForSingleObject(sei.hProcess, INFINITE);
        DWORD exitCode;
        GetExitCodeProcess(sei.hProcess, &exitCode);
        CloseHandle(sei.hProcess);
        return exitCode;  // NOLINT(bugprone-narrowing-conversions)
    }

    return 0;
}