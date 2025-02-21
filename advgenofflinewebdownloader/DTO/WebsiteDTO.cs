using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace advgenofflinewebdownloader.DTO
{
    public class WebsiteDTO
    {
        public string URL { get; set; }
        public IList<string> FileUrls { get; set; }
        public string Content { get; set; }
    }
}
