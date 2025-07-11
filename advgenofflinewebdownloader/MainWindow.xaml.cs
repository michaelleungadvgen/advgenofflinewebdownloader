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
        public MainWindow(IMainPageService mainPageService,MainWindowViewModel mainWindowViewModel)
        {
            _mainPageService = mainPageService;
            _mainWindowView = mainWindowViewModel;
            this.InitializeComponent();
        }

        private void downloadButton_Click(object sender, RoutedEventArgs e)
        {
          //  myButton.Content = "Clicked";
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
                // Read file content
                string fileContent = await FileIO.ReadTextAsync(file);
                
                // Update UI or process the file
                txtName.Text = file.Name;
                txtURL.Text = fileContent;
                
                // Add to message list
                lstMessage.Items.Add($"Opened file: {file.Path}");
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
                // Create the content to save
                string contentToSave = $"URL: {txtURL.Text}\nThread Count: {numThreadCount.Value}";

                // Save the file
                await FileIO.WriteTextAsync(file, contentToSave);

                // Update message list
                lstMessage.Items.Add($"File saved: {file.Path}");
            }
        }
    }
}
