using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProductManager
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<SubmoduleViewModel> Submodules { get; } = new();
        public ObservableCollection<string> Log { get; } = new();

        public ICommand BrowseFolderCommand { get; }
        public ICommand RefreshAllCommand { get; }
        public ICommand UpdateAllCommand { get; }
        public ICommand CheckoutAllCommand { get; }

        public ICommand SelectAllCommand { get; }
        public MainViewModel()
        {
            BrowseFolderCommand = new RelayCommand(_ => BrowseAndLoad());
            RefreshAllCommand = new RelayCommand(_ => RefreshAll());
            UpdateAllCommand = new RelayCommand(_ => UpdateAll());
            CheckoutAllCommand = new RelayCommand(_ => CheckoutAll());
            SelectAllCommand = new RelayCommand(_ => SelectAll());
        }

        private void BrowseAndLoad()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                CheckFileExists = false,
                FileName = "Select Folder",
                ValidateNames = false
            };

            if (dialog.ShowDialog() == true)
            {
                var folderPath = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    AppendLog($"Selected folder: {folderPath}");
                    LoadSubmodulesFromGitModules(folderPath);
                }
            }
        }

        private void RefreshAll()
        {
            foreach (var sm in Submodules.Where(x => x.IsSelected))
            {
                sm.RefreshCommand.Execute(null);
                AppendLog($"Refreshed {sm.Name}");
            }
        }


        private void UpdateAll()
        {
            foreach (var sm in Submodules.Where(x => x.IsSelected))
            {
                sm.PullCommand.Execute(null);
                AppendLog($"Updated {sm.Name}");
            }
        }


        private void CheckoutAll()
        {
            foreach (var sm in Submodules.Where(x => x.IsSelected))
            {
                sm.CheckoutCommand.Execute(null);
                AppendLog($"Checked out {sm.Name} to {sm.SelectedBranch}");
            }
        }

        private void SelectAll()
        {
           foreach (var sm in Submodules)
                sm.IsSelected = true;
        }
        private void LoadSubmodulesFromGitModules(string repoRoot)
        {
            Submodules.Clear();

            var gitRoot = Path.Combine(repoRoot, ".git");
            var modulesDir = Path.Combine(gitRoot, "modules");
            if (!Directory.Exists(gitRoot))
            {
                AppendLog("No .git folder found");
                return;
            }

            var nameToPath = ParseGitmodules(Path.Combine(repoRoot, ".gitmodules"));

            if (Directory.Exists(modulesDir))
            {
                foreach (var moduleGitDir in Directory.GetDirectories(modulesDir))
                {
                    var name = Path.GetFileName(moduleGitDir);

                    string currentBranch = "unknown";
                    var headFile = Path.Combine(moduleGitDir, "HEAD");
                    if (File.Exists(headFile))
                    {
                        var headContent = File.ReadAllText(headFile).Trim();
                        if (headContent.StartsWith("ref:"))
                            currentBranch = headContent.Substring(headContent.LastIndexOf('/') + 1);
                        else
                            currentBranch = headContent;
                    }

                    nameToPath.TryGetValue(name, out var workingPathRel);
                    var workingPath = string.IsNullOrEmpty(workingPathRel) ? string.Empty : Path.GetFullPath(Path.Combine(repoRoot, workingPathRel));

                    var model = new GitSubmodule(AppendLog)
                    {
                        Name = name,
                        Path = workingPath,
                        GitDir = moduleGitDir,
                        CurrentBranch = currentBranch
                    };

                    model.RefreshBranches();
                    Submodules.Add(new SubmoduleViewModel(model));
                    AppendLog($"Loaded submodule {name} at {workingPath}");
                }
            }
        }

        private static Dictionary<string, string> ParseGitmodules(string gitmodulesPath)
        {
            var map = new Dictionary<string, string>();
            if (!File.Exists(gitmodulesPath)) return map;

            string currentName = null;
            foreach (var raw in File.ReadAllLines(gitmodulesPath))
            {
                var line = raw.Trim();
                if (line.StartsWith("[submodule \""))
                {
                    var start = line.IndexOf('"') + 1;
                    var end = line.LastIndexOf('"');
                    currentName = end > start ? line.Substring(start, end - start) : null;
                }
                else if (currentName != null && line.StartsWith("path"))
                {
                    var idx = line.IndexOf('=');
                    if (idx > 0)
                    {
                        var path = line.Substring(idx + 1).Trim();
                        map[currentName] = path;
                    }
                }
            }
            return map;
        }

        public void AppendLog(string message)
        {
            Log.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            OnPropertyChanged(nameof(Log));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
