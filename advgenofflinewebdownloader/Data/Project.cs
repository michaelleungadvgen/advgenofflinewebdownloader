using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace advgenofflinewebdownloader.Data
{
    public class Project
    {
        public string Name { get; set; }
        public string URL { get; set; }
        public string DownloadPath { get; set; }
        public List<string> Logs { get; set; }
    }
}
