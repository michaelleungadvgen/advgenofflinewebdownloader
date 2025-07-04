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
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using advgenofflinewebdownloader.Helpers;
    public class FileDownloadService : IFileDownloadService
{
        private readonly HttpClient _httpClient;
        private readonly HashSet<string> _downloadedUrls;
        private CancellationTokenSource _cancellationTokenSource;
        private string _baseUrl;
        private string _hostName;
        private string _downloadFolder;
        private int _totalFiles;
        private int _downloadedFiles;
        private readonly object _lockObject = new object();

        public event EventHandler<DownloadProgressEventArgs> ProgressChanged;
        public event EventHandler<DownloadStatusEventArgs> StatusChanged;

        public FileDownloadService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _downloadedUrls = new HashSet<string>();
        }

        public async Task<DownloadResult> DownloadWebsiteAsync(string url, int depth, string baseFolder)
        {
            var stopwatch = Stopwatch.StartNew();
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    return new DownloadResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid URL format"
                    };
                }

                _baseUrl = url;
                _hostName = uri.Host;
                _downloadedUrls.Clear();
                _totalFiles = 0;
                _downloadedFiles = 0;

                // Create download folder
                _downloadFolder = Path.Combine(baseFolder, "Downloaded_Websites",
                    _hostName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(_downloadFolder);

                OnStatusChanged("Starting download...", false);

                await DownloadWebsiteRecursive(url, depth, "", _cancellationTokenSource.Token);

                stopwatch.Stop();

                return new DownloadResult
                {
                    Success = true,
                    DownloadFolder = _downloadFolder,
                    TotalFilesDownloaded = _downloadedFiles,
                    Duration = stopwatch.Elapsed
                };
            }
            catch (OperationCanceledException)
            {
                OnStatusChanged("Download cancelled", true);
                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = "Download was cancelled",
                    DownloadFolder = _downloadFolder,
                    TotalFilesDownloaded = _downloadedFiles
                };
            }
            catch (Exception ex)
            {
                OnStatusChanged($"Download failed: {ex.Message}", true);
                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    DownloadFolder = _downloadFolder,
                    TotalFilesDownloaded = _downloadedFiles
                };
            }
            finally
            {
                stopwatch.Stop();
                _cancellationTokenSource?.Dispose();
            }
        }

        public void CancelDownload()
        {
            _cancellationTokenSource?.Cancel();
        }

        private async Task DownloadWebsiteRecursive(string url, int depth, string relativePath, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            lock (_lockObject)
            {
                if (_downloadedUrls.Contains(url) || depth < 0)
                    return;
                _downloadedUrls.Add(url);
            }

            try
            {
                OnStatusChanged($"Downloading: {url}", false);

                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    OnStatusChanged($"Failed to download: {url} (Status: {response.StatusCode})", true);
                    return;
                }

                var content = await response.Content.ReadAsStringAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "";

                // Save the file
                var fileName = GetFileNameFromUrl(url);
                var filePath = Path.Combine(_downloadFolder, relativePath, fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                if (contentType.Contains("text/html"))
                {
                    // Process HTML content
                    var processedContent = await ProcessHtmlContent(content, url, depth, relativePath, cancellationToken);
                    await File.WriteAllTextAsync(filePath, processedContent, cancellationToken);
                }
                else
                {
                    // Save binary content
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);
                }

                IncrementProgress(fileName);
            }
            catch (HttpRequestException ex)
            {
                OnStatusChanged($"Network error downloading {url}: {ex.Message}", true);
            }
            catch (TaskCanceledException)
            {
                throw new OperationCanceledException();
            }
            catch (Exception ex)
            {     
                OnStatusChanged($"Error downloading {url}: {ex.Message}", true);
            }
        }

        private async Task<string> ProcessHtmlContent(string html, string currentUrl, int depth, string relativePath, CancellationToken cancellationToken)
        {
            var uri = new Uri(currentUrl);
            var baseUri = new Uri(uri.GetLeftPart(UriPartial.Authority));

            // Find all resources
            var resources = ExtractResources(html, currentUrl, baseUri);

            // Download resources
            var downloadTasks = new List<Task>();
            foreach (var resource in resources.Distinct())
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (ShouldDownload(resource.AbsoluteUrl, baseUri))
                {
                    if (resource.IsPage && depth > 0)
                    {
                        downloadTasks.Add(DownloadWebsiteRecursive(
                            resource.AbsoluteUrl,
                            depth - 1,
                            GetRelativePathForUrl(resource.AbsoluteUrl, baseUri),
                            cancellationToken));
                    }
                    else if (!resource.IsPage)
                    {
                        downloadTasks.Add(DownloadResource(
                            resource.AbsoluteUrl,
                            GetRelativePathForUrl(resource.AbsoluteUrl, baseUri),
                            cancellationToken));
                    }
                }
            }

            lock (_lockObject)
            {
                _totalFiles = Math.Max(_totalFiles, _downloadedUrls.Count + downloadTasks.Count);
            }

            await Task.WhenAll(downloadTasks);

            // Replace URLs in HTML
            foreach (var resource in resources)
            {
                if (ShouldDownload(resource.AbsoluteUrl, baseUri))
                {
                    var localPath = GetLocalPath(resource.AbsoluteUrl, baseUri, relativePath);
                    html = html.Replace(resource.OriginalUrl, localPath);
                }
            }

            return html;
        }

        private List<ResourceInfo> ExtractResources(string html, string currentUrl, Uri baseUri)
        {
            var resources = new List<ResourceInfo>();
            var resourcePatterns = new[]
            {
                (@"<link[^>]+href=[""']([^""']+)[""']", false),
                (@"<script[^>]+src=[""']([^""']+)[""']", false),
                (@"<img[^>]+src=[""']([^""']+)[""']", false),
                (@"<a[^>]+href=[""']([^""']+)[""']", true),
                (@"url\([""']?([^""')]+)[""']?\)", false),
                (@"<source[^>]+src=[""']([^""']+)[""']", false),
                (@"<video[^>]+src=[""']([^""']+)[""']", false),
                (@"<audio[^>]+src=[""']([^""']+)[""']", false),
                (@"<link[^>]+rel=[""']icon[""'][^>]+href=[""']([^""']+)[""']", false),
                (@"<link[^>]+href=[""']([^""']+)[""'][^>]+rel=[""']icon[""']", false)
            };

            foreach (var (pattern, isPagePattern) in resourcePatterns)
            {
                var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var resourceUrl = match.Groups[1].Value;
                        if (string.IsNullOrEmpty(resourceUrl) ||
                            resourceUrl.StartsWith("#") ||
                            resourceUrl.StartsWith("data:") ||
                            resourceUrl.StartsWith("mailto:") ||
                            resourceUrl.StartsWith("javascript:"))
                            continue;

                        var absoluteUrl = GetAbsoluteUrl(resourceUrl, currentUrl, baseUri);
                        var isPage = isPagePattern && !IsAssetFile(absoluteUrl);

                        resources.Add(new ResourceInfo
                        {
                            OriginalUrl = resourceUrl,
                            AbsoluteUrl = absoluteUrl,
                            IsPage = isPage
                        });
                    }
                }
            }

            return resources;
        }

        private async Task DownloadResource(string url, string relativePath, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            lock (_lockObject)
            {
                if (_downloadedUrls.Contains(url))
                    return;
                _downloadedUrls.Add(url);
            }

            try
            {
                OnStatusChanged($"Downloading resource: {Path.GetFileName(url)}", false);

                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return;

                var fileName = GetFileNameFromUrl(url);
                var filePath = Path.Combine(_downloadFolder, relativePath, fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                var bytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);

                IncrementProgress(fileName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error downloading resource {url}: {ex.Message}");
            }
        }

        private void IncrementProgress(string fileName)
        {
            lock (_lockObject)
            {
                _downloadedFiles++;
                var progress = _totalFiles > 0 ? (double)_downloadedFiles / _totalFiles : 0;

                ProgressChanged?.Invoke(this, new DownloadProgressEventArgs
                {
                    TotalFiles = _totalFiles,
                    DownloadedFiles = _downloadedFiles,
                    Progress = progress,
                    CurrentFile = fileName
                });
            }
        }

        private void OnStatusChanged(string status, bool isError)
        {
            StatusChanged?.Invoke(this, new DownloadStatusEventArgs
            {
                Status = status,
                IsError = isError
            });
        }

        private string GetAbsoluteUrl(string url, string currentUrl, Uri baseUri)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
                return absoluteUri.ToString();

            if (url.StartsWith("//"))
                return baseUri.Scheme + ":" + url;

            if (url.StartsWith("/"))
                return baseUri.ToString().TrimEnd('/') + url;

            var currentUri = new Uri(currentUrl);
            var currentBase = currentUri.ToString().Substring(0, currentUri.ToString().LastIndexOf('/') + 1);
            return currentBase + url;
        }

        private bool ShouldDownload(string url, Uri baseUri)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            // Only download from same domain or subdomains
            return uri.Host == baseUri.Host || uri.Host.EndsWith("." + baseUri.Host);
        }

     
        private bool IsAssetFile(string url)
        {
            var extensions = new[] {
                ".css", ".js", ".jpg", ".jpeg", ".png", ".gif", ".svg", ".ico",
                ".webp", ".woff", ".woff2", ".ttf", ".eot", ".otf", ".pdf",
                ".zip", ".rar", ".7z", ".tar", ".gz", ".mp4", ".mp3", ".wav",
                ".avi", ".mov", ".wmv", ".flv", ".webm"
            };
            var path = url.Split('?')[0].ToLower();
            return extensions.Any(ext => path.EndsWith(ext));
        }

        private string GetFileNameFromUrl(string url)
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;

            if (string.IsNullOrEmpty(path) || path == "/" || path.EndsWith("/"))
                return "index.html";

            var fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName) || !fileName.Contains('.'))
                return fileName + ".html";

            // Clean up filename
            fileName = Regex.Replace(fileName, @"[^\w\.-]", "_");
            return fileName;
        }

        private string GetRelativePathForUrl(string url, Uri baseUri)
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath.TrimStart('/');
            var directory = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "";
            return directory;
        }

        private string GetLocalPath(string absoluteUrl, Uri baseUri, string currentRelativePath)
        {
            var uri = new Uri(absoluteUrl);
            var path = uri.AbsolutePath.TrimStart('/');
            var fileName = GetFileNameFromUrl(absoluteUrl);
            var directory = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "";

            // Calculate relative path from current location
            var currentDepth = currentRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
            var targetDepth = directory.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;

            var relativePath = "";
            for (int i = 0; i < currentDepth; i++)
                relativePath += "../";

            if (!string.IsNullOrEmpty(directory))
                relativePath += directory + "/";

            return relativePath + fileName;
        }

        private class ResourceInfo
        {
            public string OriginalUrl { get; set; }
            public string AbsoluteUrl { get; set; }
            public bool IsPage { get; set; }
        }
    }  
}

