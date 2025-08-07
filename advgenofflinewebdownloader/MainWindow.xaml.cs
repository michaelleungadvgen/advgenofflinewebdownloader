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
                // Sync ViewModel changes to service
                _mainWindowView.UpdateCurrentProject();
                
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
            var fileOpenPicker = new FileOpenPicker();
            
            // Initialize file picker
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(fileOpenPicker, hWnd);
            
            // Add file types
            fileOpenPicker.FileTypeFilter.Add(".json");
            
            // Open file picker
            StorageFile file = await fileOpenPicker.PickSingleFileAsync();
            
            if (file != null)
            {
                // Load project from JSON
                var websiteDto = await _mainPageService.LoadProject(file);
                
                if (websiteDto != null)
                {
                    // Update ViewModel with project data
                    var project = _mainPageService.GetCurrentProject();
                    _mainWindowView.CurrentProject = project;
                    _currentProjectFilePath = file.Path;
                    
                    // Add to message list
                    lstMessage.Items.Add($"Project loaded: {file.Path}");
                }
                else
                {
                    lstMessage.Items.Add($"Failed to load project: {file.Path}");
                }
            }
        }

        // Add this method to the MainWindow class
        private async void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var savePicker = new FileSavePicker();

            // Initialize file picker
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

            // Configure save picker
            savePicker.FileTypeChoices.Add("JON file", new List<string>() { ".json" });
        
            // Open file picker
            StorageFile file = await savePicker.PickSaveFileAsync();

            if (file != null)
            {
                try
                {
                    // Sync ViewModel changes to service
                    _mainWindowView.UpdateCurrentProject();
                    
                    // Get current project from service
                    var currentProject = _mainPageService.GetCurrentProject();
                    if (currentProject == null)
                    {
                        // Create new project if none exists
                        currentProject = new Project
                        {
                            Name = _mainWindowView.ProjectName,
                            URL = _mainWindowView.ProjectURL,
                            DownloadPath = _mainWindowView.ProjectDownloadPath,
                            Logs = new List<string>()
                        };
                        _mainPageService.SetCurrentProject(currentProject);
                    }

                    // Save using MainPageService
                    bool success = _mainPageService.SaveProjectAs(file.Path, currentProject);
                    if (success)
                    {
                        _currentProjectFilePath = file.Path;
                        lstMessage.Items.Add($"Project saved: {file.Path}");
                    }
                    else
                    {
                        lstMessage.Items.Add("Failed to save project.");
                    }
                }
                catch (Exception ex)
                {
                    lstMessage.Items.Add($"Error saving project: {ex.Message}");
                }
            }
        }

        private async void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentProjectFilePath))
            {
                // If no file path, use Save As instead
                SaveAsMenuItem_Click(sender, e);
                return;
            }

            try
            {
                // Sync ViewModel changes to service
                _mainWindowView.UpdateCurrentProject();
                
                // Get current project from service
                var currentProject = _mainPageService.GetCurrentProject();
                if (currentProject == null)
                {
                    lstMessage.Items.Add("No project to save.");
                    return;
                }

                // Save using MainPageService
                bool success = _mainPageService.SaveProject(_currentProjectFilePath);
                if (success)
                {
                    lstMessage.Items.Add($"Project saved: {_currentProjectFilePath}");
                }
                else
                {
                    lstMessage.Items.Add("Failed to save project.");
                }
            }
            catch (Exception ex)
            {
                lstMessage.Items.Add($"Error saving project: {ex.Message}");
            }
        }

        private async void btnFolder_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            
            // Initialize folder picker
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hWnd);
            
            // Configure folder picker
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");
            
            // Open folder picker
            StorageFolder folder = await folderPicker.PickSingleFolderAsync();
            
            if (folder != null)
            {
                // Get or create current project
                var currentProject = _mainPageService.GetCurrentProject();
                if (currentProject == null)
                {
                    // Create a new project if none exists
                    currentProject = new Project();
                    _mainPageService.SetCurrentProject(currentProject);
                    _mainWindowView.CurrentProject = currentProject;
                }
                
                // Update project's download path
                currentProject.DownloadPath = folder.Path;
                _mainPageService.SetCurrentProject(currentProject);
                
                // Update ViewModel to reflect changes
                _mainWindowView.CurrentProject = currentProject;
                
                // Update button text to show selected folder
                btnFolder.Content = $"Selected: {folder.Name}";
                
                // Add to message list
                lstMessage.Items.Add($"Download folder set to: {folder.Path}");
            }
        }

        private static readonly object _logFileLock = new object();
        private static bool _isWritingToLog = false;

        private void WriteToLogFile(string logMessage)
        {
            // Prevent infinite loops by checking if we're already writing to log
            if (_isWritingToLog)
                return;

            lock (_logFileLock)
            {
                if (_isWritingToLog)
                    return;

                _isWritingToLog = true;
                try
                {
                    // Use download folder if available, otherwise use current directory
                    var logDirectory = !string.IsNullOrEmpty(_currentDownloadFolder) 
                        ? _currentDownloadFolder 
                        : Directory.GetCurrentDirectory();
                        
                    var logFilePath = Path.Combine(logDirectory, "log.txt");
                    
                    // Ensure directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(logFilePath));
                    
                    File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    // Silently ignore log file write errors to avoid infinite loops
                    // Don't call any logging methods here!
                    System.Diagnostics.Debug.WriteLine($"Failed to write to log file: {ex.Message}");
                }
                finally
                {
                    _isWritingToLog = false;
                }
            }
        }

        private void ExportLogsToFile()
        {
            try
            {
                // Use download folder if available, otherwise use current directory
                var logDirectory = !string.IsNullOrEmpty(_currentDownloadFolder) 
                    ? _currentDownloadFolder 
                    : Directory.GetCurrentDirectory();
                    
                var logFilePath = Path.Combine(logDirectory, "log.txt");
                var allLogs = new List<string>();
                
                // Add header with timestamp
                allLogs.Add($"=== Log Export - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                allLogs.Add("");
                
                // Export all messages from the UI list
                foreach (var item in lstMessage.Items)
                {
                    allLogs.Add(item.ToString());
                }
                
                // Write all logs to file
                File.WriteAllLines(logFilePath, allLogs);
                
                lstMessage.Items.Add($"Logs exported to: {logFilePath}");
            }
            catch (Exception ex)
            {
                lstMessage.Items.Add($"Failed to export logs: {ex.Message}");
            }
        }

        private void ExportLogsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ExportLogsToFile();
        }

        private void ClearLogsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            lstMessage.Items.Clear();
            lstMessage.Items.Add("Logs cleared");
        }
    }
}
