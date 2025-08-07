using advgenofflinewebdownloader.Services;
using advgenofflinewebdownloader.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using Windows.Storage;
using advgenofflinewebdownloader.Data;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace advgenofflinewebdownloader
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public IMainPageService _mainPageService;
        public MainWindowViewModel _mainWindowView;
        private string _currentProjectFilePath;
        private string _currentDownloadFolder;
        private const int MAX_LOG_MESSAGES = 1000; // Limit UI log messages
        
        public MainWindowViewModel ViewModel => _mainWindowView;
        
        public MainWindow(IMainPageService mainPageService,MainWindowViewModel mainWindowViewModel)
        {
            _mainPageService = mainPageService;
            _mainWindowView = mainWindowViewModel;
            this.InitializeComponent();
        }

        private async void downloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Manually sync TextBox values to ViewModel (WinUI 3 x:Bind issue workaround)
                if (_mainWindowView.CurrentProject != null)
                {
                    _mainWindowView.CurrentProject.Name = txtName.Text ?? "";
                    _mainWindowView.CurrentProject.URL = txtURL.Text ?? "";
                    _mainWindowView.CurrentProject.DownloadPath = _currentDownloadFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }
                
                // Sync ViewModel changes to service
                _mainWindowView.UpdateCurrentProject();
                
                // Debug: Check current project status
                var currentProject = _mainPageService.GetCurrentProject();
                var debugMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [DEBUG] Project Name: '{currentProject?.Name ?? "NULL"}', URL: '{currentProject?.URL ?? "NULL"}'";
                lstMessage.Items.Add(debugMessage);
                WriteToLogFile(debugMessage);
                
                // Validate URL before proceeding
                if (string.IsNullOrEmpty(currentProject?.URL))
                {
                    var errorMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] Please enter a valid website URL";
                    lstMessage.Items.Add(errorMessage);
                    WriteToLogFile(errorMessage);
                    return;
                }
                
                var startMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] Starting download...";
                lstMessage.Items.Add(startMessage);
                WriteToLogFile(startMessage);
                
                // Subscribe to download service events to show status in UI
                if (_mainPageService is MaingPageService service)
                {
                    service.fileDownloadService.StatusChanged += (s, args) =>
                    {
                        // Use Dispatcher to update UI from background thread
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{(args.IsError ? "ERROR" : "INFO")}] {args.Status}";
                            
                            // Limit UI messages to prevent memory issues
                            if (lstMessage.Items.Count >= MAX_LOG_MESSAGES)
                            {
                                lstMessage.Items.RemoveAt(0); // Remove oldest message
                            }
                            lstMessage.Items.Add(logMessage);
                            
                            // Extract download folder from status messages
                            if (args.Status.Contains("Download folder created:"))
                            {
                                var parts = args.Status.Split(':');
                                if (parts.Length > 1)
                                {
                                    _currentDownloadFolder = string.Join(":", parts.Skip(1)).Trim();
                                }
                            }
                            
                            // Also write to log file (with infinite loop protection)
                            WriteToLogFile(logMessage);
                        });
                    };
                }
                
                var result = await _mainPageService.Download();
                
                if (result.Success)
                {
                    _currentDownloadFolder = result.DownloadFolder;
                    
                    var completedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] Download completed: {result.TotalFilesDownloaded} files downloaded";
                    var folderMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] Download folder: {result.DownloadFolder}";
                    var durationMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] Duration: {result.Duration}";
                    
                    lstMessage.Items.Add(completedMessage);
                    lstMessage.Items.Add(folderMessage);
                    lstMessage.Items.Add(durationMessage);
                    
                    WriteToLogFile(completedMessage);
                    WriteToLogFile(folderMessage);
                    WriteToLogFile(durationMessage);
                }
                else
                {
                    var errorMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] Download failed: {result.ErrorMessage}";
                    lstMessage.Items.Add(errorMessage);
                    WriteToLogFile(errorMessage);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] Error: {ex.Message}";
                lstMessage.Items.Add(errorMessage);
                WriteToLogFile(errorMessage);
            }
        }

        private async void OpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add(".json");
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    _currentProjectFilePath = file.Path;
                    lstMessage.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] Loading project from: {file.Path}");
                    
                    var websiteDto = await _mainPageService.LoadProject(file);
                    if (websiteDto != null)
                    {
                        // Update ViewModel with loaded project data (service -> ViewModel sync)
                        var loadedProject = _mainPageService.GetCurrentProject();
                        
                        if (loadedProject != null && (!string.IsNullOrEmpty(loadedProject.Name) || !string.IsNullOrEmpty(loadedProject.URL)))
                        {
                            _mainWindowView.CurrentProject = loadedProject;
                            
                            // Force UI to update TextBoxes with loaded data
                            txtName.Text = loadedProject.Name ?? "";
                            txtURL.Text = loadedProject.URL ?? "";
                            _currentDownloadFolder = loadedProject.DownloadPath;
                            
                            lstMessage.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] Project loaded successfully");
                            lstMessage.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [DEBUG] Name: '{loadedProject.Name}', URL: '{loadedProject.URL}', Path: '{loadedProject.DownloadPath}'");
                        }
                        else
                        {
                            lstMessage.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] Project file appears to be empty or invalid");
                        }
                    }
                    else
                    {
                        lstMessage.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] Failed to load project: Invalid JSON format or file read error");
                    }
                }
            }
            catch (Exception ex)
            {
                lstMessage.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] Error loading project: {ex.Message}");
            }
        }

        private async void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_currentProjectFilePath))
                {
                    _mainWindowView.UpdateCurrentProject();
                    if (_mainPageService.SaveProject(_currentProjectFilePath))
                    {
                        lstMessage.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] Project saved: {_currentProjectFilePath}");
                    }
                    else
                    {
                        lstMessage.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] Failed to save project");
                    }
                }
                else
                {
                    SaveAsMenuItem_Click(sender, e);
                }
            }
            catch (Exception ex)
            {
                lstMessage.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] Error saving project: {ex.Message}");
            }
        }

        private async void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileSavePicker();
                picker.FileTypeChoices.Add("JSON Files", new List<string>() { ".json" });
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.SuggestedFileName = "website_project.json";

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    _currentProjectFilePath = file.Path;
                    _mainWindowView.UpdateCurrentProject();
                    if (_mainPageService.SaveProjectAs(file.Path, _mainPageService.GetCurrentProject()))
                    {
                        lstMessage.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] Project saved as: {file.Path}");
                    }
                    else
                    {
                        lstMessage.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] Failed to save project");
                    }
                }
            }
            catch (Exception ex)
            {
                lstMessage.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] Error saving project: {ex.Message}");
            }
        }

        private async void btnFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FolderPicker();
                picker.FileTypeFilter.Add("*");
                picker.SuggestedStartLocation = PickerLocationId.Desktop;

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    _currentDownloadFolder = folder.Path;
                    lstMessage.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] Download folder set: {folder.Path}");
                }
            }
            catch (Exception ex)
            {
                lstMessage.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] Error selecting folder: {ex.Message}");
            }
        }

        private async void ExportLogsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileSavePicker();
                picker.FileTypeChoices.Add("Text Files", new List<string>() { ".txt" });
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.SuggestedFileName = $"download_logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    var logs = new List<string>();
                    foreach (var item in lstMessage.Items)
                    {
                        logs.Add(item.ToString());
                    }
                    await File.WriteAllLinesAsync(file.Path, logs);
                    
                    lstMessage.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] Logs exported to: {file.Path}");
                }
            }
            catch (Exception ex)
            {
                lstMessage.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] Error exporting logs: {ex.Message}");
            }
        }

        private void ClearLogsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            lstMessage.Items.Clear();
        }

        // Thread-safe log file writing with infinite loop protection
        private readonly object _logFileLock = new object();
        private volatile bool _isWritingToLog = false;

        private void WriteToLogFile(string message)
        {
            if (_isWritingToLog)
                return; // Prevent recursive calls

            try
            {
                lock (_logFileLock)
                {
                    if (_isWritingToLog)
                        return; // Double-check after acquiring lock

                    _isWritingToLog = true;

                    if (!string.IsNullOrEmpty(_currentDownloadFolder))
                    {
                        var logFilePath = Path.Combine(_currentDownloadFolder, "download_log.txt");
                        File.AppendAllText(logFilePath, message + Environment.NewLine);
                    }
                }
            }
            catch (Exception)
            {
                // Silently ignore log file errors to prevent infinite loops
            }
            finally
            {
                _isWritingToLog = false;
            }
        }
    }
}