using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace advgenofflinewebdownloader.Services
{
    public interface IFileDownloadService
    {
        event EventHandler<DownloadProgressEventArgs> ProgressChanged;
        event EventHandler<DownloadStatusEventArgs> StatusChanged;
        Task DownloadFilesAsync(List<string> fileUrls, string downloadPath);
    }
    public class DownloadProgressEventArgs : EventArgs
    {
        public int TotalFiles { get; set; }
        public int DownloadedFiles { get; set; }
        public double Progress { get; set; }
        public string CurrentFile { get; set; }
    }

    public class DownloadStatusEventArgs : EventArgs
    {
        public string Status { get; set; }
        public bool IsError { get; set; }
    }

    public class DownloadResult
    {
        public bool Success { get; set; }
        public string DownloadFolder { get; set; }
        public int TotalFilesDownloaded { get; set; }
        public string ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
