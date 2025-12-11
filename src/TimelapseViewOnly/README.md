# TimelapseViewOnly C++ Wrapper

This folder contains a native C++ wrapper executable that launches the main Timelapse application with the `-viewonly` command-line argument.

## Purpose

The View Only mode is fully integrated into the main Timelapse application. This lightweight C++ wrapper provides a convenient separate executable (`Timelapse-ViewOnly.exe`) that users can launch to open Timelapse in read-only mode, preventing accidental modifications to data.

## Architecture

- **TimelapseViewOnly.cpp** - Native C++ launcher that:
  - Locates `Timelapse.exe` in the same directory
  - Launches it with the `-viewonly` argument
  - Waits for the process to complete
  - Provides error handling if Timelapse.exe is not found

- **TimelapseViewOnly.rc** - Resource file containing icon and version information

- **Build.bat** - Build script that compiles the wrapper using Visual Studio C++ compiler

## Building

Run `Build.bat` from this directory. The script will:
1. Initialize the Visual Studio environment
2. Compile the resource file
3. Compile the C++ wrapper
4. Output `Timelapse-ViewOnly.exe` to the main Timelapse build directory

**Note:** This wrapper is built separately from the main solution and is not part of `Timelapse.sln`.
