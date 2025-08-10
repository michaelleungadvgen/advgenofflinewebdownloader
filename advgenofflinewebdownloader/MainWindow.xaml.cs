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
using System.Threading.Tasks;
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
        
        // Download statistics tracking
        private int _filesDownloaded = 0;
        private int _totalFiles = 0;
        private long _totalBytesDownloaded = 0;
        private int _activeThreads = 0;
        private DateTime _downloadStartTime;
        private readonly object _statsLock = new object();
        
        public MainWindowViewModel ViewModel => _mainWindowView;
        
        public MainWindow(IMainPageService mainPageService,MainWindowViewModel mainWindowViewModel)
        {
            _mainPageService = mainPageService;
            _mainWindowView = mainWindowViewModel;
            this.InitializeComponent();
            
            // Set window title
            this.Title = "AdvGen Offline Web Downloader";
           
            // Subscribe to the Activated event to set window size after window is fully initialized
            this.Activated += MainWindow_Activated;
        }

        private bool _windowSizeSet = false;
        
        private async void MainWindow_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs e)
        {
            // Only set size once when window is first activated
            if (!_windowSizeSet && e.WindowActivationState != Microsoft.UI.Xaml.WindowActivationState.Deactivated)
            {
                _windowSizeSet = true;
                
                // Set window size after window is fully loaded
                SetWindowSize();
                this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(100, 100, 10000, 10000));
                // Also try again after a short delay to ensure the window is fully initialized
                await Task.Delay(100);
                SetWindowSize();
            }
        }

        private void SetWindowSize()
        {
            try
            {
                // Method 1: Using AppWindow (preferred for WinUI 3)
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                
                if (appWindow != null)
                {
                    // Set the desired size                   
                    this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(100, 100, 1500, 1500));
              
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("AppWindow is null - cannot resize");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to resize window: {ex.Message}");
                
                // Method 2: Fallback using Win32 API if AppWindow fails
                try
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 1200, 1000, SWP_NOMOVE | SWP_NOZORDER);
                    System.Diagnostics.Debug.WriteLine("Used Win32 SetWindowPos fallback");
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Win32 fallback also failed: {fallbackEx.Message}");
                }
            }
        }

        // Win32 API fallback for window sizing
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;

        private async void downloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable download button to prevent multiple concurrent downloads
                downloadButton.IsEnabled = false;
                
                // Manually sync TextBox values to ViewModel (WinUI 3 x:Bind issue workaround)
                if (_mainWindowView.CurrentProject != null)
                {
                    _mainWindowView.CurrentProject.Name = txtName.Text ?? "";
                    _mainWindowView.CurrentProject.URL = txtURL.Text ?? "";
                    _mainWindowView.CurrentProject.DownloadPath = _currentDownloadFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }
                
                // Sync ViewModel changes to service
                _mainWindowView.UpdateCurrentProject();
                
                // Get thread count from UI
                var threadCount = (int)Math.Max(1, Math.Min(numThreadCount.Value, 16));
                
                // Debug: Check current project status
                var currentProject = _mainPageService.GetCurrentProject();
                var debugMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [DEBUG] Project Name: '{currentProject?.Name ?? "NULL"}', URL: '{currentProject?.URL ?? "NULL"}', Threads: {threadCount}";
                lstMessage.Items.Add(debugMessage);
                WriteToLogFile(debugMessage);
                
                // Validate URL before proceeding
                if (string.IsNullOrEmpty(currentProject?.URL))
                {
                    var errorMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] Please enter a valid website URL";
                    lstMessage.Items.Add(errorMessage);
                    WriteToLogFile(errorMessage);
                    downloadButton.IsEnabled = true; // Re-enable button on validation error
                    return;
                }
                
                var startMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] Starting download with {threadCount} threads...";
                lstMessage.Items.Add(startMessage);
                WriteToLogFile(startMessage);
                
                // Initialize download statistics
                InitializeDownloadStats();
                UpdateDownloadStatus("Starting...");
                UpdateActiveThreads(threadCount);
                
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
                            
                            // Update statistics based on status messages
                            UpdateStatisticsFromStatusMessage(args.Status, args.IsError);
                            
                            // Also write to log file (with infinite loop protection)
                            WriteToLogFile(logMessage);
                        });
                    };
                }
                
                // Run download in background task
                var result = await Task.Run(async () => await _mainPageService.Download(threadCount));
                
                if (result.Success)
                {
                    _currentDownloadFolder = result.DownloadFolder;
                    
                    // Update final statistics
                    UpdateFileStats(result.TotalFilesDownloaded, result.TotalFilesDownloaded);
                    UpdateDownloadStatus("Completed");
                    UpdateActiveThreads(0);
                    
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
                    UpdateDownloadStatus("Failed");
                    UpdateActiveThreads(0);
                    
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
            finally
            {
                // Re-enable download button
                downloadButton.IsEnabled = true;
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

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Close the application gracefully
            this.Close();
        }

        private void ResizeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Manual resize trigger for testing
            SetWindowSize();
        }

        #region Download Statistics Methods

        private void InitializeDownloadStats()
        {
            lock (_statsLock)
            {
                _filesDownloaded = 0;
                _totalFiles = 0;
                _totalBytesDownloaded = 0;
                _activeThreads = 0;
                _downloadStartTime = DateTime.Now;
            }
            UpdateStatsUI();
        }

        private void UpdateFileStats(int totalFiles, int downloadedFiles)
        {
            lock (_statsLock)
            {
                _totalFiles = totalFiles;
                _filesDownloaded = downloadedFiles;
            }
            UpdateStatsUI();
        }

        private void IncrementDownloadedFiles()
        {
            lock (_statsLock)
            {
                _filesDownloaded++;
            }
            UpdateStatsUI();
        }

        private void UpdateBytesDownloaded(long additionalBytes)
        {
            lock (_statsLock)
            {
                _totalBytesDownloaded += additionalBytes;
            }
            UpdateStatsUI();
        }

        private void UpdateActiveThreads(int activeThreads)
        {
            lock (_statsLock)
            {
                _activeThreads = activeThreads;
            }
            UpdateStatsUI();
        }

        private void UpdateDownloadStatus(string status)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                txtDownloadStatus.Text = status;
            });
        }

        private void UpdateStatsUI()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                lock (_statsLock)
                {
                    // Update files count
                    txtFilesDownloaded.Text = _filesDownloaded.ToString();
                    
                    // Update data downloaded with proper formatting
                    txtDataDownloaded.Text = FormatBytes(_totalBytesDownloaded);
                    
                    // Update active threads
                    txtActiveThreads.Text = _activeThreads.ToString();
                    
                    // Calculate and update download speed
                    var elapsed = DateTime.Now - _downloadStartTime;
                    if (elapsed.TotalSeconds > 1 && _totalBytesDownloaded > 0)
                    {
                        var bytesPerSecond = _totalBytesDownloaded / elapsed.TotalSeconds;
                        txtDownloadSpeed.Text = FormatBytesPerSecond(bytesPerSecond);
                    }
                    else
                    {
                        txtDownloadSpeed.Text = "0 KB/s";
                    }
                }
            });
        }

        private void UpdateStatisticsFromStatusMessage(string status, bool isError)
        {
            if (isError)
                return;

            // Parse different types of status messages to update statistics
            if (status.Contains("Downloaded file:") || 
                status.Contains("Downloaded HTML file:") || 
                status.Contains("Downloaded CSS file:") || 
                status.Contains("Downloaded CSS resource:") || 
                status.Contains("Downloaded resource:"))
            {
                IncrementDownloadedFiles();
            }
            else if (status.Contains("bytes"))
            {
                // Try to extract byte count from status messages like "Downloaded 1024 bytes"
                var matches = System.Text.RegularExpressions.Regex.Matches(status, @"(\d+)\s*bytes?");
                if (matches.Count > 0 && long.TryParse(matches[0].Groups[1].Value, out long bytes))
                {
                    UpdateBytesDownloaded(bytes);
                }
            }
            else if (status.Contains("Starting download") && status.Contains("threads"))
            {
                // Extract thread count from messages like "Starting download with 4 threads"
                var threadMatch = System.Text.RegularExpressions.Regex.Match(status, @"with\s+(\d+)\s+threads?");
                if (threadMatch.Success && int.TryParse(threadMatch.Groups[1].Value, out int threads))
                {
                    UpdateActiveThreads(threads);
                }
            }
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1073741824) return $"{bytes / 1048576.0:F1} MB";
            return $"{bytes / 1073741824.0:F2} GB";
        }

        private string FormatBytesPerSecond(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024) return $"{bytesPerSecond:F0} B/s";
            if (bytesPerSecond < 1048576) return $"{bytesPerSecond / 1024:F1} KB/s";
            if (bytesPerSecond < 1073741824) return $"{bytesPerSecond / 1048576:F1} MB/s";
            return $"{bytesPerSecond / 1073741824:F2} GB/s";
        }

        #endregion

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