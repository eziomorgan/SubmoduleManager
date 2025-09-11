using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
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

        public SubmoduleViewModel(GitSubmodule model)
        {
            _model = model;
            Branches = new ObservableCollection<string>(_model.Branches ?? new string[0]);
            _selectedBranch = _model.CurrentBranch;

            RefreshCommand = new RelayCommand(_ =>
            {
                _model.RefreshBranches();
                Branches.Clear();
                foreach (var b in _model.Branches)
                    Branches.Add(b);
                OnPropertyChanged(nameof(Branches));
            });

            PullCommand = new RelayCommand(_ => _model.PullLatest());

            CheckoutCommand = new RelayCommand(_ =>
            {

                if (!string.IsNullOrEmpty(SelectedBranch))
                {
                    _model.CheckoutBranch(SelectedBranch);

                    // After checkout, update model + notify
                    _selectedBranch = SelectedBranch;
                    OnPropertyChanged(nameof(SelectedBranch));
                }
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
