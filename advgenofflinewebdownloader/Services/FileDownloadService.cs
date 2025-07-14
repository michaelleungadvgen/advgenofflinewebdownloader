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
            var handler = new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            
            _httpClient = new HttpClient(handler);
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
                else if (contentType.Contains("text/css") || fileName.EndsWith(".css"))
                {
                    // Process CSS content for font and resource references
                    var processedContent = await ProcessCssContent(content, url, relativePath, cancellationToken);
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
                        OnStatusChanged($"Recursively downloading page: {resource.AbsoluteUrl} (depth: {depth})", false);
                        downloadTasks.Add(DownloadWebsiteRecursive(
                            resource.AbsoluteUrl,
                            depth - 1,
                            GetRelativePathForUrl(resource.AbsoluteUrl, baseUri),
                            cancellationToken));
                    }
                    else if (!resource.IsPage)
                    {
                        OnStatusChanged($"Downloading resource: {resource.AbsoluteUrl}", false);
                        downloadTasks.Add(DownloadResource(
                            resource.AbsoluteUrl,
                            GetRelativePathForUrl(resource.AbsoluteUrl, baseUri),
                            cancellationToken));
                    }
                    else if (resource.IsPage && depth == 0)
                    {
                        OnStatusChanged($"Skipping page (max depth reached): {resource.AbsoluteUrl}", false);
                    }
                }
            }

            lock (_lockObject)
            {
                _totalFiles = Math.Max(_totalFiles, _downloadedUrls.Count + downloadTasks.Count);
            }

            await Task.WhenAll(downloadTasks);

            // Replace URLs in HTML - use more specific replacements to avoid corrupting HTML tags
            foreach (var resource in resources)
            {
                if (ShouldDownload(resource.AbsoluteUrl, baseUri))
                {
                    var localPath = GetLocalPath(resource.AbsoluteUrl, baseUri, relativePath);
                    
                    // Replace URLs in quotes to avoid corrupting HTML tags
                    var quotedOriginal = $"\"{resource.OriginalUrl}\"";
                    var quotedLocal = $"\"{localPath}\"";
                    html = html.Replace(quotedOriginal, quotedLocal);
                    
                    var singleQuotedOriginal = $"'{resource.OriginalUrl}'";
                    var singleQuotedLocal = $"'{localPath}'";
                    html = html.Replace(singleQuotedOriginal, singleQuotedLocal);
                    
                    // Replace URLs in CSS url() functions
                    var cssOriginal = $"url({resource.OriginalUrl})";
                    var cssLocal = $"url({localPath})";
                    html = html.Replace(cssOriginal, cssLocal);
                    
                    var cssQuotedOriginal = $"url('{resource.OriginalUrl}')";
                    var cssQuotedLocal = $"url('{localPath}')";
                    html = html.Replace(cssQuotedOriginal, cssQuotedLocal);
                    
                    var cssDoubleQuotedOriginal = $"url(\"{resource.OriginalUrl}\")";
                    var cssDoubleQuotedLocal = $"url(\"{localPath}\")";
                    html = html.Replace(cssDoubleQuotedOriginal, cssDoubleQuotedLocal);
                }
            }

            return html;
        }

        private async Task<string> ProcessCssContent(string css, string currentUrl, string relativePath, CancellationToken cancellationToken)
        {
            var uri = new Uri(currentUrl);
            var baseUri = new Uri(uri.GetLeftPart(UriPartial.Authority));

            // Find all CSS resources (fonts, images, etc.)
            var resources = ExtractCssResources(css, currentUrl, baseUri);

            // Download resources
            var downloadTasks = new List<Task>();
            foreach (var resource in resources.Distinct())
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (ShouldDownload(resource.AbsoluteUrl, baseUri))
                {
                    downloadTasks.Add(DownloadResource(
                        resource.AbsoluteUrl,
                        GetRelativePathForUrl(resource.AbsoluteUrl, baseUri),
                        cancellationToken));
                }
            }

            await Task.WhenAll(downloadTasks);

            // Replace URLs in CSS
            foreach (var resource in resources)
            {
                if (ShouldDownload(resource.AbsoluteUrl, baseUri))
                {
                    var localPath = GetLocalPath(resource.AbsoluteUrl, baseUri, relativePath);
                    
                    // Replace URLs in CSS url() functions
                    var cssOriginal = $"url({resource.OriginalUrl})";
                    var cssLocal = $"url({localPath})";
                    css = css.Replace(cssOriginal, cssLocal);
                    
                    var cssQuotedOriginal = $"url('{resource.OriginalUrl}')";
                    var cssQuotedLocal = $"url('{localPath}')";
                    css = css.Replace(cssQuotedOriginal, cssQuotedLocal);
                    
                    var cssDoubleQuotedOriginal = $"url(\"{resource.OriginalUrl}\")";
                    var cssDoubleQuotedLocal = $"url(\"{localPath}\")";
                    css = css.Replace(cssDoubleQuotedOriginal, cssDoubleQuotedLocal);
                }
            }

            return css;
        }

        private List<ResourceInfo> ExtractCssResources(string css, string currentUrl, Uri baseUri)
        {
            var resources = new List<ResourceInfo>();
            var cssPatterns = new[]
            {
                (@"url\([""']?([^""')]+)[""']?\)", false),
                (@"@import\s+[""']([^""']+)[""']", false),
                (@"@import\s+url\([""']?([^""')]+)[""']?\)", false)
            };

            foreach (var (pattern, isPagePattern) in cssPatterns)
            {
                var matches = Regex.Matches(css, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var resourceUrl = match.Groups[1].Value;
                        if (string.IsNullOrEmpty(resourceUrl) ||
                            resourceUrl.StartsWith("#") ||
                            resourceUrl.StartsWith("data:") ||
                            resourceUrl.StartsWith("javascript:"))
                            continue;

                        var absoluteUrl = GetAbsoluteUrl(resourceUrl, currentUrl, baseUri);
                        
                        resources.Add(new ResourceInfo
                        {
                            OriginalUrl = resourceUrl,
                            AbsoluteUrl = absoluteUrl,
                            IsPage = false
                        });
                    }
                }
            }

            return resources;
        }

        private List<ResourceInfo> ExtractResources(string html, string currentUrl, Uri baseUri)
        {
            var resources = new List<ResourceInfo>();
            var resourcePatterns = new[]
            {
                (@"<link[^>]*rel=[""']stylesheet[""'][^>]*href=[""']([^""']+)[""']", false),
                (@"<link[^>]*href=[""']([^""']+)[""'][^>]*rel=[""']stylesheet[""']", false),
                (@"<script[^>]*type=[""']text/javascript[""'][^>]*src=[""']([^""']+)[""']", false),
                (@"<script[^>]*src=[""']([^""']+)[""'][^>]*type=[""']text/javascript[""']", false),
                (@"<script[^>]*src=[""']([^""']+)[""']", false),
                (@"<img[^>]*src=[""']([^""']+)[""']", false),
                (@"<a[^>]*href=[""']([^""']+)[""']", true),
                (@"url\([""']?([^""')]+)[""']?\)", false),
                (@"<source[^>]*src=[""']([^""']+)[""']", false),
                (@"<video[^>]*src=[""']([^""']+)[""']", false),
                (@"<audio[^>]*src=[""']([^""']+)[""']", false),
                (@"<link[^>]*rel=[""']icon[""'][^>]*href=[""']([^""']+)[""']", false),
                (@"<link[^>]*href=[""']([^""']+)[""'][^>]*rel=[""']icon[""']", false),
                (@"@font-face[^}]*src:[^}]*url\([""']?([^""')]+)[""']?\)", false),
                (@"src:[^;]*url\([""']?([^""')]+\.(?:woff2?|ttf|eot|otf|svg))[""']?\)", false),
                (@"<link[^>]*rel=[""']preload[""'][^>]*href=[""']([^""']+\.(?:woff2?|ttf|eot|otf))[""']", false)
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
            bool isSameDomain = uri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase);
            bool isSubdomain = uri.Host.EndsWith("." + baseUri.Host, StringComparison.OrdinalIgnoreCase);
            
            bool shouldDownload = isSameDomain || isSubdomain;
            
            if (!shouldDownload)
            {
                OnStatusChanged($"Skipping external domain: {uri.Host} (base: {baseUri.Host})", false);
            }
            
            return shouldDownload;
        }

     
        private bool IsAssetFile(string url)
        {
            var extensions = new[] {
                ".css", ".js", ".jpg", ".jpeg", ".png", ".gif", ".svg", ".ico",
                ".webp", ".woff", ".woff2", ".ttf", ".eot", ".otf", ".pdf",
                ".zip", ".rar", ".7z", ".tar", ".gz", ".mp4", ".mp3", ".wav",
                ".avi", ".mov", ".wmv", ".flv", ".webm"
            };
            
            // Split URL to get path without query parameters
            var path = url.Split('?')[0].ToLower();
            
            // If it explicitly ends with a known asset extension, it's an asset
            if (extensions.Any(ext => path.EndsWith(ext)))
                return true;
            
            // If it explicitly ends with HTML extensions, it's a page
            if (path.EndsWith(".html") || path.EndsWith(".htm") || path.EndsWith(".php") || 
                path.EndsWith(".asp") || path.EndsWith(".aspx") || path.EndsWith(".jsp"))
                return false;
            
            // If no extension or ends with slash, likely a page
            if (path.EndsWith("/") || !path.Contains('.'))
                return false;
            
            // If URL has query parameters (like ?page_id=13), it's likely a page
            if (url.Contains('?'))
                return false;
            
            // If URL has fragments (like #section), it's likely a page
            if (url.Contains('#'))
                return false;
            
            // Default to asset if we can't determine
            return true;
        }

        private string GetFileNameFromUrl(string url)
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var query = uri.Query;

            // Handle root or directory paths
            if (string.IsNullOrEmpty(path) || path == "/" || path.EndsWith("/"))
            {
                if (!string.IsNullOrEmpty(query))
                {
                    // Convert query parameters to filename: ?page_id=13 -> page_id_13.html
                    var queryFileName = query.TrimStart('?')
                        .Replace("=", "_")
                        .Replace("&", "_")
                        .Replace("%20", "_")
                        .Replace("+", "_");
                    queryFileName = Regex.Replace(queryFileName, @"[^\w\.-]", "_");
                    return queryFileName + ".html";
                }
                return "index.html";
            }

            var fileName = Path.GetFileName(path);
            
            // Handle files without extensions
            if (string.IsNullOrEmpty(fileName) || !fileName.Contains('.'))
            {
                var baseFileName = string.IsNullOrEmpty(fileName) ? "index" : fileName;
                
                if (!string.IsNullOrEmpty(query))
                {
                    // Append query parameters: page.php?id=13 -> page_id_13.html
                    var queryPart = query.TrimStart('?')
                        .Replace("=", "_")
                        .Replace("&", "_")
                        .Replace("%20", "_")
                        .Replace("+", "_");
                    queryPart = Regex.Replace(queryPart, @"[^\w\.-]", "_");
                    return baseFileName + "_" + queryPart + ".html";
                }
                
                return baseFileName + ".html";
            }

            // Handle files with extensions
            if (!string.IsNullOrEmpty(query))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                
                // For HTML-like files, append query to name
                if (extension.ToLower() == ".html" || extension.ToLower() == ".htm" || 
                    extension.ToLower() == ".php" || extension.ToLower() == ".asp" || 
                    extension.ToLower() == ".aspx" || extension.ToLower() == ".jsp")
                {
                    var queryPart = query.TrimStart('?')
                        .Replace("=", "_")
                        .Replace("&", "_")
                        .Replace("%20", "_")
                        .Replace("+", "_");
                    queryPart = Regex.Replace(queryPart, @"[^\w\.-]", "_");
                    return nameWithoutExt + "_" + queryPart + ".html";
                }
            }

            // Clean up filename for assets
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

