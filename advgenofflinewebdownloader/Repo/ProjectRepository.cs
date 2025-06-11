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
    public class ProjectRepository :IProjectRepository
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
        
            /* for IProjectDataStore and JsonFileBased interface.生成C#代码表示IJsonFileBased项目的实现：public class JsonFileBased : IJsonFileBased
    {
       public void Save( List<Project> projects)
       {
          var json = JsonConvert.SerializeObject(projects);
          File.WriteAllText(GetFilePath(), json);  
       }  
    public string GetFilePath()
       {
         return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects.json");
       }  
    //其他用于加载，更新和删除项目方法作为所需的任何方法}  
    下面是IProjectDataStore接口的完整代码表示： public interface IProjectDataStore
    {
       void Save( List<Project> projects);
       //other methods for loading, updating and deleting projects as needed
    }  
    这是IJsonFileBased项目的实现。该类实现了IProjectDataStore接口，并提供了Save()和LoadProjects()方法来保存和加载从JSON文件中读取的项目。public class IProjectDataStore: JsonFileBased,IProjectDataStore
    {
       public void Save( List<Project> projects)
       {
          var json = JsonConvert.SerializeObject(projects);
          File.WriteAllText(GetFilePath(), json);  
       }  
    public string GetFilePath()
       {
         return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects.json");
       }  
    //其他用于加载，更新和删除项目方法作为所需的任何方法}  
    生成C#代码表示IJsonFileBased项目的实现：public class JsonFileBased : IJsonFileBased
    {
       public void Save( List<Project> projects)
       {
          var json = JsonConvert.SerializeObject(projects);
          File.WriteAllText(GetFilePath(), json);  
       }  
    public string GetFilePath()
       {
         return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Projects.json");
       }  
    //其他用于加载，更新和删除项目方法作为所需的任何方法}  
    上面的代码实现了IProjectDataStore接口，并使用JsonFileBased类来保存和加载项目。*/
        }
}
