# 📦 Installation Packages for AdvGen Offline Web Downloader

This document describes the available installation options and how to create distribution packages for the AdvGen Offline Web Downloader WinUI application.

## 🎯 Available Package Types

### 1. MSIX Packages (Recommended)
**Modern Windows App Package Format**

- ✅ **Native Windows integration** with Start Menu, taskbar, and notifications
- ✅ **Automatic updates** through Windows Update or Microsoft Store
- ✅ **Clean installation/uninstallation** with no registry pollution
- ✅ **Sandboxed security** with controlled file system access
- ✅ **Digital signing support** for enterprise deployment
- ⚠️ **Requires Windows 10 1809+** or Windows 11

**File naming**: `AdvGenOfflineWebDownloader-v1.0.0-{platform}.msix`

### 2. Portable ZIP Packages
**Traditional Portable Application Format**

- ✅ **No installation required** - extract and run
- ✅ **Universal compatibility** with all supported Windows versions
- ✅ **Network drive friendly** - can run from shared locations
- ✅ **Multiple instances** - can run different versions simultaneously
- ✅ **Full file system access** - no sandboxing restrictions
- ⚠️ **Manual updates** - user must download and replace files

**File naming**: `AdvGenOfflineWebDownloader-v1.0.0-{platform}-Portable.zip`

### 3. MSI Packages (Future Enhancement)
**Traditional Windows Installer Format**

- ✅ **Enterprise-friendly** with Group Policy deployment support  
- ✅ **Custom installation options** with feature selection
- ✅ **Registry integration** for file associations
- ✅ **Rollback capabilities** with Windows Installer transactions
- ⚠️ **Requires WiX toolset** for building
- ⚠️ **Administrative privileges** may be required for installation

**Status**: Framework created, requires WiX toolset for building

## 🏗️ Building Packages

### Quick Build (Recommended)
```batch
# Run the batch file for interactive building
Build-Installers.bat
```

### PowerShell Build Script
```powershell
# Build Release x64 packages
.\Build-Installers.ps1 -Configuration Release -Platform x64

# Build all platforms
.\Build-Installers.ps1 -Configuration Release -Platform x64
.\Build-Installers.ps1 -Configuration Release -Platform x86 -SkipBuild
.\Build-Installers.ps1 -Configuration Release -Platform ARM64 -SkipBuild

# Custom version
.\Build-Installers.ps1 -Configuration Release -Platform x64 -Version "1.2.0.0"
```

### Manual dotnet Commands
```bash
# Build MSIX package
dotnet publish advgenofflinewebdownloader/advgenofflinewebdownloader.csproj \
  --configuration Release \
  --runtime win-x64 \
  --output ./publish/x64 \
  /p:GenerateAppxPackageOnBuild=true \
  /p:WindowsAppSDKSelfContained=true

# Build for specific platform
dotnet build --configuration Release --platform x64 \
  /p:Platform=x64 \
  /p:WindowsAppSDKSelfContained=true
```

## 📁 Package Structure

### MSIX Package Contents
```
AdvGenOfflineWebDownloader.msix
├── advgenofflinewebdownloader.exe          # Main application
├── Microsoft.WindowsAppSDK.dll             # WinUI runtime
├── Microsoft.WinUI.dll                     # WinUI framework
├── Newtonsoft.Json.dll                     # JSON processing
├── Microsoft.Extensions.DependencyInjection.dll
├── [Other dependency DLLs]
├── Assets/                                 # Application icons
│   ├── Square150x150Logo.png
│   ├── Square44x44Logo.png
│   └── [Other assets]
└── AppxManifest.xml                        # Package manifest
```

### ZIP Package Contents
```
AdvGenOfflineWebDownloader-Portable.zip
├── advgenofflinewebdownloader.exe          # Main application
├── [All dependency DLLs]
├── Launch.bat                              # Quick launch script
├── INSTALLATION.txt                        # Installation guide
├── README.md                               # Documentation
├── LICENSE.txt                             # License information
└── [Configuration files]
```

## 🤖 Automated Building

### GitHub Actions
Packages are automatically built when:
- **Git tags** are pushed (e.g., `git tag v1.0.0 && git push origin v1.0.0`)
- **Manual workflow dispatch** from GitHub Actions page
- **Release creation** through GitHub interface

### CI/CD Pipeline
The `.github/workflows/package.yml` workflow:
1. **Builds** the application for all platforms (x64, x86, ARM64)
2. **Creates** MSIX and ZIP packages for each platform
3. **Tests** package integrity and content validation
4. **Uploads** packages as GitHub artifacts (90-day retention)
5. **Creates** GitHub releases with automatic asset uploads

## 📋 System Requirements

### Development Environment
- **Windows 10/11** with latest Windows SDK
- **.NET 8.0 SDK** or later
- **Visual Studio 2022** or VS Code with C# extension
- **Windows App SDK** (installed via NuGet)

### Target Systems (End Users)

#### For MSIX Packages
- **Windows 10** version 1809 (build 17763) or later
- **Windows 11** (any version)
- **.NET 8 Runtime** (automatically installed if missing)
- **Microsoft Store** access (for automatic updates)

#### For ZIP Packages
- **Windows 10** version 1809 (build 17763) or later
- **Windows 11** (any version)
- **.NET 8 Runtime** (will prompt to install if missing)
- **No special privileges** required

## 🔧 Package Configuration

### Version Management
Update version in multiple locations:
1. **Package.appxmanifest** - MSIX package version
2. **advgenofflinewebdownloader.csproj** - Assembly versions
3. **Build scripts** - Default version parameters

### Manifest Configuration
Key settings in `Package.appxmanifest`:
```xml
<Identity Name="AdvGenOfflineWebDownloader" 
          Publisher="CN=AdvGen Software" 
          Version="1.0.0.0" />
<Properties>
  <DisplayName>AdvGen Offline Web Downloader</DisplayName>
  <PublisherDisplayName>AdvGen Software</PublisherDisplayName>
  <Description>Download complete websites with all resources</Description>
</Properties>
```

### Capabilities and Permissions
```xml
<Capabilities>
  <rescap:Capability Name="runFullTrust" />
</Capabilities>
```

## 🔐 Code Signing (Production)

### For MSIX Packages
```powershell
# Sign with certificate
signtool sign /f "certificate.pfx" /p "password" /t "http://timestamp.digicert.com" "package.msix"

# Verify signature
signtool verify /pa "package.msix"
```

### For ZIP Packages
Individual executables can be signed:
```powershell
# Sign executable
signtool sign /f "certificate.pfx" /p "password" /t "http://timestamp.digicert.com" "advgenofflinewebdownloader.exe"
```

## 📊 Package Validation

### MSIX Validation
```powershell
# Test MSIX package integrity
$zip = [System.IO.Compression.ZipFile]::OpenRead("package.msix")
Write-Host "Package contains $($zip.Entries.Count) files"
$zip.Dispose()

# Install locally for testing (Developer Mode required)
Add-AppxPackage -Path "package.msix"
```

### ZIP Validation
```powershell
# Extract and test
Expand-Archive "package.zip" -DestinationPath "test_extract"
Test-Path "test_extract/advgenofflinewebdownloader.exe"
```

## 📝 Distribution Channels

### Internal Distribution
- **GitHub Releases** - Public releases with automatic asset uploads
- **Network Shares** - Internal company distribution
- **Direct Download** - Direct links to package files

### Microsoft Store (Future)
- **MSIX packages** can be submitted to Microsoft Store
- **Certification required** - Microsoft Store compliance review
- **Automatic updates** - Users get updates through Microsoft Store

### Enterprise Distribution
- **Microsoft Intune** - Deploy MSIX packages to managed devices
- **Group Policy** - MSI packages via software installation policies
- **SCCM/ConfigMgr** - Enterprise software deployment

## 🚀 Quick Start Guide

1. **Choose package type** based on your deployment needs
2. **Run build script**: `Build-Installers.bat`
3. **Select configuration**: Release + target platform(s)
4. **Find packages** in `Installers/` directory
5. **Test installation** on clean system
6. **Distribute packages** through chosen channel

## 📞 Troubleshooting

### Common Issues

**Build fails with "MSIX tooling not found"**
- Install Visual Studio with Windows App Development workload
- Ensure Windows SDK is installed and up to date

**Package won't install on target system**
- Check Windows version compatibility (requires 1809+)
- Verify .NET 8 Runtime is installed
- Enable Developer Mode for unsigned MSIX packages

**ZIP package missing dependencies**
- Ensure build output includes all required DLLs
- Check publish output directory for completeness
- Verify self-contained deployment settings

**WiX MSI build fails**
- Install WiX Toolset v3.11 or later
- Configure WiX project properties correctly
- Check file paths in Product.wxs match build output

---

*For additional support, consult the main README.md or create an issue in the repository.*