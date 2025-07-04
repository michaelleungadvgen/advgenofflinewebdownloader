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

        public MainWindowViewModel(IMainPageService mainPageService)
        {
            _mainPageService = mainPageService;
            Projects = new ObservableCollection<Project>();
            LoadProjectsCommand = new RelayCommand(ExecuteLoadProjects);
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
