using advgenofflinewebdownloader.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace advgenofflinewebdownloader.Services
{

    using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
    using advgenofflinewebdownloader.Helpers;

    public class FileDownloadService : IFileDownloadService
{
    private readonly HttpClient _httpClient;
    private string[] subTypes = { "png", "css", "jpg", "html", "jpeg" };

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
                var response = await _httpClient.GetAsync(fileUrl);
                response.EnsureSuccessStatusCode();

                var contentStream = await response.Content.ReadAsStreamAsync();
                var fileName = Path.GetFileName(new Uri(fileUrl).LocalPath);

                if (IsSupportedType(fileName))
                {
                    var filePath = Path.Combine(downloadPath, fileName);
                    using (var fileStream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write))
                    {
                        contentStream.CopyTo(fileStream);
                    }
                    Console.WriteLine($"Downloaded {fileName} to {filePath}");
                }

                // If HTML, parse for additional files
                if (IsHtmlType(fileName) && response.Content.Headers.ContentType.MediaType == "text/html")
                {
                   /* var htmlDocument = new HtmlDocument();
                    htmlDocument.Load(contentStream);
                    ParseAndDownloadLinks(htmlDocument, fileUrl, downloadPath);*/
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to download {fileUrl}: {ex.Message}");
            }
        }
    }

    private bool IsSupportedType(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;

        var extension = Path.GetExtension(fileName).TrimStart('.').ToLower();
        return subTypes.Contains(extension);
    }

    private bool IsHtmlType(string fileName)
    {
        return string.Equals(Path.GetExtension(fileName), ".html", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Path.GetExtension(fileName), ".htm", StringComparison.OrdinalIgnoreCase);
    }

    private void ParseAndDownloadLinks(HtmlDocument document, string baseUrl, string downloadPath)
    {
     /*  var nodes = document.DocumentNode.SelectNodes("//a[@href]");
        
        if (nodes == null) return;

        var fileUrls = nodes
            .Where(node => node.Attributes["href"] != null)
            .Select(node =>
                new Uri(new Uri(baseUrl), node.Attributes["href"].Value).AbsoluteUri)
            .ToList();

        await DownloadFilesAsync(fileUrls, downloadPath);*/
    }
}



   
}
