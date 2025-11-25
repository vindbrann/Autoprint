using Autoprint.Client.Data;
using Autoprint.Client.Services;
using Autoprint.Client.ViewModels;
using Autoprint.Shared;
using Hardcodet.Wpf.TaskbarNotification;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Autoprint.Client
{
    public partial class App : Application
    {
        private TaskbarIcon? _notifyIcon;

        // Services
        private readonly PathService _pathService = new PathService();
        private readonly NetworkService _networkService = new NetworkService();
        private readonly ConfigurationService _configService = new ConfigurationService();

        private ApiService? _apiService;
        private DataService? _dataService;

        // UI & État
        private MainWindow? _mainWindow;
        private MainWindowViewModel? _mainViewModel;

        // Données en mémoire
        private List<Imprimante> _toutesLesImprimantes = new List<Imprimante>();
        private Emplacement? _lieuActuel = null;
        private string _ipActuelle = "Inconnue";

        protected override async void OnStartup(StartupEventArgs e)
        {
            // 1. Surveillance du Thème Windows (Mode Clair/Sombre)
            SystemEvents.UserPreferenceChanged += (s, args) =>
            {
                if (args.Category == UserPreferenceCategory.General)
                {
                    var paletteHelper = new PaletteHelper();
                    var theme = paletteHelper.GetTheme();
                    // Si Windows ne renvoie rien, on met Light par défaut
                    var systemTheme = Theme.GetSystemTheme() ?? BaseTheme.Light;
                    theme.SetBaseTheme(systemTheme);
                    paletteHelper.SetTheme(theme);
                }
            };

            base.OnStartup(e);

            // 2. Initialisation UI (Fenêtre cachée + Icône)
            _notifyIcon = (TaskbarIcon)FindResource("MainNotifyIcon");

            _mainViewModel = new MainWindowViewModel();
            _mainWindow = new MainWindow();
            _mainWindow.DataContext = _mainViewModel;

            // 3. Initialisation des Services (Config, Clé API, Chemins, DB)
            _configService.Initialize(e.Args);
            _apiService = new ApiService(_configService.ApiKey);
            _pathService.Initialize(e.Args);
            _dataService = new DataService(_pathService);
            await _dataService.InitializeAsync();

            // 4. Détection IP Locale
            string? myIp = _networkService.GetLocalIpAddress();
            _ipActuelle = myIp ?? "Non connecté";

            // 5. Boucle de Connexion (Tentative API -> Sinon Cache)
            List<Emplacement> lieux = new List<Emplacement>();
            int tentatives = 0;
            bool modeHorsLigne = false;

            while (tentatives < 5)
            {
                if (_apiService != null)
                {
                    // Téléchargement parallèle Lieux + Imprimantes
                    var taskLieux = _apiService.GetLieuxAsync();
                    var taskImprimantes = _apiService.GetImprimantesAsync();
                    await Task.WhenAll(taskLieux, taskImprimantes);

                    lieux = taskLieux.Result;
                    _toutesLesImprimantes = taskImprimantes.Result;
                }

                if (lieux.Count > 0)
                {
                    // Succès : On met à jour le cache local
                    if (_dataService != null)
                    {
                        await _dataService.SaveEmplacementsAsync(lieux);
                        if (_toutesLesImprimantes.Count > 0)
                            await _dataService.SaveImprimantesAsync(_toutesLesImprimantes);
                    }
                    break;
                }
                tentatives++;
                await Task.Delay(2000);
            }

            // 6. Fallback (Si serveur éteint, on lit le cache)
            if (lieux.Count == 0 && _dataService != null)
            {
                lieux = await _dataService.GetEmplacementsAsync();
                _toutesLesImprimantes = await _dataService.GetImprimantesAsync();
                modeHorsLigne = true;
            }

            // 7. Logique de Détection & Notification
            string titreNotif = "Autoprint";
            string messageNotif = "Démarrage...";
            // CORRECTION 1 : On met 'None' pour ne pas avoir le ballon bleu Windows
            BalloonIcon iconeNotif = BalloonIcon.None;

            if (lieux.Count > 0 && !string.IsNullOrEmpty(myIp))
            {
                var lieuTrouve = lieux.Find(l => IpHelper.IsIpInCidr(myIp, l.CidrIpv4));

                if (lieuTrouve != null)
                {
                    _lieuActuel = lieuTrouve;
                    MettreAJourInterface();

                    int nbImprimantesZone = _toutesLesImprimantes.Count(i => i.EmplacementId == lieuTrouve.Id);

                    // CORRECTION 2 : On remet l'épingle (Emoji) dans le titre
                    titreNotif = $"📍 Lieu : {lieuTrouve.Nom}";

                    if (nbImprimantesZone > 0)
                        messageNotif = $"{nbImprimantesZone} imprimante(s) disponible(s).";
                    else
                        messageNotif = "Aucune imprimante pour cette zone.";

                    if (modeHorsLigne) messageNotif += "\n(Mode Hors-Ligne ⚠️)";
                }
                else
                {
                    titreNotif = "⛔ Lieu Inconnu";
                    messageNotif = $"IP : {myIp}\nAucune zone correspondante.";
                    iconeNotif = BalloonIcon.Warning; // On garde le triangle jaune pour les erreurs
                }
            }
            else
            {
                titreNotif = "Erreur";
                messageNotif = "Impossible de récupérer la configuration.";
                iconeNotif = BalloonIcon.Error;
            }

            // 8. Affichage de la bulle
            if (_notifyIcon != null)
            {
                _notifyIcon.ShowBalloonTip(titreNotif, messageNotif, iconeNotif);
            }
        }

        // ============================================================
        // GESTION DES ÉVÉNEMENTS
        // ============================================================

        // Clic sur la bulle -> Ouvre la fenêtre
        private void OnTrayBalloonTipClicked(object sender, RoutedEventArgs e) => AfficherFenetre();

        // Double clic icône -> Ouvre la fenêtre
        private void OnTrayDoubleClick(object sender, RoutedEventArgs e) => AfficherFenetre();

        // Menu -> Ouvrir
        private void MenuOpen_Click(object sender, RoutedEventArgs e) => AfficherFenetre();

        // Menu -> Quitter (Arrêt complet)
        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            _notifyIcon?.Dispose();
            Shutdown();
        }

        private void AfficherFenetre()
        {
            if (_mainWindow == null) return;

            // On s'assure que les données affichées sont à jour
            MettreAJourInterface();

            if (_mainWindow.Visibility == Visibility.Visible)
                _mainWindow.Activate(); // Met au premier plan
            else
                _mainWindow.Show();     // Affiche
        }

        // Filtre les imprimantes selon le lieu actuel et envoie au ViewModel
        private void MettreAJourInterface()
        {
            if (_mainViewModel == null) return;

            string nomLieu = _lieuActuel?.Nom ?? "Lieu Inconnu";
            List<Imprimante> imprimantesDuLieu;

            if (_lieuActuel != null)
            {
                imprimantesDuLieu = _toutesLesImprimantes
                    .Where(i => i.EmplacementId == _lieuActuel.Id)
                    .ToList();
            }
            else
            {
                imprimantesDuLieu = new List<Imprimante>();
            }

            _mainViewModel.ChargerDonnees(nomLieu, imprimantesDuLieu, _ipActuelle);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            base.OnExit(e);
        }
    }
}