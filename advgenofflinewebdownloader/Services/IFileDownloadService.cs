using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace advgenofflinewebdownloader.Services
{
    public interface IFileDownloadService
    {
        Task DownloadFilesAsync(List<string> fileUrls, string downloadPath);
    }
}
