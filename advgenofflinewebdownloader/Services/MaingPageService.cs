using advgenofflinewebdownloader.DTO;
using advgenofflinewebdownloader.Repo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace advgenofflinewebdownloader.Services
{
    public class MaingPageService : IMainPageService
    {
        public IProjectRepository projectRepository;
        public IFileDownloadService fileDownloadService;

        public MaingPageService(IProjectRepository projectRepository)
        {
            this.projectRepository = projectRepository;
        }

        public WebsiteDTO Load(string path)
        {
            return null;
        }
    }
}
