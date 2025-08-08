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
         try
         {
            // Load the project from a file
            if (!File.Exists(file))
            {
               System.Diagnostics.Debug.WriteLine($"Project file not found: {file}");
               return new Project();
            }
            
            using (StreamReader reader = new StreamReader(file))
            {
               string json = reader.ReadToEnd();
               System.Diagnostics.Debug.WriteLine($"Loading project JSON: {json}");
               
               if (string.IsNullOrWhiteSpace(json))
               {
                  System.Diagnostics.Debug.WriteLine("Empty JSON content");
                  return new Project();
               }
               
               var project = JsonConvert.DeserializeObject<Project>(json);
               System.Diagnostics.Debug.WriteLine($"Loaded project - Name: '{project?.Name}', URL: '{project?.URL}'");
               return project ?? new Project();
            }
         }
         catch (JsonException ex)
         {
            System.Diagnostics.Debug.WriteLine($"JSON deserialization error: {ex.Message}");
            return new Project();
         }
         catch (Exception ex)
         {
            System.Diagnostics.Debug.WriteLine($"Error loading project file: {ex.Message}");
            return new Project();
         }
      }
   }
}
