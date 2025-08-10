param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    
    [ValidateSet('x86', 'x64', 'ARM64')]
    [string]$Platform = 'x64',
    
    [string]$Version = '1.0.0.0',
    [switch]$SkipBuild = $false,
    [switch]$SkipMSIX = $false,
    [switch]$CreateZipPackage = $true
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$solutionPath = Join-Path $projectRoot "advgenofflinewebdownloader.sln"
$appProjectPath = Join-Path $projectRoot "advgenofflinewebdownloader\advgenofflinewebdownloader.csproj"
$outputDir = Join-Path $projectRoot "Installers\$Configuration-$Platform"

Write-Host "Building AdvGen Offline Web Downloader" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration, Platform: $Platform, Version: $Version" -ForegroundColor Green

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

if (-not $SkipBuild) {
    Write-Host "Building application..." -ForegroundColor Yellow
    dotnet restore $solutionPath --verbosity quiet
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to restore packages"
    }
    
    dotnet build $solutionPath --configuration $Configuration --verbosity minimal --no-restore /p:Platform=$Platform /p:WindowsAppSDKSelfContained=true /p:PublishReadyToRun=false
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build application"
    }
    
    Write-Host "Build completed successfully" -ForegroundColor Green
}

if (-not $SkipMSIX) {
    Write-Host "Building MSIX package..." -ForegroundColor Yellow
    $msixOutputDir = Join-Path $outputDir "MSIX"
    New-Item -ItemType Directory -Path $msixOutputDir -Force | Out-Null
    
    # Create MSIX package using MSBuild directly for better control
    $msbuildArgs = @(
        $appProjectPath
        "/p:Configuration=$Configuration"
        "/p:Platform=$Platform"
        "/p:RuntimeIdentifier=win-$Platform"
        "/p:WindowsAppSDKSelfContained=true"
        "/p:GenerateAppxPackageOnBuild=true"
        "/p:AppxPackageDir=$msixOutputDir\"
        "/p:AppxBundle=Never"
        "/p:AppxBundlePlatforms=$Platform"
        "/p:AppxPackageSigningEnabled=false"
        "/p:UapAppxPackageBuildMode=SideloadOnly"
        "/p:PublishReadyToRun=false"
        "/p:PublishSingleFile=false"
        "/t:Publish"
        "/verbosity:minimal"
    )
    
    Write-Host "Creating MSIX with MSBuild..." -ForegroundColor Gray
    & dotnet msbuild @msbuildArgs
    
    if ($LASTEXITCODE -eq 0) {
        # Look for generated MSIX files
        $msixFiles = Get-ChildItem $msixOutputDir -Filter "*.msix" -Recurse
        if ($msixFiles.Count -eq 0) {
            $msixFiles = Get-ChildItem $msixOutputDir -Filter "*.appxupload" -Recurse
        }
        if ($msixFiles.Count -eq 0) {
            # Look in the app packages folder (where MSIX files are actually created)
            $appPackagesDir = Join-Path (Split-Path $appProjectPath) "AppPackages"
            Write-Host "Searching for MSIX in: $appPackagesDir" -ForegroundColor Gray
            if (Test-Path $appPackagesDir) {
                $msixFiles = Get-ChildItem $appPackagesDir -Filter "*.msix" -Recurse
                Write-Host "Found $($msixFiles.Count) MSIX files in AppPackages" -ForegroundColor Gray
            }
        }
        
        if ($msixFiles.Count -gt 0) {
            $finalMsixPath = Join-Path $outputDir "AdvGenOfflineWebDownloader-v$Version-$Platform.msix"
            Copy-Item $msixFiles[0].FullName $finalMsixPath -Force
            $packageSize = [math]::Round((Get-Item $finalMsixPath).Length / 1MB, 2)
            Write-Host "✅ MSIX package created: $finalMsixPath ($packageSize MB)" -ForegroundColor Green
            
            # Create installation instructions
            $installInstructions = @"
MSIX Package Installation Instructions
=====================================

This MSIX package can be installed in several ways:

1. DOUBLE-CLICK INSTALLATION (Recommended):
   - Double-click the .msix file
   - Click 'Install' when prompted
   - The app will appear in Start Menu

2. POWERSHELL INSTALLATION:
   - Open PowerShell as Administrator
   - Run: Add-AppxPackage -Path "$($finalMsixPath | Split-Path -Leaf)"

3. DEVELOPER MODE (For unsigned packages):
   - Go to Settings > Update & Security > For Developers
   - Enable 'Developer Mode'
   - Then double-click the .msix file

SYSTEM REQUIREMENTS:
- Windows 10 version 1809 (17763) or later
- Windows 11 (any version)

UNINSTALLATION:
- Go to Settings > Apps > Apps & Features
- Find "AdvGen Offline Web Downloader"
- Click Uninstall

Package Version: $Version
Platform: $Platform
"@
            $installInstructions | Out-File (Join-Path $outputDir "MSIX_INSTALLATION_GUIDE.txt") -Encoding UTF8
            
        } else {
            Write-Warning "⚠️  No MSIX files found after build"
            Write-Host "Checking build output directories..." -ForegroundColor Gray
            Get-ChildItem $msixOutputDir -Recurse | Select-Object FullName, Length | Format-Table
        }
    } else {
        Write-Warning "⚠️  MSIX build failed with exit code $LASTEXITCODE"
    }
}

if ($CreateZipPackage) {
    Write-Host "Creating portable ZIP package..." -ForegroundColor Yellow
    $appBinPath = Join-Path $projectRoot "advgenofflinewebdownloader\bin\$Platform\$Configuration\net8.0-windows10.0.26100.0\win-$Platform"
    
    if (Test-Path $appBinPath) {
        $zipTempDir = Join-Path $outputDir "ZipTemp"
        $zipPath = Join-Path $outputDir "AdvGenOfflineWebDownloader-v$Version-$Platform-Portable.zip"
        
        if (Test-Path $zipTempDir) {
            Remove-Item $zipTempDir -Recurse -Force
        }
        New-Item -ItemType Directory -Path $zipTempDir -Force | Out-Null
        
        Copy-Item "$appBinPath\*" $zipTempDir -Recurse -Force
        
        $docFiles = @('README.md', 'LICENSE.txt', 'LICENSE')
        foreach ($docFile in $docFiles) {
            $docPath = Join-Path $projectRoot $docFile
            if (Test-Path $docPath) {
                Copy-Item $docPath $zipTempDir -Force
            }
        }
        
        $launchScript = @"
@echo off
title AdvGen Offline Web Downloader v$Version

echo Starting AdvGen Offline Web Downloader...
echo Version: $Version
echo Platform: $Platform
echo.

if not exist "advgenofflinewebdownloader.exe" (
    echo ERROR: Main executable not found!
    pause
    exit /b 1
)

start "" "advgenofflinewebdownloader.exe"
"@
        $launchScript | Out-File (Join-Path $zipTempDir "Launch.bat") -Encoding ASCII
        
        Compress-Archive -Path "$zipTempDir\*" -DestinationPath $zipPath -Force
        Remove-Item $zipTempDir -Recurse -Force
        
        $zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
        Write-Host "ZIP package created: $zipPath ($zipSize MB)" -ForegroundColor Green
    } else {
        Write-Warning "Build output not found: $appBinPath"
    }
}

Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Output directory: $outputDir" -ForegroundColor Cyan