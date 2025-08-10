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
        private readonly HashSet<string> _processingUrls; // Track URLs currently being processed to prevent loops
        private CancellationTokenSource _cancellationTokenSource;
        private SemaphoreSlim _downloadSemaphore;
        private string _baseUrl;
        private string _hostName;
        private string _downloadFolder;
        private int _totalFiles;
        private int _downloadedFiles;
        private int _maxThreads;
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
            _processingUrls = new HashSet<string>();
        }

        public async Task<DownloadResult> DownloadWebsiteAsync(string url, int depth, string baseFolder, int maxThreads = 4)
        {
            var stopwatch = Stopwatch.StartNew();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Initialize threading support
            _maxThreads = Math.Max(1, Math.Min(maxThreads, 16)); // Clamp between 1 and 16
            _downloadSemaphore?.Dispose(); // Clean up previous semaphore
            _downloadSemaphore = new SemaphoreSlim(_maxThreads, _maxThreads);

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
                _processingUrls.Clear();
                _totalFiles = 0;
                _downloadedFiles = 0;

                // Create download folder
                _downloadFolder = Path.Combine(baseFolder, "Downloaded_Websites",
                    _hostName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(_downloadFolder);

                OnStatusChanged($"Download folder created: {_downloadFolder}", false);
                OnStatusChanged($"Starting download with {_maxThreads} concurrent threads...", false);

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
                _downloadSemaphore?.Dispose();
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
                {
                    OnStatusChanged($"URL already processed or max depth reached: {url}", false);
                    return;
                }
                
                if (_processingUrls.Contains(url))
                {
                    OnStatusChanged($"LOOP DETECTED: Skipping already processing URL: {url}", true);
                    return;
                }
                
                _downloadedUrls.Add(url);
                _processingUrls.Add(url);
                OnStatusChanged($"Starting to process: {url} (depth: {depth})", false);
            }

            try
            {
                OnStatusChanged($"Downloading: {url}", false);
                
                // Special debug for CSS files
                if (url.Contains(".css"))
                {
                    OnStatusChanged($"DOWNLOADING CSS FILE: {url}", false);
                }

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

                OnStatusChanged($"Processing file: {fileName}, Content-Type: '{contentType}', URL: {url}", false);

                // Debug CSS detection - Enhanced to catch more CSS files
                bool isCssContentType = contentType.Contains("text/css") || contentType.Contains("application/css");
                bool isCssFileName = fileName.EndsWith(".css", StringComparison.OrdinalIgnoreCase);
                bool isCssUrl = url.Contains(".css", StringComparison.OrdinalIgnoreCase);
                bool isCssContent = content.Contains("@font-face") || content.Contains("url(") || content.Contains("@import") || content.Contains("@charset") || content.Contains("background:") || content.Contains("background-image:");
                bool isTextPlainCss = contentType.Contains("text/plain") && (isCssFileName || isCssUrl || isCssContent);
                
                OnStatusChanged($"CSS Detection - ContentType: {isCssContentType}, FileName: {isCssFileName}, URL: {isCssUrl}, Content: {isCssContent}, PlainText: {isTextPlainCss}", false);

                if (contentType.Contains("text/html"))
                {
                    OnStatusChanged($"Processing HTML: {fileName}", false);
                    // Process HTML content
                    var processedContent = await ProcessHtmlContent(content, url, depth, relativePath, cancellationToken);
                    await File.WriteAllTextAsync(filePath, processedContent, cancellationToken);
                    OnStatusChanged($"Downloaded HTML file: {fileName} ({System.Text.Encoding.UTF8.GetByteCount(processedContent)} bytes)", false);
                }
                else if (isCssContentType || isCssFileName || isCssUrl || isCssContent || isTextPlainCss)
                {
                    string detectionMethod = isCssContentType ? "content-type" : 
                                           isCssFileName ? "filename" : 
                                           isCssUrl ? "url" : 
                                           isCssContent ? "content-analysis" : 
                                           "text-plain-css";
                    OnStatusChanged($"Processing CSS: {fileName} (detected via: {detectionMethod})", false);
                    // Process CSS content for font and resource references
                    var processedContent = await ProcessCssContent(content, url, relativePath, cancellationToken);
                    await File.WriteAllTextAsync(filePath, processedContent, cancellationToken);
                    OnStatusChanged($"Downloaded CSS file: {fileName} ({System.Text.Encoding.UTF8.GetByteCount(processedContent)} bytes)", false);
                }
                else
                {
                    OnStatusChanged($"Processing binary: {fileName} (not detected as CSS)", false);
                    // Save binary content
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);
                    OnStatusChanged($"Downloaded file: {fileName} ({bytes.Length} bytes)", false);
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
            finally
            {
                // Remove from processing set when done
                lock (_lockObject)
                {
                    _processingUrls.Remove(url);
                }
            }
        }

        private async Task<string> ProcessHtmlContent(string html, string currentUrl, int depth, string relativePath, CancellationToken cancellationToken)
        {
            var uri = new Uri(currentUrl);
            var baseUri = new Uri(uri.GetLeftPart(UriPartial.Authority));

            // Find all resources
            var resources = ExtractResources(html, currentUrl, baseUri);
            
            OnStatusChanged($"Found {resources.Count} total resources in HTML", false);
            
            // Debug CSS resources specifically
            var cssResources = resources.Where(r => r.AbsoluteUrl.Contains(".css")).ToList();
            OnStatusChanged($"Found {cssResources.Count} CSS resources in HTML", false);
            foreach (var cssRes in cssResources)
            {
                OnStatusChanged($"CSS resource found in HTML: {cssRes.AbsoluteUrl}", false);
            }

            // Download resources with thread control
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
                        downloadTasks.Add(DownloadWithSemaphore(() => DownloadWebsiteRecursive(
                            resource.AbsoluteUrl,
                            depth - 1,
                            GetRelativePathForUrl(resource.AbsoluteUrl, baseUri),
                            cancellationToken), cancellationToken));
                    }
                    else if (!resource.IsPage)
                    {
                        OnStatusChanged($"Downloading resource: {resource.AbsoluteUrl}", false);
                        downloadTasks.Add(DownloadWithSemaphore(() => DownloadResource(
                            resource.AbsoluteUrl,
                            GetRelativePathForUrl(resource.AbsoluteUrl, baseUri),
                            cancellationToken), cancellationToken));
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

            // Handle srcset attributes replacement specifically
            html = UpdateSrcsetAttributes(html, resources, baseUri, relativePath);

            return html;
        }

        private async Task<string> ProcessCssContent(string css, string currentUrl, string relativePath, CancellationToken cancellationToken)
        {
            var uri = new Uri(currentUrl);
            var baseUri = new Uri(uri.GetLeftPart(UriPartial.Authority));

            OnStatusChanged($"Processing CSS file: {currentUrl}", false);

            // Find all CSS resources (fonts, images, etc.)
            var resources = ExtractCssResources(css, currentUrl, baseUri);

            OnStatusChanged($"Found {resources.Count} resources in CSS", false);

            // Download resources (remove duplicates by absolute URL) with thread control
            var downloadTasks = new List<Task>();
            var uniqueResources = resources.GroupBy(r => r.AbsoluteUrl).Select(g => g.First()).ToList();
            
            foreach (var resource in uniqueResources)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (ShouldDownload(resource.AbsoluteUrl, baseUri))
                {
                    OnStatusChanged($"Downloading CSS resource: {resource.AbsoluteUrl}", false);
                    downloadTasks.Add(DownloadWithSemaphore(() => DownloadResource(
                        resource.AbsoluteUrl,
                        GetRelativePathForUrl(resource.AbsoluteUrl, baseUri),
                        cancellationToken), cancellationToken));
                }
            }

            lock (_lockObject)
            {
                _totalFiles = Math.Max(_totalFiles, _downloadedUrls.Count + downloadTasks.Count);
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
            
            OnStatusChanged($"Extracting CSS resources from {currentUrl}", false);
            OnStatusChanged($"CSS content preview (first 200 chars): {css.Substring(0, Math.Min(200, css.Length))}", false);
            
            // Always test with the specific example from the user to verify regex works
            var testString = "url(../webfonts/fa-brands-400.woff2) format(\"woff2\"),url(../webfonts/fa-brands-400.ttf)";
            OnStatusChanged($"Testing regex with example: {testString}", false);
            var testMatches = Regex.Matches(testString, @"url\s*\(\s*[""']?([^""')]+)[""']?\s*\)", RegexOptions.IgnoreCase);
            OnStatusChanged($"Test matches found: {testMatches.Count}", false);
            foreach (Match testMatch in testMatches)
            {
                OnStatusChanged($"Test match: '{testMatch.Groups[1].Value}'", false);
            }
            
            // Check if CSS contains any url() patterns at all
            if (css.Contains("url("))
            {
                OnStatusChanged($"CSS contains url() patterns", false);
            }
            else
            {
                OnStatusChanged($"CSS does NOT contain any url() patterns", false);
            }
            
            // Find all url() functions in CSS, including multiple URLs in a single declaration
            // Enhanced pattern to handle spaces, quotes, and various URL formats
            var urlPattern = @"url\s*\(\s*[""']?([^""')]+?)[""']?\s*\)";
            var matches = Regex.Matches(css, urlPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            
            OnStatusChanged($"Found {matches.Count} URL matches in CSS", false);
            
            // Show first few matches for debugging
            for (int i = 0; i < Math.Min(5, matches.Count); i++)
            {
                OnStatusChanged($"Match {i + 1}: '{matches[i].Groups[1].Value}'", false);
            }
            
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    var resourceUrl = match.Groups[1].Value.Trim();
                    if (string.IsNullOrEmpty(resourceUrl) ||
                        resourceUrl.StartsWith("#") ||
                        resourceUrl.StartsWith("data:") ||
                        resourceUrl.StartsWith("javascript:"))
                        continue;

                    // Clean up the URL - remove format(), hash, and query parameters
                    var cleanUrl = resourceUrl;
                    
                    // Remove format() and other CSS function calls
                    if (cleanUrl.Contains("format("))
                    {
                        cleanUrl = cleanUrl.Substring(0, cleanUrl.IndexOf("format(")).Trim();
                    }
                    
                    // Remove query parameters and hash for path resolution
                    var queryIndex = cleanUrl.IndexOf('?');
                    if (queryIndex > 0)
                    {
                        cleanUrl = cleanUrl.Substring(0, queryIndex);
                    }
                    
                    var hashIndex = cleanUrl.IndexOf('#');
                    if (hashIndex > 0)
                    {
                        cleanUrl = cleanUrl.Substring(0, hashIndex);
                    }
                    
                    // Remove any trailing whitespace or semicolons
                    cleanUrl = cleanUrl.Trim().TrimEnd(';', ',').Trim();

                    if (string.IsNullOrEmpty(cleanUrl))
                        continue;

                    var absoluteUrl = GetAbsoluteUrl(cleanUrl, currentUrl, baseUri);
                    
                    OnStatusChanged($"CSS resource found - Original: '{cleanUrl}', Current URL: {currentUrl}, Resolved: {absoluteUrl}", false);
                    
                    // Special debug for font files
                    if (cleanUrl.Contains("webfonts") || cleanUrl.Contains("woff") || cleanUrl.Contains("ttf"))
                    {
                        OnStatusChanged($"FONT FILE DETECTED: {cleanUrl} -> {absoluteUrl}", false);
                    }
                    
                    // Special debug for background images
                    if (cleanUrl.Contains(".jpg") || cleanUrl.Contains(".jpeg") || cleanUrl.Contains(".png") || cleanUrl.Contains(".gif") || cleanUrl.Contains(".webp") || cleanUrl.Contains(".svg"))
                    {
                        OnStatusChanged($"BACKGROUND IMAGE DETECTED: {cleanUrl} -> {absoluteUrl}", false);
                    }
                    
                    resources.Add(new ResourceInfo
                    {
                        OriginalUrl = cleanUrl,
                        AbsoluteUrl = absoluteUrl,
                        IsPage = false
                    });
                }
            }

            // Also find @import statements and background properties
            var additionalPatterns = new[]
            {
                (@"@import\s+[""']([^""']+)[""']", false),
                (@"@import\s+url\s*\(\s*[""']?([^""')]+)[""']?\s*\)", false),
                (@"background\s*:\s*[^;]*url\s*\(\s*[""']?([^""')]+?)[""']?\s*\)", false),
                (@"background-image\s*:\s*url\s*\(\s*[""']?([^""')]+?)[""']?\s*\)", false),
                (@"background-repeat\s*:\s*[^;]*url\s*\(\s*[""']?([^""')]+?)[""']?\s*\)", false),
                (@"background-position\s*:\s*[^;]*url\s*\(\s*[""']?([^""')]+?)[""']?\s*\)", false),
                (@"background-size\s*:\s*[^;]*url\s*\(\s*[""']?([^""')]+?)[""']?\s*\)", false),
                (@"content\s*:\s*url\s*\(\s*[""']?([^""')]+?)[""']?\s*\)", false),
                (@"list-style(?:-image)?\s*:\s*url\s*\(\s*[""']?([^""')]+?)[""']?\s*\)", false),
                (@"cursor\s*:\s*url\s*\(\s*[""']?([^""')]+?)[""']?\s*\)", false)
            };

            foreach (var (pattern, isPagePattern) in additionalPatterns)
            {
                var importMatches = Regex.Matches(css, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                
                // Debug specific patterns
                if (pattern.Contains("background") && importMatches.Count > 0)
                {
                    OnStatusChanged($"Found {importMatches.Count} background URL matches with pattern: {pattern}", false);
                }
                
                foreach (Match match in importMatches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var resourceUrl = match.Groups[1].Value.Trim();
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
                (@"<style[^>]*>.*?@import\s+[""']([^""']+)[""'].*?</style>", false),
                (@"@import\s+[""']([^""']+\.css[^""']*)[""']", false),
                (@"@import\s+url\([""']?([^""')]+\.css[^""']*)[""']?\)", false),
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
                (@"<link[^>]*rel=[""']preload[""'][^>]*href=[""']([^""']+\.(?:woff2?|ttf|eot|otf))[""']", false),
                (@"background(?:-image)?\s*:\s*url\([""']?([^""')]+)[""']?\)", false),
                (@"style=[""'][^""']*background(?:-image)?\s*:\s*url\([""']?([^""')]+)[""']?\)", false)
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
                        
                        // Force CSS files to be treated as resources, not pages, to prevent infinite loops
                        if (absoluteUrl.Contains(".css", StringComparison.OrdinalIgnoreCase))
                        {
                            isPage = false;
                            OnStatusChanged($"Forcing CSS file to be treated as resource: {absoluteUrl}", false);
                        }
                        if (absoluteUrl.Contains("woff"))
                        {
                            Console.WriteLine(absoluteUrl);
                        }
                        resources.Add(new ResourceInfo
                        {
                            OriginalUrl = resourceUrl,
                            AbsoluteUrl = absoluteUrl,
                            IsPage = isPage
                        });
                    }
                }
            }

            // Handle srcset attributes separately
            var srcsetResources = ExtractSrcsetResources(html, currentUrl, baseUri);
            resources.AddRange(srcsetResources);

            return resources;
        }

        private List<ResourceInfo> ExtractSrcsetResources(string html, string currentUrl, Uri baseUri)
        {
            var resources = new List<ResourceInfo>();
            
            // Find all img tags with srcset attributes
            var srcsetPattern = @"<img[^>]*srcset=[""']([^""']+)[""'][^>]*>";
            var matches = Regex.Matches(html, srcsetPattern, RegexOptions.IgnoreCase);
            
            OnStatusChanged($"Found {matches.Count} srcset attributes to process", false);
            
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    var srcsetValue = match.Groups[1].Value;
                    OnStatusChanged($"Processing srcset: {srcsetValue}", false);
                    
                    // Parse individual URLs from srcset
                    // srcset format: "url1 descriptor1, url2 descriptor2, ..."
                    var srcsetUrls = ParseSrcsetUrls(srcsetValue);
                    
                    foreach (var url in srcsetUrls)
                    {
                        if (string.IsNullOrEmpty(url) ||
                            url.StartsWith("#") ||
                            url.StartsWith("data:") ||
                            url.StartsWith("mailto:") ||
                            url.StartsWith("javascript:"))
                            continue;

                        var absoluteUrl = GetAbsoluteUrl(url, currentUrl, baseUri);
                        OnStatusChanged($"SRCSET IMAGE: {url} -> {absoluteUrl}", false);
                        
                        resources.Add(new ResourceInfo
                        {
                            OriginalUrl = url,
                            AbsoluteUrl = absoluteUrl,
                            IsPage = false
                        });
                    }
                }
            }
            
            return resources;
        }

        private List<string> ParseSrcsetUrls(string srcsetValue)
        {
            var urls = new List<string>();
            
            // Split by comma to get individual srcset entries
            var entries = srcsetValue.Split(',');
            
            foreach (var entry in entries)
            {
                // Each entry format: "url descriptor" where descriptor is like "813w" or "1.5x"
                var trimmed = entry.Trim();
                
                // Find the first space to separate URL from descriptor
                var spaceIndex = trimmed.IndexOf(' ');
                if (spaceIndex > 0)
                {
                    var url = trimmed.Substring(0, spaceIndex).Trim();
                    urls.Add(url);
                }
                else if (!string.IsNullOrEmpty(trimmed))
                {
                    // No descriptor, just URL
                    urls.Add(trimmed);
                }
            }
            
            return urls;
        }

        private string UpdateSrcsetAttributes(string html, List<ResourceInfo> resources, Uri baseUri, string relativePath)
        {
            // Find all srcset attributes and update them
            var srcsetPattern = @"(<img[^>]*srcset=[""'])([^""']+)([""'][^>]*>)";
            
            return Regex.Replace(html, srcsetPattern, (match) =>
            {
                var beforeSrcset = match.Groups[1].Value;
                var srcsetValue = match.Groups[2].Value;
                var afterSrcset = match.Groups[3].Value;
                
                OnStatusChanged($"Updating srcset: {srcsetValue}", false);
                
                // Update each URL in the srcset
                var updatedSrcset = UpdateSrcsetValue(srcsetValue, resources, baseUri, relativePath);
                
                OnStatusChanged($"Updated srcset to: {updatedSrcset}", false);
                
                return beforeSrcset + updatedSrcset + afterSrcset;
            }, RegexOptions.IgnoreCase);
        }

        private string UpdateSrcsetValue(string srcsetValue, List<ResourceInfo> resources, Uri baseUri, string relativePath)
        {
            var entries = srcsetValue.Split(',');
            var updatedEntries = new List<string>();
            
            foreach (var entry in entries)
            {
                var trimmed = entry.Trim();
                var spaceIndex = trimmed.IndexOf(' ');
                
                if (spaceIndex > 0)
                {
                    var url = trimmed.Substring(0, spaceIndex).Trim();
                    var descriptor = trimmed.Substring(spaceIndex).Trim();
                    
                    // Find the corresponding resource to get the local path
                    var resource = resources.FirstOrDefault(r => r.OriginalUrl == url);
                    if (resource != null && ShouldDownload(resource.AbsoluteUrl, baseUri))
                    {
                        var localPath = GetLocalPath(resource.AbsoluteUrl, baseUri, relativePath);
                        updatedEntries.Add($"{localPath} {descriptor}");
                    }
                    else
                    {
                        updatedEntries.Add(trimmed);
                    }
                }
                else if (!string.IsNullOrEmpty(trimmed))
                {
                    // No descriptor, just URL
                    var resource = resources.FirstOrDefault(r => r.OriginalUrl == trimmed);
                    if (resource != null && ShouldDownload(resource.AbsoluteUrl, baseUri))
                    {
                        var localPath = GetLocalPath(resource.AbsoluteUrl, baseUri, relativePath);
                        updatedEntries.Add(localPath);
                    }
                    else
                    {
                        updatedEntries.Add(trimmed);
                    }
                }
            }
            
            return string.Join(", ", updatedEntries);
        }

        private async Task DownloadWithSemaphore(Func<Task> downloadFunc, CancellationToken cancellationToken)
        {
            await _downloadSemaphore.WaitAsync(cancellationToken);
            try
            {
                await downloadFunc();
            }
            finally
            {
                _downloadSemaphore.Release();
            }
        }

        private async Task DownloadResource(string url, string relativePath, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            lock (_lockObject)
            {
                if (_downloadedUrls.Contains(url) || _processingUrls.Contains(url))
                {
                    if (_processingUrls.Contains(url))
                    {
                        OnStatusChanged($"RESOURCE LOOP DETECTED: Skipping already processing URL: {url}", true);
                    }
                    return;
                }
                _downloadedUrls.Add(url);
                _processingUrls.Add(url);
            }

            try
            {
                var fileName = GetFileNameFromUrl(url);
                OnStatusChanged($"Downloading resource: {fileName}", false);

                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    OnStatusChanged($"Failed to download resource: {url} (Status: {response.StatusCode})", true);
                    return;
                }

                var content = await response.Content.ReadAsStringAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "";

                // Check if this is a CSS file that needs processing
                var isCssFile = url.Contains(".css", StringComparison.OrdinalIgnoreCase) || 
                               contentType.Contains("text/css") || 
                               contentType.Contains("application/css") ||
                               content.Contains("@font-face") || content.Contains("url(") || content.Contains("@import");

                var filePath = Path.Combine(_downloadFolder, relativePath, fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                if (isCssFile)
                {
                    OnStatusChanged($"Processing CSS resource: {fileName}", false);
                    
                    // Process CSS content to download its resources
                    var processedContent = await ProcessCssContent(content, url, relativePath, cancellationToken);
                    await File.WriteAllTextAsync(filePath, processedContent, cancellationToken);
                    OnStatusChanged($"Downloaded CSS resource: {fileName} ({System.Text.Encoding.UTF8.GetByteCount(processedContent)} bytes)", false);
                }
                else
                {
                    // Save binary content for non-CSS resources
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);
                    OnStatusChanged($"Downloaded resource: {fileName} ({bytes.Length} bytes)", false);
                }

                IncrementProgress(fileName);
            }
            catch (Exception ex)
            {
                OnStatusChanged($"Error downloading resource {url}: {ex.Message}", true);
                Debug.WriteLine($"Error downloading resource {url}: {ex.Message}");
            }
            finally
            {
                // Remove from processing set when done
                lock (_lockObject)
                {
                    _processingUrls.Remove(url);
                }
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
            // If already absolute, return as-is
            if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
                return absoluteUri.ToString();

            // Handle protocol-relative URLs
            if (url.StartsWith("//"))
                return baseUri.Scheme + ":" + url;

            // Handle root-relative URLs
            if (url.StartsWith("/"))
                return baseUri.ToString().TrimEnd('/') + url;

            try
            {
                // Handle relative URLs properly using Uri constructor
                var currentUri = new Uri(currentUrl);
                var resolvedUri = new Uri(currentUri, url);
                return resolvedUri.ToString();
            }
            catch (UriFormatException)
            {
                // Fallback to old method if URI creation fails
                try
                {
                    var currentUri = new Uri(currentUrl);
                    var currentBase = currentUri.ToString().Substring(0, currentUri.ToString().LastIndexOf('/') + 1);
                    return currentBase + url;
                }
                catch
                {
                    // Last resort - return as-is
                    return url;
                }
            }
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

