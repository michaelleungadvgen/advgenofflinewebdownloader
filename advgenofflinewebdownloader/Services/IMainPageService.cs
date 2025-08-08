using advgenofflinewebdownloader.Data;
using advgenofflinewebdownloader.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace advgenofflinewebdownloader.Services
{
    public interface IMainPageService
    {
        Task<WebsiteDTO> LoadProject(StorageFile file);
        Task<DownloadResult> Download(int maxThreads = 4);
        Project GetCurrentProject();
        void SetCurrentProject(Project project);
        bool SaveProject(string filePath);
        bool SaveProjectAs(string filePath, Project project);

    }
}
