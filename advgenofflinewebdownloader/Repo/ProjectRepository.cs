using advgenofflinewebdownloader.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace advgenofflinewebdownloader.Repo
{
   public class ProjectRepository : IProjectRepository
   {
      public bool Save(string file, Project project)
      {
         // Save the project to a file
         using (StreamWriter writer = new StreamWriter(file))
         {
            string json = JsonConvert.SerializeObject(project);
            writer.Write(json);
         }
         return true;
      }

      public Project LoadProject(string file)
      {
         // Load the project from a file
         if (!File.Exists(file))
         {
            return new Project();
         }
         using (StreamReader reader = new StreamReader(file))
         {
            string json = reader.ReadToEnd();
            return JsonConvert.DeserializeObject<Project>(json);
         }
      }
   }
}
