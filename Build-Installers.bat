@echo off
title AdvGen Offline Web Downloader - Installer Builder

echo ========================================================
echo AdvGen Offline Web Downloader - Installer Builder
echo ========================================================
echo.

REM Check if PowerShell is available
where powershell >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: PowerShell is not available
    echo Please install PowerShell or run the build script manually
    pause
    exit /b 1
)

echo Select build configuration:
echo 1. Debug Build (x64)
echo 2. Release Build (x64) - Recommended
echo 3. Release Build (x86)  
echo 4. Release Build (ARM64)
echo 5. All Platforms (Release)
echo 6. Custom build...
echo.

set /p choice="Enter your choice (1-6): "

if "%choice%"=="1" (
    set config=Debug
    set platform=x64
    goto :build
)
if "%choice%"=="2" (
    set config=Release
    set platform=x64
    goto :build
)
if "%choice%"=="3" (
    set config=Release
    set platform=x86
    goto :build
)
if "%choice%"=="4" (
    set config=Release
    set platform=ARM64
    goto :build
)
if "%choice%"=="5" (
    goto :buildall
)
if "%choice%"=="6" (
    goto :custom
)

echo Invalid choice. Please try again.
pause
exit /b 1

:build
echo.
echo Building %config% configuration for %platform%...
echo.
powershell.exe -ExecutionPolicy Bypass -File "Build-Installers.ps1" -Configuration %config% -Platform %platform%
goto :end

:buildall
echo.
echo Building all platforms (Release configuration)...
echo.
powershell.exe -ExecutionPolicy Bypass -File "Build-Installers.ps1" -Configuration Release -Platform x64
if %errorlevel% neq 0 goto :error
powershell.exe -ExecutionPolicy Bypass -File "Build-Installers.ps1" -Configuration Release -Platform x86 -SkipBuild
if %errorlevel% neq 0 goto :error
powershell.exe -ExecutionPolicy Bypass -File "Build-Installers.ps1" -Configuration Release -Platform ARM64 -SkipBuild
if %errorlevel% neq 0 goto :error
goto :end

:custom
echo.
set /p config="Enter configuration (Debug/Release): "
set /p platform="Enter platform (x86/x64/ARM64): "
set /p version="Enter version (default 1.0.0.0): "
if "%version%"=="" set version=1.0.0.0

echo.
echo Building %config% configuration for %platform%, version %version%...
echo.
powershell.exe -ExecutionPolicy Bypass -File "Build-Installers.ps1" -Configuration %config% -Platform %platform% -Version %version%
goto :end

:error
echo.
echo BUILD FAILED!
pause
exit /b 1

:end
if %errorlevel% equ 0 (
    echo.
    echo BUILD COMPLETED SUCCESSFULLY!
    echo.
    echo Check the 'Installers' folder for the generated packages.
) else (
    echo.
    echo BUILD FAILED! Check the output above for errors.
)

echo.
pause