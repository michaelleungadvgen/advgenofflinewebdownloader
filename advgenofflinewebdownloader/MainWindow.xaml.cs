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
                
                lstMessage.Items.Add("Starting download...");
                
                var result = await _mainPageService.Download();
                
                if (result.Success)
                {
                    lstMessage.Items.Add($"Download completed: {result.TotalFilesDownloaded} files downloaded");
                    lstMessage.Items.Add($"Download folder: {result.DownloadFolder}");
                    lstMessage.Items.Add($"Duration: {result.Duration}");
                }
                else
                {
                    lstMessage.Items.Add($"Download failed: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                lstMessage.Items.Add($"Error: {ex.Message}");
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
    }
}
