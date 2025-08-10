using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using advgenofflinewebdownloader.Services;
using advgenofflinewebdownloader.Repo;
using advgenofflinewebdownloader.ViewModels;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace advgenofflinewebdownloader
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            _serviceProvider = ConfigureServices();
            var services = new ServiceCollection();
       
            services.AddSingleton<MainWindow>();
            services.AddTransient<MainWindowViewModel>();
            services.AddScoped<IFileDownloadService, FileDownloadService>();
            services.AddScoped<IProjectRepository,ProjectRepository>();
            services.AddScoped<IMainPageService, MaingPageService>();
            _serviceProvider = services.BuildServiceProvider();
        }
        private IServiceProvider _serviceProvider;
        private static IServiceProvider ConfigureServices()
        {
            var provider = new ServiceCollection()
              
                .BuildServiceProvider(true);

            return provider;
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                // call m_window from the service provider
                m_window = _serviceProvider.GetRequiredService<MainWindow>();

                m_window.Activate();
            }
            catch (Exception ex)
            {
                // Write error to a file for debugging
                string errorFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "AdvGenWebDownloader_Error.txt");
                System.IO.File.WriteAllText(errorFile, $"App startup error: {ex.Message}\n\nStack trace: {ex.StackTrace}\n\nTime: {DateTime.Now}");
                Environment.Exit(1);
            }
        }

        private Window m_window;
    }
}
