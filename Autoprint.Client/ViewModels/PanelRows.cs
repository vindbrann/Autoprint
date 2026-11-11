using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Autoprint.Shared;

namespace Autoprint.Client.ViewModels
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }

    /// <summary>Une ligne d'imprimante du panneau (les vôtres, à ajouter, sur d'autres sites, ou dans le volet « autre site »).</summary>
    public class PrinterRow : ObservableObject
    {
        public PrinterRow(string name, string? sub, Imprimante? data, string? queueFullName, IPanelActions actions)
        {
            Name = name;
            Sub = sub ?? "";
            Data = data;
            QueueFullName = queueFullName;

            PickCommand = new RelayCommand(_ => actions.Pick(this), _ => !IsPendingRemoval);
            AddCommand = new RelayCommand(async _ => await actions.AddAsync(this), _ => actions.CanInstall && !IsBusy);
            RemoveCommand = new RelayCommand(_ => actions.Remove(this));
            UndoCommand = new RelayCommand(_ => actions.Undo(this));
        }

        public string Name { get; }
        public string Sub { get; }
        public bool HasSub => !string.IsNullOrWhiteSpace(Sub);
        public Imprimante? Data { get; }
        public string? QueueFullName { get; }
        public bool IsInstalled => QueueFullName != null;

        private bool _isDefault;
        public bool IsDefault { get => _isDefault; set => Set(ref _isDefault, value); }

        private bool _isPendingRemoval;
        public bool IsPendingRemoval
        {
            get => _isPendingRemoval;
            set { if (Set(ref _isPendingRemoval, value)) CommandManager.InvalidateRequerySuggested(); }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { if (Set(ref _isBusy, value)) CommandManager.InvalidateRequerySuggested(); }
        }

        private string? _errorText;
        public string? ErrorText
        {
            get => _errorText;
            set { if (Set(ref _errorText, value)) OnPropertyChanged(nameof(HasError)); }
        }
        public bool HasError => !string.IsNullOrEmpty(ErrorText);

        // Bref surlignage quand la ligne devient l'imprimante par défaut
        private bool _flash;
        public bool Flash { get => _flash; private set => Set(ref _flash, value); }

        public async void DoFlash()
        {
            Flash = true;
            await Task.Delay(1000);
            Flash = false;
        }

        public ICommand PickCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand RemoveCommand { get; }
        public ICommand UndoCommand { get; }
    }

    /// <summary>Un site (lieu) du volet « imprimer sur un autre site ».</summary>
    public class SiteRow
    {
        public SiteRow(Emplacement lieu, int count, Action<SiteRow> open)
        {
            Lieu = lieu;
            Count = count;
            OpenCommand = new RelayCommand(_ => open(this));
        }

        public Emplacement Lieu { get; }
        public int Count { get; }
        public string Name => Lieu.Nom;
        public string Sub => Count > 1 ? $"{Count} imprimantes" : "1 imprimante";
        public ICommand OpenCommand { get; }
    }

    public interface IPanelActions
    {
        bool CanInstall { get; }
        void Pick(PrinterRow row);
        Task AddAsync(PrinterRow row);
        void Remove(PrinterRow row);
        void Undo(PrinterRow row);
    }
}
