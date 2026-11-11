using Autoprint.Client.Services;
using Autoprint.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Autoprint.Client.ViewModels
{
    /// <summary>
    /// Panneau unique du client (B81) : vos imprimantes du site, celles à ajouter, celles des autres sites,
    /// et deux volets par-dessus (réglages, imprimer sur un autre site). Aucune popup pour les actions du panneau.
    /// </summary>
    public class MainWindowViewModel : ObservableObject, IPanelActions
    {
        private const int ShortList = 6; // au-delà, la liste à ajouter est repliée et se filtre
        private static readonly StringComparer FrSort = StringComparer.Create(new CultureInfo("fr-FR"), true);

        private readonly UserPreferencesService _prefService;
        private readonly ConfigurationService _configService;

        private List<Emplacement> _currentLieux = new();
        private List<Emplacement> _allLieux = new();
        private List<Imprimante> _printers = new();
        private Dictionary<string, string> _queues = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _pendingRemoval = new(StringComparer.OrdinalIgnoreCase);
        private List<PrinterRow> _addAll = new();
        private bool _loaded;

        private readonly Func<Dictionary<string, string>> _readQueues;

        /// <param name="readQueues">Lecture des imprimantes réseau installées (par défaut : le spouleur de Windows).</param>
        public MainWindowViewModel(UserPreferencesService prefService, ConfigurationService configService, Func<Dictionary<string, string>>? readQueues = null)
        {
            _prefService = prefService;
            _configService = configService;
            _readQueues = readQueues ?? PrinterService.GetNetworkQueues;
            Options = new OptionsViewModel(prefService, configService);

            OpenSettingsCommand = new RelayCommand(_ => { IsSettingsOpen = true; });
            CloseSettingsCommand = new RelayCommand(_ => { IsSettingsOpen = false; Options.ResetAdminPanel(); });
            OpenElsewhereCommand = new RelayCommand(_ => OpenElsewhere());
            CloseElsewhereCommand = new RelayCommand(_ => CloseElsewhere());
            BackToSitesCommand = new RelayCommand(_ => { PickedSite = null; PrinterQuery = ""; });
            OpenAddCommand = new RelayCommand(_ => IsAddOpen = true);
            CloseAddCommand = new RelayCommand(_ => { IsAddOpen = false; AddQuery = ""; });
            ToggleOthersCommand = new RelayCommand(_ => IsOthersOpen = !IsOthersOpen);
            RemoveAllOthersCommand = new RelayCommand(_ => { foreach (var r in OtherPrinters.Where(r => !r.IsPendingRemoval).ToList()) Remove(r); });
            ClearQueryCommand = new RelayCommand(p =>
            {
                switch (p as string)
                {
                    case "add": AddQuery = ""; break;
                    case "site": SiteQuery = ""; break;
                    case "printer": PrinterQuery = ""; break;
                }
            });
            OpenWindowsPrintersCommand = new RelayCommand(_ => OpenWindowsPrinters());
        }

        public OptionsViewModel Options { get; }

        public ObservableCollection<PrinterRow> MyPrinters { get; } = new();
        public ObservableCollection<PrinterRow> AddPrinters { get; } = new();
        public ObservableCollection<PrinterRow> OtherPrinters { get; } = new();
        public ObservableCollection<SiteRow> Sites { get; } = new();
        public ObservableCollection<PrinterRow> SitePrinters { get; } = new();

        // ---------- En-tête ----------
        private bool _isOffline;
        public bool IsOffline
        {
            get => _isOffline;
            private set { if (Set(ref _isOffline, value)) RaiseState(); }
        }

        private bool _isSmbAvailable = true;
        public bool IsSmbAvailable
        {
            get => _isSmbAvailable;
            private set { if (Set(ref _isSmbAvailable, value)) RaiseState(); }
        }

        public bool CanInstall => !IsOffline && IsSmbAvailable;
        public bool HasSite => _currentLieux.Count > 0;
        public string HeaderKicker => !_loaded ? "Autoprint" : IsOffline ? "Serveur injoignable" : HasSite ? "Vous êtes à" : "Hors des sites";
        public string SiteName => !_loaded ? "Chargement…" : HasSite ? string.Join(" + ", _currentLieux.Select(l => l.Nom)) : "Télétravail ou réseau inconnu";
        public bool ShowNote => _loaded && !CanInstall;
        public string NoteTitle => "Vos imprimantes fonctionnent.";
        public string NoteText => IsOffline
            ? "Pour en ajouter, connectez-vous au réseau de l'entreprise ou au VPN."
            : "Le serveur d'impression ne répond pas : l'ajout est indisponible pour le moment.";
        public string AddDisabledTip => CanInstall ? "" : "Serveur injoignable";

        // ---------- Vos imprimantes ----------
        public bool HasMyPrinters => MyPrinters.Count > 0;
        public bool ShowMySection => _loaded && HasSite;
        public bool ShowNoPrinterHint => ShowMySection && !HasMyPrinters;
        public bool ShowDefaultHint => ShowMySection && HasMyPrinters && !MyPrinters.Any(r => r.IsDefault);

        // ---------- Ajouter sur ce site ----------
        public int AddCount => _addAll.Count;
        private bool IsAddLong => AddCount > ShortList;
        public bool ShowAddSimple => HasSite && AddCount > 0 && !IsAddLong;
        public bool ShowAddCollapsed => HasSite && IsAddLong && !IsAddOpen;
        public bool ShowAddOpen => HasSite && IsAddLong && IsAddOpen;
        public bool ShowAddNoMatch => ShowAddOpen && AddPrinters.Count == 0;

        private bool _isAddOpen;
        public bool IsAddOpen { get => _isAddOpen; set { if (Set(ref _isAddOpen, value)) RaiseState(); } }

        private string _addQuery = "";
        public string AddQuery { get => _addQuery; set { if (Set(ref _addQuery, value ?? "")) { ApplyAddFilter(); RaiseState(); } } }

        // ---------- Vos imprimantes sur d'autres sites ----------
        public bool HasOthers => OtherPrinters.Count > 0;
        public int OtherCount => OtherPrinters.Count;

        private bool _isOthersOpen;
        public bool IsOthersOpen { get => _isOthersOpen; set { if (Set(ref _isOthersOpen, value)) RaiseState(); } }

        public bool ShowTeleworkHint => _loaded && !HasSite;
        public bool ShowElsewhereLink => _loaded && HasSite;

        // ---------- Volets ----------
        private bool _isSettingsOpen;
        public bool IsSettingsOpen { get => _isSettingsOpen; set { if (Set(ref _isSettingsOpen, value)) RaiseState(); } }

        private bool _isElsewhereOpen;
        public bool IsElsewhereOpen { get => _isElsewhereOpen; set { if (Set(ref _isElsewhereOpen, value)) RaiseState(); } }

        private SiteRow? _pickedSite;
        public SiteRow? PickedSite
        {
            get => _pickedSite;
            set { if (Set(ref _pickedSite, value)) { BuildSitePrinters(); RaiseState(); } }
        }

        public bool IsSiteStep => IsElsewhereOpen && PickedSite == null;
        public bool IsPrinterStep => IsElsewhereOpen && PickedSite != null;
        public string ElsewhereTitle => PickedSite?.Name ?? "Choisir un site";
        public bool ShowPrinterFilter => PickedSite != null && PickedSite.Count > ShortList;
        public bool ShowSitesNoMatch => IsSiteStep && Sites.Count == 0;
        public bool ShowSitePrintersNoMatch => IsPrinterStep && SitePrinters.Count == 0;

        private string _siteQuery = "";
        public string SiteQuery { get => _siteQuery; set { if (Set(ref _siteQuery, value ?? "")) { BuildSites(); RaiseState(); } } }

        private string _printerQuery = "";
        public string PrinterQuery { get => _printerQuery; set { if (Set(ref _printerQuery, value ?? "")) { BuildSitePrinters(); RaiseState(); } } }

        public ICommand OpenSettingsCommand { get; }
        public ICommand CloseSettingsCommand { get; }
        public ICommand OpenElsewhereCommand { get; }
        public ICommand CloseElsewhereCommand { get; }
        public ICommand BackToSitesCommand { get; }
        public ICommand OpenAddCommand { get; }
        public ICommand CloseAddCommand { get; }
        public ICommand ToggleOthersCommand { get; }
        public ICommand RemoveAllOthersCommand { get; }
        public ICommand ClearQueryCommand { get; }
        public ICommand OpenWindowsPrintersCommand { get; }

        /// <summary>Code du lieu actuel : clé de l'imprimante par défaut dans les préférences.</summary>
        public string? CurrentCode => _currentLieux.FirstOrDefault()?.Code;

        // ================= Chargement =================

        public void Load(List<Emplacement> currentLieux, List<Emplacement> allLieux, List<Imprimante> printers, bool isOffline)
        {
            _currentLieux = currentLieux;
            _allLieux = allLieux;
            _printers = printers;
            _loaded = true;
            IsOffline = isOffline;

            string server = _configService.PrintServerName ?? "";
            if (!isOffline && !string.IsNullOrEmpty(server)) _ = CheckSmbAccessAsync(server);

            Rebuild();
        }

        /// <summary>Relit le spouleur et reconstruit toutes les listes (les retraits en attente sont conservés).</summary>
        public void Rebuild()
        {
            _queues = _readQueues();
            var currentIds = _currentLieux.Select(l => l.Id).ToHashSet();
            string? fav = Preferred(CurrentCode);

            MyPrinters.Clear();
            var mine = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in _printers.Where(p => currentIds.Contains(p.EmplacementId) && _queues.ContainsKey(p.NomAffiche)).OrderBy(p => p.NomAffiche, FrSort))
            {
                string full = _queues[p.NomAffiche];
                var row = new PrinterRow(p.NomAffiche, p.Localisation, p, full, this) { IsPendingRemoval = _pendingRemoval.Contains(full) };
                row.IsDefault = !row.IsPendingRemoval && string.Equals(fav, p.NomAffiche, StringComparison.OrdinalIgnoreCase);
                MyPrinters.Add(row);
                mine.Add(p.NomAffiche);
            }

            _addAll = _printers.Where(p => currentIds.Contains(p.EmplacementId) && !_queues.ContainsKey(p.NomAffiche))
                               .OrderBy(p => p.NomAffiche, FrSort)
                               .Select(p => new PrinterRow(p.NomAffiche, p.Localisation, p, null, this))
                               .ToList();
            ApplyAddFilter();

            // Toutes les autres imprimantes réseau installées sur le poste, pour pouvoir faire le ménage
            OtherPrinters.Clear();
            foreach (var q in _queues.Where(q => !mine.Contains(q.Key)).OrderBy(q => q.Key, FrSort))
            {
                var p = _printers.FirstOrDefault(x => string.Equals(x.NomAffiche, q.Key, StringComparison.OrdinalIgnoreCase));
                var lieu = p == null ? null : _allLieux.FirstOrDefault(l => l.Id == p.EmplacementId);
                string sub = lieu == null ? q.Value : lieu.Nom + (string.Equals(Preferred(lieu.Code), q.Key, StringComparison.OrdinalIgnoreCase) ? " · par défaut sur ce site" : "");
                OtherPrinters.Add(new PrinterRow(p?.NomAffiche ?? q.Key, sub, p, q.Value, this) { IsPendingRemoval = _pendingRemoval.Contains(q.Value) });
            }

            if (IsElsewhereOpen) { BuildSites(); BuildSitePrinters(); }
            RaiseState();
        }

        private void ApplyAddFilter()
        {
            string q = Norm(AddQuery.Trim());
            AddPrinters.Clear();
            foreach (var r in _addAll.Where(r => q.Length == 0 || Matches(r.Data, q))) AddPrinters.Add(r);
        }

        private void BuildSites()
        {
            Sites.Clear();
            if (!IsElsewhereOpen) return;
            var currentIds = _currentLieux.Select(l => l.Id).ToHashSet();
            string q = Norm(SiteQuery.Trim());
            foreach (var lieu in _allLieux.Where(l => !currentIds.Contains(l.Id)).OrderBy(l => l.Nom, FrSort))
            {
                int count = _printers.Count(p => p.EmplacementId == lieu.Id);
                if (count == 0 || (q.Length > 0 && !Norm(lieu.Nom).Contains(q))) continue;
                Sites.Add(new SiteRow(lieu, count, s => PickedSite = s));
            }
        }

        private void BuildSitePrinters()
        {
            SitePrinters.Clear();
            if (PickedSite == null) return;
            string q = Norm(PrinterQuery.Trim());
            foreach (var p in _printers.Where(p => p.EmplacementId == PickedSite.Lieu.Id && (q.Length == 0 || Matches(p, q))).OrderBy(p => p.NomAffiche, FrSort))
            {
                _queues.TryGetValue(p.NomAffiche, out string? full);
                SitePrinters.Add(new PrinterRow(p.NomAffiche, p.Localisation, p, full, this));
            }
        }

        // « siege » trouve « Siège » : comparaison sans accents ni majuscules, sur le nom, l'emplacement et le code inventaire
        private static bool Matches(Imprimante? p, string q)
            => p != null && Norm($"{p.NomAffiche} {p.Localisation} {p.Code}").Contains(q);

        private static string Norm(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s.Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString().ToLowerInvariant();
        }

        private string? Preferred(string? code)
            => !string.IsNullOrEmpty(code) && _prefService.Current.PreferredPrinters.TryGetValue(code, out var v) ? v : null;

        // ================= Actions des lignes =================

        public void Pick(PrinterRow row)
        {
            string? code = CurrentCode;
            if (row.IsPendingRemoval || row.IsDefault || string.IsNullOrEmpty(code) || row.QueueFullName == null) return;

            _prefService.SetPreferredPrinter(code, row.Name);
            PrinterService.SetDefault(row.QueueFullName); // choix explicite : toujours appliqué à Windows
            foreach (var r in MyPrinters) r.IsDefault = r == row;
            row.DoFlash();
            RaiseState();
        }

        public async Task AddAsync(PrinterRow row)
        {
            if (!CanInstall || row.IsBusy || row.Data == null) return;
            string server = _configService.PrintServerName ?? "";
            if (string.IsNullOrWhiteSpace(server)) { row.ErrorText = "Serveur d'impression non configuré"; return; }

            row.ErrorText = null;
            row.IsBusy = true;
            bool ok = await PrinterService.InstallAsync(PrinterService.UncPath(server, row.Name));
            row.IsBusy = false;
            if (!ok) { row.ErrorText = "Ajout impossible. Réessayez plus tard."; return; }

            // La première imprimante ajoutée sur un site devient celle par défaut de ce site
            var p = row.Data;
            bool here = _currentLieux.Any(l => l.Id == p.EmplacementId);
            string? code = here ? CurrentCode : _allLieux.FirstOrDefault(l => l.Id == p.EmplacementId)?.Code;
            bool becomesDefault = !string.IsNullOrEmpty(code) && Preferred(code) == null;
            if (becomesDefault) _prefService.SetPreferredPrinter(code!, p.NomAffiche);

            Rebuild();

            if (here && becomesDefault)
            {
                var added = MyPrinters.FirstOrDefault(r => r.Name == p.NomAffiche);
                if (added?.QueueFullName != null)
                {
                    PrinterService.SetDefault(added.QueueFullName);
                    added.DoFlash();
                }
            }
        }

        // Retrait différé : la ligne affiche « Retirée · Annuler » ; le retrait réel a lieu à la fermeture du panneau
        public void Remove(PrinterRow row)
        {
            if (row.QueueFullName == null) return;
            _pendingRemoval.Add(row.QueueFullName);
            row.IsPendingRemoval = true;
            row.IsDefault = false;
            RaiseState();
        }

        public void Undo(PrinterRow row)
        {
            if (row.QueueFullName == null) return;
            _pendingRemoval.Remove(row.QueueFullName);
            row.IsPendingRemoval = false;
            if (MyPrinters.Contains(row)) row.IsDefault = string.Equals(Preferred(CurrentCode), row.Name, StringComparison.OrdinalIgnoreCase);
            RaiseState();
        }

        /// <summary>Exécute les retraits en attente (fermeture du panneau ou de l'application).</summary>
        public async Task CommitPendingRemovalsAsync()
        {
            if (_pendingRemoval.Count == 0) return;
            var todo = _pendingRemoval.ToList();
            _pendingRemoval.Clear();

            foreach (var full in todo)
                if (await PrinterService.RemoveAsync(full)) ForgetPreferred(full);
            Rebuild();
        }

        /// <summary>Fermeture de l'application : termine les retraits en attente sans passer par l'interface (délai borné).</summary>
        public void CommitPendingRemovalsBlocking(TimeSpan timeout)
        {
            if (_pendingRemoval.Count == 0) return;
            var todo = _pendingRemoval.ToList();
            _pendingRemoval.Clear();

            var work = Task.Run(async () =>
            {
                var done = new List<string>();
                foreach (var full in todo) if (await PrinterService.RemoveAsync(full)) done.Add(full);
                return done;
            });
            if (work.Wait(timeout)) foreach (var full in work.Result) ForgetPreferred(full);
        }

        // Une imprimante retirée n'est plus l'imprimante par défaut d'aucun site
        private void ForgetPreferred(string fullName)
        {
            string shortName = Path.GetFileName(fullName);
            foreach (var key in _prefService.Current.PreferredPrinters.Where(kv => string.Equals(kv.Value, shortName, StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Key).ToList())
                _prefService.RemovePreferredPrinter(key);
        }

        // ================= Panneau =================

        private void OpenElsewhere()
        {
            SiteQuery = "";
            PrinterQuery = "";
            _pickedSite = null;
            IsElsewhereOpen = true;
            OnPropertyChanged(nameof(PickedSite));
            BuildSites();
            SitePrinters.Clear();
            RaiseState();
        }

        private void CloseElsewhere()
        {
            IsElsewhereOpen = false;
            _pickedSite = null;
            OnPropertyChanged(nameof(PickedSite));
            Sites.Clear();
            SitePrinters.Clear();
            RaiseState();
        }

        /// <summary>Échap : referme le volet ouvert, étape par étape. Renvoie false s'il n'y avait rien à refermer.</summary>
        public bool HandleEscape()
        {
            if (IsSettingsOpen) { IsSettingsOpen = false; Options.ResetAdminPanel(); return true; }
            if (IsPrinterStep) { PickedSite = null; PrinterQuery = ""; return true; }
            if (IsElsewhereOpen) { CloseElsewhere(); return true; }
            return false;
        }

        /// <summary>Panneau refermé : retraits effectués, volets refermés, recherches vidées.</summary>
        public void OnPanelHidden()
        {
            IsSettingsOpen = false;
            Options.ResetAdminPanel();
            CloseElsewhere();
            _addQuery = ""; OnPropertyChanged(nameof(AddQuery));
            IsAddOpen = false;
            IsOthersOpen = false;
            ApplyAddFilter();
            _ = CommitPendingRemovalsAsync();
        }

        private void RaiseState()
        {
            foreach (var n in new[]
            {
                nameof(CanInstall), nameof(HasSite), nameof(HeaderKicker), nameof(SiteName), nameof(ShowNote), nameof(NoteText), nameof(AddDisabledTip),
                nameof(HasMyPrinters), nameof(ShowMySection), nameof(ShowNoPrinterHint), nameof(ShowDefaultHint),
                nameof(AddCount), nameof(ShowAddSimple), nameof(ShowAddCollapsed), nameof(ShowAddOpen), nameof(ShowAddNoMatch),
                nameof(HasOthers), nameof(OtherCount), nameof(ShowTeleworkHint), nameof(ShowElsewhereLink),
                nameof(IsSiteStep), nameof(IsPrinterStep), nameof(ElsewhereTitle), nameof(ShowPrinterFilter), nameof(ShowSitesNoMatch), nameof(ShowSitePrintersNoMatch)
            }) OnPropertyChanged(n);
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task CheckSmbAccessAsync(string serverName)
        {
            bool ok;
            try { ok = await Task.Run(() => Directory.Exists($@"\\{PrinterService.CleanServerName(serverName)}\print$")); }
            catch { ok = false; }
            Application.Current.Dispatcher.Invoke(() => IsSmbAvailable = ok);
        }

        private static void OpenWindowsPrinters()
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:printers") { UseShellExecute = true }); }
            catch { try { Process.Start(new ProcessStartInfo("control", "printers") { UseShellExecute = true }); } catch { } }
        }
    }
}
