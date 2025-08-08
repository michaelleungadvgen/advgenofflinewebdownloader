using advgenofflinewebdownloader.Data;
using advgenofflinewebdownloader.Helpers;
using advgenofflinewebdownloader.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace advgenofflinewebdownloader.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public IMainPageService _mainPageService;

        public ICommand LoadProjectsCommand { get; set; }
        private ObservableCollection<Project> _projects;
        public ObservableCollection<Project> Projects
        {
            get => _projects;
            set
            {
                _projects = value;
                OnPropertyChanged();
            }
        }

        private Project _currentProject;
        public Project CurrentProject
        {
            get => _currentProject;
            set
            {
                _currentProject = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProjectName));
                OnPropertyChanged(nameof(ProjectURL));
                OnPropertyChanged(nameof(ProjectDownloadPath));
            }
        }

        public string ProjectName
        {
            get => _currentProject?.Name ?? "";
            set
            {
                if (_currentProject != null)
                {
                    _currentProject.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ProjectURL
        {
            get => _currentProject?.URL ?? "";
            set
            {
                if (_currentProject != null)
                {
                    _currentProject.URL = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ProjectDownloadPath
        {
            get => _currentProject?.DownloadPath ?? "";
            set
            {
                if (_currentProject != null)
                {
                    _currentProject.DownloadPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public void UpdateCurrentProject()
        {
            _mainPageService.SetCurrentProject(_currentProject);
        }

        public MainWindowViewModel(IMainPageService mainPageService)
        {
            _mainPageService = mainPageService;
            Projects = new ObservableCollection<Project>();
            LoadProjectsCommand = new RelayCommand(ExecuteLoadProjects);
            CurrentProject = new Project();
        }

        private void ExecuteLoadProjects(object parameter)
        {
            LoadProjects();
        }

        private void LoadProjects()
        {
            /*var projects = _mainPageService.GetProjects();
            Projects.Clear();
            foreach (var project in projects)
            {
                Projects.Add(project);
            }*/
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
}
