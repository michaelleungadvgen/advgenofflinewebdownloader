using advgenofflinewebdownloader.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace advgenofflinewebdownloader.Services
{
    public interface IMainPageService
    {
        WebsiteDTO Load(string path);
        bool Download();
    }
}
