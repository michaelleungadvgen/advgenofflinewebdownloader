using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace advgenofflinewebdownloader.Helpers
{
    public class HtmlDocument
    {
        private string _html;

        public HtmlDocument(string html)
        {
            _html = html;
        }

        public void Load(string filePath)
        {
            using (var reader = new StreamReader(filePath))
            {
                _html = reader.ReadToEnd();
            }
        }

        public void Save(string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.Write(_html);
            }
        }

        public HtmlElement GetElementById(string id)
        {
            var element = _html.Split('<').Where(s => s.Contains($"id=\"{id}\"")).FirstOrDefault();
            if (element == null)
                return null;

            return new HtmlElement(_html, element);
        }
    }
}
