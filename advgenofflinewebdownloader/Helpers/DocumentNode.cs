using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace advgenofflinewebdownloader.Helpers
{
    public class DocumentNode
    {
        private HtmlDocument _document;
        private string _id;

        public DocumentNode(HtmlDocument document)
        {
            _document = document;
        }

        public string Id
        {
            get => _id;
            set => _id = value;
        }

        public HtmlElement GetElementById(string id)
        {
            return _document.GetElementById(id);
        }
    }
}
