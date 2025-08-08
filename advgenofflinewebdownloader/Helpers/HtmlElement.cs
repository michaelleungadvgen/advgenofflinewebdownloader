using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace advgenofflinewebdownloader.Helpers
{
    public class HtmlElement
    {
        private string _html;
        private string _id;

        public HtmlElement(string html, string id)
        {
            _html = html;
            _id = id;
        }

        public string Id => _id;

        public string Text => _html.Split('>').Last().Trim();

        public IEnumerable<HtmlElement> Children => _html.Split('<').Where(s => s.Contains("</")).Select(s => new HtmlElement(_html, s));
    }
}
