# Website Downloader Application - Development Context

## Project Overview
This is a WinUI 3 application that downloads complete websites including all resources (CSS, fonts, images, JavaScript) with support for JSON project files and comprehensive logging.

## Current Architecture
- **WinUI 3** application with MVVM pattern
- **Dependency injection** with service layer architecture
- **HTTP client** with custom SSL certificate validation bypass
- **CSS parsing** with regex for resource extraction
- **Event-driven** architecture for real-time status updates

## Key Files and Components

### UI Layer
- `MainWindow.xaml` - Main UI with form controls and data binding
- `MainWindow.xaml.cs` - Code-behind with download logic and logging
- `ViewModels/MainWindowViewModel.cs` - ViewModel with INotifyPropertyChanged

### Service Layer
- `Services/IMainPageService.cs` - Service interface with project management methods
- `Services/MaingPageService.cs` - Main service coordinating downloads and projects
- `Services/FileDownloadService.cs` - Core download service with CSS processing

### Data Layer
- `Repo/IProjectRepository.cs` - Repository interface for project persistence
- `Data/Project.cs` - Project data model

## Recent Critical Fixes

### 1. Infinite Loop in Logging System (FIXED)
**Location:** `MainWindow.xaml.cs:296-336`
**Problem:** WriteToLogFile() was causing recursive calls through StatusChanged events
**Solution:** 
- Added thread-safe locking with `_logFileLock` object
- Implemented `_isWritingToLog` flag to prevent recursive calls
- Added proper exception handling without triggering more log calls

### 2. CSS Font File Downloading (ENHANCED)
**Location:** `Services/FileDownloadService.cs:364-495`
**Problem:** CSS files with complex multi-URL declarations weren't being parsed correctly
**Solution:**
- Improved regex pattern: `@"url\s*\(\s*[""']?([^""')]+)[""']?\s*\)"`
- Enhanced URL cleaning logic to handle format() functions
- Added comprehensive debugging for CSS parsing

### 3. WinUI 3 Data Binding (FIXED)
**Location:** `MainWindow.xaml:69-71`
**Problem:** WinUI 3 doesn't support DataContext like WPF
**Solution:** Used `{x:Bind ViewModel.ProjectName, Mode=TwoWay}` instead of traditional binding

## Current Status
-  JSON project loading and saving functionality
-  Recursive same-domain downloading with depth control
-  CSS font file extraction and downloading
-  Real-time status logging with file export
-  Thread-safe logging system preventing infinite loops
-  Comprehensive debugging for CSS parsing issues
-  SSL certificate validation bypass for development

## Build Commands
```bash
# Build project (may have packaging issues but DLL compiles successfully)
dotnet build

# The application successfully compiles but has packaging target issues
# The core functionality works correctly
```

## Known Issues
- Build packaging error related to ProcessorArchitecture neutral setting
- Some compiler warnings for unused variables and async methods
- Application compiles successfully but packaging may need platform-specific target

## Testing Focus Areas
1. **Infinite Loop Fix**: Verify logging doesn't cause recursive calls during downloads
2. **CSS Font Files**: Ensure woff2, ttf, svg files are correctly downloaded
3. **Log File Location**: Confirm logs are saved in downloaded content folder
4. **Multi-URL CSS**: Test CSS with multiple url() declarations in single line

## Debug Information
The application provides extensive logging for:
- CSS file detection and processing
- Font file discovery and downloading
- URL resolution and path handling
- Download progress and errors
- File type detection and content processing