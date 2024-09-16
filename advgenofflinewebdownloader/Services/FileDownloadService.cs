using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace advgenofflinewebdownloader.services
{
    public class FileDownloadService
    {
        private readonly HttpClient _httpClient;

        public FileDownloadService()
        {
            _httpClient = new HttpClient();
        }

        public async Task DownloadFilesAsync(List<string> fileUrls, string downloadPath)
        {
            if (!Directory.Exists(downloadPath))
            {
                Directory.CreateDirectory(downloadPath);
            }

            foreach (var fileUrl in fileUrls)
            {
                try
                {
                    var fileBytes = await _httpClient.GetByteArrayAsync(fileUrl);
                    var fileName = Path.GetFileName(new Uri(fileUrl).LocalPath);
                    var filePath = Path.Combine(downloadPath, fileName);

                    await File.WriteAllBytesAsync(filePath, fileBytes);
                    Console.WriteLine($"Downloaded {fileName} to {filePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to download {fileUrl}: {ex.Message}");
                }
            }
        }
    }
}
