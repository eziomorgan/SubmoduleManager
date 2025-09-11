using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProductManager
{
    public class SubmoduleViewModel : INotifyPropertyChanged
    {
        private readonly GitSubmodule _model;
        private bool _isSelected; // for checkbox
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }
        public string Name => _model.Name;
        public string WorkingPath => _model.Path;

        public ObservableCollection<string> Branches { get; }

        private string _selectedBranch;
        public string SelectedBranch
        {
            get => _selectedBranch;
            set
            {
                if (_selectedBranch != value)
                {
                    _selectedBranch = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand PullCommand { get; }
        public ICommand CheckoutCommand { get; }
        public ICommand AddPopularBranchCommand { get; }

        public SubmoduleViewModel(GitSubmodule model, Action<string> addPopularBranch)
        {
            _model = model;
            Branches = new ObservableCollection<string>(_model.Branches ?? new string[0]);
            _selectedBranch = _model.CurrentBranch;

            RefreshCommand = new RelayCommand(_ => _ = RefreshAsync());
            PullCommand = new RelayCommand(_ => _ = PullAsync());
            CheckoutCommand = new RelayCommand(_ => _ = CheckoutAsync());
            AddPopularBranchCommand = new RelayCommand(_ => addPopularBranch?.Invoke(SelectedBranch));
        }

        public Task RefreshAsync()
        {
            return Task.Run(() =>
            {
                _model.RefreshBranches();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Branches.Clear();
                    foreach (var b in _model.Branches)
                        Branches.Add(b);
                    OnPropertyChanged(nameof(Branches));
                });
            });
        }

        public Task PullAsync() => Task.Run(() => _model.PullLatest());

        public Task CheckoutAsync()
        {
            if (string.IsNullOrEmpty(SelectedBranch))
                return Task.CompletedTask;

            return Task.Run(() =>
            {
                _model.CheckoutBranch(SelectedBranch);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _selectedBranch = SelectedBranch;
                    OnPropertyChanged(nameof(SelectedBranch));
                });
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
