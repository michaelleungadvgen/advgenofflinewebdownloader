# 🎯 Visual Studio Debug Setup Guide

## Quick Start - F5 Debugging

### 1. Open the Solution in Visual Studio
```
📁 File Location: C:\Users\advgen10\source\repos\advgenofflinewebdownloader\advgenofflinewebdownloader.sln
```

### 2. Set Startup Project (if needed)
- Right-click on `advgenofflinewebdownloader` project in Solution Explorer
- Select **"Set as Startup Project"**

### 3. Choose Debug Profile
In the toolbar dropdown, select one of these profiles:
- **`advgenofflinewebdownloader (Unpackaged)`** ← **Recommended for debugging**
- `advgenofflinewebdownloader (Package)` ← For MSIX package testing

### 4. Start Debugging
**Press F5** or click **▶ Start Debugging**

---

## ✅ What Should Happen

1. **Application builds** with Windows App SDK 1.7.3
2. **WinUI window opens** at 1200×800 pixels
3. **Debugger attaches** - breakpoints will work
4. **All features available**:
   - File menu with Exit option
   - Download functionality
   - Logging system
   - Project save/load

---

## 🛠️ Debug Configuration Details

### Launch Settings (Already Configured)
```json
{
  "profiles": {
    "advgenofflinewebdownloader (Package)": {
      "commandName": "MsixPackage"
    },
    "advgenofflinewebdownloader (Unpackaged)": {
      "commandName": "Project"
    }
  }
}
```

### Project Configuration
- **Target Framework**: .NET 8 Windows
- **Windows App SDK**: 1.7.250310001 (matches your system)
- **Platform**: Any CPU (will use x64 on your system)
- **Output Type**: WinExe

---

## 🔧 Debugging Features Available

### Breakpoints
- Set breakpoints in any .cs file
- **MainWindow.xaml.cs** - UI event handlers
- **FileDownloadService.cs** - Download logic
- **MaingPageService.cs** - Business logic

### Debug Output
- **Output Window** - Build messages
- **Debug Output** - Console.WriteLine, Debug.WriteLine
- **Immediate Window** - Execute code while debugging

### Hot Reload
- **XAML Hot Reload** - Edit UI while running
- **C# Hot Reload** - Edit some C# code while running (limited)

---

## 🚨 Troubleshooting

### If F5 Fails with COM Error
The COM registration error should be **fixed** with Windows App SDK 1.7.3 update, but if it occurs:

1. **Clean Solution**: Build → Clean Solution
2. **Rebuild**: Build → Rebuild Solution  
3. **Restart Visual Studio**
4. **Try again**

### If Application Doesn't Start
1. Check **Error List** window for build errors
2. Ensure **Windows App SDK 1.7.3** is installed
3. Try **"advgenofflinewebdownloader (Unpackaged)"** profile

### If Window Doesn't Appear
- Application is running but may be hidden
- Check **Task Manager** for "advgenofflinewebdownloader.exe"
- Window should appear at 1200×800 size

---

## 🎯 Recommended Debug Workflow

### 1. Start Debugging
```
F5 → Application launches → Window appears at 1200×800
```

### 2. Test Core Features
- **File Menu** → Open/Save/Exit functions
- **Download Button** → Test with a simple website
- **Thread Count** → Adjust and test performance

### 3. Set Breakpoints for Investigation
- **downloadButton_Click()** - Download initiation
- **WriteToLogFile()** - Logging system
- **ExitMenuItem_Click()** - Menu functionality

### 4. Use Debug Features
- **Step Through Code** (F10, F11)
- **Inspect Variables** - Hover or Watch window
- **Modify Values** - Edit variables while debugging

---

## ✨ Success Indicators

When everything is working correctly:
- ✅ **No COM errors** on startup
- ✅ **Window opens** at 1200×800 pixels  
- ✅ **Title shows**: "AdvGen Offline Web Downloader"
- ✅ **File menu** works with Exit option
- ✅ **Download functionality** operational
- ✅ **Breakpoints hit** when expected

---

**Ready to debug! Press F5 and start developing! 🚀**