using advgenofflinewebdownloader.Data;
using advgenofflinewebdownloader.DTO;
using advgenofflinewebdownloader.Repo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace advgenofflinewebdownloader.Services
{
    public class MaingPageService : IMainPageService
    {
        public IProjectRepository projectRepository;
        public IFileDownloadService fileDownloadService;
        public Project currentProject;

        public MaingPageService(IProjectRepository projectRepository, IFileDownloadService fileDownloadService)
        {
            this.projectRepository = projectRepository;
            this.fileDownloadService = fileDownloadService;
            
            // Subscribe to download service events to forward status messages
            this.fileDownloadService.StatusChanged += (sender, args) =>
            {
                if (currentProject != null)
                {
                    currentProject.Logs = currentProject.Logs ?? new List<string>();
                    currentProject.Logs.Add($"[{(args.IsError ? "ERROR" : "INFO")}] {args.Status}");
                }
            };
        }

        public async Task<DownloadResult> Download()
        {
            if (currentProject == null || string.IsNullOrEmpty(currentProject.URL))
            {
                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = "No project loaded or URL is empty"
                };
            }

            try
            {
                var downloadPath = !string.IsNullOrEmpty(currentProject.DownloadPath) 
                    ? currentProject.DownloadPath 
                    : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                var result = await fileDownloadService.DownloadWebsiteAsync(
                    currentProject.URL, 
                    depth: 2, 
                    downloadPath);

                if (result.Success)
                {
                    currentProject.Logs = currentProject.Logs ?? new List<string>();
                    currentProject.Logs.Add($"Download completed: {result.TotalFilesDownloaded} files in {result.Duration}");
                }

                return result;
            }
            catch (Exception ex)
            {
                return new DownloadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
   
        public async Task<WebsiteDTO> LoadProject(StorageFile file)
        {
            try
            {
                currentProject = projectRepository.LoadProject(file.Path);
                
                if (currentProject != null)
                {
                    return new WebsiteDTO
                    {
                        URL = currentProject.URL,
                        FileUrls = new List<string>(),
                        Content = currentProject.Name
                    };
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                // Log the actual error for debugging
                System.Diagnostics.Debug.WriteLine($"Error loading project: {ex.Message}");
                return null;
            }
        }

        public Project GetCurrentProject()
        {
            return currentProject;
        }

        public void SetCurrentProject(Project project)
        {
            currentProject = project;
        }

        public bool SaveProject(string filePath)
        {
            if (currentProject == null)
                return false;

            try
            {
                return projectRepository.Save(filePath, currentProject);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool SaveProjectAs(string filePath, Project project)
        {
            try
            {
                return projectRepository.Save(filePath, project);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
