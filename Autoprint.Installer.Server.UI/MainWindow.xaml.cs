using Autoprint.Installer.Server.UI.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Web.Administration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Autoprint.Installer.Server.UI
{
    public partial class MainWindow : Window
    {
        private int _currentStep = 1;
        private readonly PrerequisiteService _prereqService;
        private bool _prereqsValidated = false;
        private bool _isUpgrade = false;
        // Mise à jour d'une version antérieure à 26.11 : « AllowedHosts » vaut encore « * » (backlog B17)
        private bool _needsPublicHost = false;
        private bool _sqlGrantFailed = false;   // droits SQL non accordés par l'installeur (script à exécuter par un DBA)
        private int _existingPort = 0;

        private const string InstallPath = @"C:\Program Files\Autoprint Server";
        // Base SQLite hors du dossier du programme (backlog B25) : le compte du site n'a pas besoin d'écrire dans Program Files
        private static readonly string DataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Autoprint", "Data");
        private static string SqlitePath => Path.Combine(DataPath, "Autoprint.db");

        private const string NamespacePrefix = "Autoprint.Installer.Server.UI.Resources.";
        private const string MsiResourceName = NamespacePrefix + "Autoprint.Server.Setup.msi";
        private const string LicenseResourceName = NamespacePrefix + "License.rtf";
        private const string DotNetResourceName = NamespacePrefix + "dotnet-hosting-10.exe";

        public MainWindow()
        {
            InitializeComponent();
            DetectExistingConfiguration();
            UpdateUiForUpgrade();
            _prereqService = new PrerequisiteService();
            LoadLicenseResource();
            UpdateView();
        }

        private void LoadLicenseResource()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (Stream? stream = assembly.GetManifestResourceStream(LicenseResourceName))
                {
                    if (stream != null)
                    {
                        var range = new TextRange(RtbLicense.Document.ContentStart, RtbLicense.Document.ContentEnd);
                        range.Load(stream, DataFormats.Rtf);
                    }
                    else
                    {
                        var range = new TextRange(RtbLicense.Document.ContentStart, RtbLicense.Document.ContentEnd);
                        range.Text = "Fichier de licence introuvable.";
                    }
                }
            }
            catch {  }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateCurrentStep()) return;

            if (_currentStep == 3 && _isUpgrade && _needsPublicHost)
            {
                // Seul le nom public est demandé : le port du site existant est conservé
                if (_existingPort > 0) TxtWebPort.Text = _existingPort.ToString();
                TxtWebPort.IsEnabled = false;
                _currentStep = 5;
                UpdateView();
                return;
            }

            if (_currentStep == 3 && _isUpgrade)
            {
                _currentStep = 6;
                UpdateView();
                _ = RunInstallationSequence();
                return;
            }

            if (_currentStep == 5)
            {
                _currentStep = 6;
                UpdateView();
                _ = RunInstallationSequence();
                return;
            }

            // Fin
            if (_currentStep == 7)
            {
                System.Windows.Application.Current.Shutdown();
                return;
            }

            _currentStep++;
            UpdateView();
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            // En mise à jour, l'écran SQL (étape 4) n'est pas proposé
            if (_isUpgrade && _currentStep == 5) { _currentStep = 3; UpdateView(); return; }
            if (_currentStep > 1) { _currentStep--; UpdateView(); }
        }

        private void UpdateView()
        {
            View1_Welcome.Visibility = Visibility.Collapsed;
            View2_License.Visibility = Visibility.Collapsed;
            View3_Prereqs.Visibility = Visibility.Collapsed;
            View4_Sql.Visibility = Visibility.Collapsed;
            View5_Iis.Visibility = Visibility.Collapsed;
            View6_Install.Visibility = Visibility.Collapsed;
            View7_Finish.Visibility = Visibility.Collapsed;

            switch (_currentStep)
            {
                case 1: View1_Welcome.Visibility = Visibility.Visible; break;
                case 2: View2_License.Visibility = Visibility.Visible; break;
                case 3: View3_Prereqs.Visibility = Visibility.Visible; RunPrereqCheck(); break;
                case 4: View4_Sql.Visibility = Visibility.Visible; break;
                case 5: View5_Iis.Visibility = Visibility.Visible; break;
                case 6: View6_Install.Visibility = Visibility.Visible; break;
                case 7: View7_Finish.Visibility = Visibility.Visible; break;
            }

            BtnPrev.IsEnabled = _currentStep > 1 && _currentStep < 6;
            BtnNext.IsEnabled = _currentStep != 6;

            if (_currentStep == 6) BtnNext.Visibility = Visibility.Collapsed;
            else BtnNext.Visibility = Visibility.Visible;

            if (_currentStep == 7) BtnNext.Content = "Terminer";
            else BtnNext.Content = "Suivant";
        }

        private bool ValidateCurrentStep()
        {
            if (_currentStep == 2 && ChkAcceptLicense.IsChecked != true)
            {
                MessageBox.Show("Vous devez accepter la licence.", "Attention", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (_currentStep == 3 && !_prereqsValidated)
            {
                MessageBox.Show("Pré-requis manquants. Utilisez les boutons 'Installer'.", "Système", MessageBoxButton.OK, MessageBoxImage.Stop);
                return false;
            }
            if (_currentStep == 4 && RadioSqlServer.IsChecked == true)
            {
                try { BuildSqlDataSource(TxtSqlServer.Text, TxtSqlPort.Text); }
                catch (ArgumentException ex)
                {
                    MessageBox.Show(ex.Message, "SQL Server", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            if (_currentStep == 5 && (!int.TryParse(TxtWebPort.Text, out int p) || p < 1))
            {
                MessageBox.Show("Port Web invalide.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (_currentStep == 5 && !System.Text.RegularExpressions.Regex.IsMatch(TxtPublicHost.Text.Trim(), @"^[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?)*$"))
            {
                MessageBox.Show("Nom public de la console invalide (ex : autoprint.maboite.fr, sans http:// ni port).", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private async void RunPrereqCheck()
        {
            _prereqsValidated = false;
            var res = await Task.Run(() => _prereqService.CheckSystem());

            UpdatePrereqRow(IconIis, MsgIis, BtnInstallIis, res.IsIisInstalled);
            UpdatePrereqRow(IconPrint, MsgPrint, BtnInstallPrint, res.IsPrintServerRoleInstalled);
            UpdatePrereqRow(IconNet, MsgNet, BtnInstallNet, res.IsNet10Installed);
            if (res.IsNet10Installed)
                MsgNet.Text = $"Installé (version {res.InstalledAspNetCoreVersion})";
            else if (res.InstalledAspNetCoreVersion != null)
            {
                // Runtime présent mais plus ancien que celui embarqué : il lui manque des correctifs de sécurité
                MsgNet.Text = $"Version {res.InstalledAspNetCoreVersion} trop ancienne : {res.RequiredAspNetCoreVersion} requise (correctifs de sécurité)";
                BtnInstallNet.Content = "Mettre à jour";
            }

            _prereqsValidated = res.IsAllGood;
        }

        private void UpdatePrereqRow(TextBlock icon, TextBlock msg, Button btn, bool isOk)
        {
            icon.Text = isOk ? "✅" : "❌";
            msg.Text = isOk ? "Installé" : "Manquant";
            msg.Foreground = isOk ? Brushes.Green : Brushes.Red;
            btn.Visibility = isOk ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void BtnInstallIis_Click(object sender, RoutedEventArgs e)
        {
            BtnInstallIis.IsEnabled = false;
            try { await _prereqService.InstallIis(); RunPrereqCheck(); }
            catch (Exception ex) { MessageBox.Show("Erreur : " + ex.Message); }
            finally { BtnInstallIis.IsEnabled = true; }
        }

        private async void BtnInstallPrint_Click(object sender, RoutedEventArgs e)
        {
            BtnInstallPrint.IsEnabled = false;
            try { await _prereqService.InstallPrintServer(); RunPrereqCheck(); }
            catch (Exception ex) { MessageBox.Show("Erreur : " + ex.Message); }
            finally { BtnInstallPrint.IsEnabled = true; }
        }

        private async void BtnInstallNet_Click(object sender, RoutedEventArgs e)
        {
            BtnInstallNet.IsEnabled = false;
            BtnInstallNet.Content = "Extraction...";
            try
            {
                string tempExe = Path.Combine(Path.GetTempPath(), "dotnet-hosting-10.exe");
                await ExtractResource(DotNetResourceName, tempExe);

                BtnInstallNet.Content = "Installation...";
                await RunProcess(tempExe, "/install /passive /norestart");

                MessageBox.Show($"Runtime .NET {PrerequisiteService.RequiredAspNetCoreVersion} installé.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                RunPrereqCheck();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur : " + ex.Message);
                BtnInstallNet.Content = "Réessayer";
                BtnInstallNet.IsEnabled = true;
            }
        }

        private void RadioSql_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelSqlServer != null) PanelSqlServer.Visibility = (RadioSqlServer.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ComboAuth_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PanelSqlCreds == null) return;
            PanelSqlCreds.Visibility = (ComboAuth.SelectedIndex == 1) ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Adresse SQL Server à partir du nom du serveur et d'un port facultatif. SQL Server note le port après une virgule
        /// (« serveur,1433 ») : l'utilisateur saisit le port dans son propre champ, ou « serveur:1433 », ou même la virgule.
        /// </summary>
        internal static string BuildSqlDataSource(string server, string? port)
        {
            string s = (server ?? "").Trim();
            string p = (port ?? "").Trim();

            // « serveur:1433 » ou « serveur,1433 » saisi dans le champ serveur (pas d'IPv6 entre crochets à gérer ici)
            int sep = s.LastIndexOfAny(new[] { ':', ',' });
            if (sep > 0 && sep < s.Length - 1 && s.IndexOf(':') == s.LastIndexOf(':') && int.TryParse(s[(sep + 1)..], out _))
            {
                if (p.Length == 0) p = s[(sep + 1)..];
                s = s[..sep].Trim();
            }

            if (p.Length == 0) return s;
            if (!int.TryParse(p, out int n) || n < 1 || n > 65535) throw new ArgumentException($"Port SQL Server invalide : « {p} » (1 à 65535).");
            return $"{s},{n}";
        }

        private string GetSqlConnectionString(int timeout = 60)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = BuildSqlDataSource(TxtSqlServer.Text, TxtSqlPort.Text),
                InitialCatalog = TxtSqlDb.Text,
                TrustServerCertificate = true,
                ConnectTimeout = timeout
            };

            if (ComboAuth.SelectedIndex == 0) builder.IntegratedSecurity = true;
            else
            {
                builder.IntegratedSecurity = false;
                builder.UserID = TxtSqlUser.Text;
                builder.Password = TxtSqlPass.Password;
            }
            return builder.ConnectionString;
        }

        private async void BtnTestSql_Click(object sender, RoutedEventArgs e)
        {
            BtnTestSql.IsEnabled = false;
            BtnTestSql.Content = "Test...";
            try
            {
                // Connexion au serveur (base master) : sur une installation neuve, la base n'existe pas encore (B59)
                string cnx = WithCatalog(GetSqlConnectionString(5), "master");
                string db = TxtSqlDb.Text.Trim();
                bool exists = await Task.Run(() =>
                {
                    using var c = new SqlConnection(cnx); c.Open();
                    using var cmd = new SqlCommand("SELECT DB_ID(@db)", c);
                    cmd.Parameters.AddWithValue("@db", db);
                    return cmd.ExecuteScalar() is not (null or DBNull);
                });
                MessageBox.Show(exists ? $"Connexion réussie ; la base « {db} » existe."
                                       : $"Connexion réussie ; la base « {db} » n'existe pas encore : elle sera créée pendant l'installation.",
                    "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Echec de connexion :\n" + ex.Message, "Erreur SQL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnTestSql.IsEnabled = true;
                BtnTestSql.Content = "Tester la connexion";
            }
        }

        private async Task RunInstallationSequence()
        {
            try
            {
                TxtInstallLog.Text = "Arrêt du service IIS...";
                await Task.Run(() => StopIISPool());

                TxtInstallLog.Text = "Extraction des fichiers...";
                string msi = Path.Combine(Path.GetTempPath(), "AutoprintSetup.msi");
                await ExtractResource(MsiResourceName, msi);

                string? oldJsonContent = null;
                string configPath = Path.Combine(InstallPath, "appsettings.json");

                if (_isUpgrade && File.Exists(configPath))
                {
                    TxtInstallLog.Text = "Lecture de la configuration actuelle...";
                    try
                    {
                        oldJsonContent = await File.ReadAllTextAsync(configPath);
                    }
                    catch (Exception ex)
                    {
                        TxtInstallLog.Text += $"\n[Warn] Lecture config échouée : {ex.Message}";
                    }
                }

                TxtInstallLog.Text = _isUpgrade ? "Mise à jour (Ecrasement des fichiers)..." : "Installation...";

                string logPath = Path.Combine(Path.GetTempPath(), "autoprint_msi.log");
                await RunProcess("msiexec.exe", $"/i \"{msi}\" /l*v \"{logPath}\" /qn /norestart");

                TxtInstallLog.Text = "Configuration des droits (NTFS)...";
                await Task.Run(() => ConfigureNtfsPermissions());

                if (_isUpgrade && !string.IsNullOrEmpty(oldJsonContent) && File.Exists(configPath))
                {
                    TxtInstallLog.Text = "Fusion de la configuration...";
                    try
                    {
                        var oldNode = System.Text.Json.Nodes.JsonNode.Parse(oldJsonContent)?.AsObject();
                        string newJsonContent = await File.ReadAllTextAsync(configPath);
                        var newNode = System.Text.Json.Nodes.JsonNode.Parse(newJsonContent)?.AsObject();

                        if (oldNode != null && newNode != null)
                        {
                            MergeJsonNodes(newNode, oldNode);
                            var options = new JsonSerializerOptions { WriteIndented = true };
                            await File.WriteAllTextAsync(configPath, newNode.ToJsonString(options));
                        }
                    }
                    catch (Exception ex)
                    {
                        TxtInstallLog.Text += $"\n[Erreur] Echec de la fusion : {ex.Message}";
                        await File.WriteAllTextAsync(configPath, oldJsonContent);
                    }

                    if (_needsPublicHost) await ApplyPublicHost(configPath);
                    await MoveSqliteDatabaseToProgramData(configPath);
                    await CapJwtLifetime(configPath);

                    TxtInstallLog.Text = "Passage du site en compte sans privilège...";
                    await EnableSpoolerServiceMode();
                    await GrantSqlAccessForUpgrade();
                }
                else if (!_isUpgrade)
                {
                    if (RadioSqlServer.IsChecked == true)
                    {
                        // La base doit exister avant d'y accorder des droits (B59)
                        TxtInstallLog.Text = "Création de la base SQL...";
                        await EnsureSqlDatabase(GetSqlConnectionString(60), TxtSqlDb.Text.Trim());
                    }
                    TxtInstallLog.Text = "Génération configuration (JSON)...";
                    await ConfigureAppSettings();
                }

                TxtInstallLog.Text = "Protection des fichiers sensibles...";
                await Task.Run(() => HardenSensitiveFiles());

                TxtInstallLog.Text = "Migration de la base de données...";
                await RunMigrationProcess();

                if (!_isUpgrade)
                {
                    TxtInstallLog.Text = "Configuration finale IIS...";
                    ConfigureIIS();

                    // Droits SQL du site : après la création du pool, sinon « IIS AppPool\Autoprint » est inconnu de Windows (B59)
                    if (RadioSqlServer.IsChecked == true && ComboAuth.SelectedIndex == 0)
                    {
                        TxtInstallLog.Text = "Configuration droits SQL...";
                        string adminString = GetSqlConnectionString(60);
                        string login = GetSqlLoginForSite(adminString);
                        if (login == PoolIdentity) await Task.Run(() => WaitForPoolAccount());
                        await GrantSqlAccessToSite(adminString, TxtSqlDb.Text, login);
                    }
                }
                else
                {
                    SwitchPoolToApplicationPoolIdentity();
                }

                TxtInstallLog.Text = "Ouverture du port dans le pare-feu...";
                int sitePort = _isUpgrade ? _existingPort : int.Parse(TxtWebPort.Text);
                await Task.Run(() => EnsureFirewallRule(sitePort));

                // Le pool existe maintenant : droits NTFS de son identité (et base SQLite créée par la migration)
                await Task.Run(() => HardenSensitiveFiles());

                TxtInstallLog.Text = "Démarrage du service AutoprintSpooler...";
                await Task.Run(() => RestartSpoolerService());

                _currentStep = 7;
                UpdateView();

                if (_sqlGrantFailed)
                {
                    // Rappel sur l'écran final : la console ne fonctionnera qu'après le script de l'administrateur SQL (B73)
                    TxtSqlAction.Text = "Action requise : faire exécuter par un administrateur SQL le script « " +
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Autoprint-droits-SQL.sql") +
                        " ». D'ici là, la console ne peut pas accéder à sa base.";
                    PanelSqlAction.Visibility = Visibility.Visible;
                }

                if (_isUpgrade)
                {
                    BtnOpenIis.Visibility = Visibility.Collapsed;
                    if (FindName("TxtFinishTitle") is TextBlock txtTitle) txtTitle.Text = "Mise à jour terminée";
                }
                else
                {
                    BtnOpenIis.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                TxtInstallLog.Text = "ERREUR CRITIQUE : " + ex.Message;
                TxtInstallLog.Foreground = Brushes.Red;
                PrgInstall.IsIndeterminate = false;

                if (!_isUpgrade)
                {
                    var result = MessageBox.Show(
                        "L'installation a échoué.\n\nVoulez-vous supprimer les fichiers et configurations créés lors de cette installation ?",
                        "Échec de l'installation",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        TxtInstallLog.Text += "\nNettoyage en cours...";
                        try
                        {
                            string msi = Path.Combine(Path.GetTempPath(), "AutoprintSetup.msi");
                            if (File.Exists(msi))
                            {
                                TxtInstallLog.Text += "\nDésinstallation du package MSI...";
                                await RunProcess("msiexec.exe", $"/x \"{msi}\" /qn");
                            }

                            TxtInstallLog.Text += "\nSuppression du site IIS...";
                            await Task.Run(() => CleanupIIS());

                            if (Directory.Exists(InstallPath))
                            {
                                TxtInstallLog.Text += "\nSuppression du répertoire d'installation...";
                                await Task.Run(() =>
                                {
                                    try { Directory.Delete(InstallPath, true); } catch { }
                                });
                            }
                            TxtInstallLog.Text += "\nNettoyage terminé.";
                        }
                        catch (Exception cleanEx)
                        {
                            TxtInstallLog.Text += $"\n[Erreur Nettoyage] {cleanEx.Message}";
                        }
                    }
                }

                BtnNext.Visibility = Visibility.Visible;
                BtnNext.Content = "Fermer";
                BtnNext.IsEnabled = true;

                BtnNext.Click -= BtnNext_Click;
                BtnNext.Click += (s, args) => Close();
            }
            finally
            {
                TxtInstallLog.Text += "\nRedémarrage du site...";
                RestartIISSite();
            }
        }

        private Task ExtractResource(string resource, string dest)
        {
            return Task.Run(() =>
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream(resource);
                if (stream == null)
                {
                    var avail = string.Join("\n", assembly.GetManifestResourceNames());
                    throw new Exception($"Ressource introuvable : {resource}\nDispo :\n{avail}");
                }
                using var file = File.Create(dest);
                stream.CopyTo(file);
            });
        }

        private Task RunProcess(string exe, string args)
        {
            return Task.Run(() => {
                var p = Process.Start(new ProcessStartInfo { FileName = exe, Arguments = args, UseShellExecute = true, Verb = "runas" });
                if (p == null) throw new Exception($"Impossible de démarrer le processus : {exe}");
                p.WaitForExit();
                if (p.ExitCode != 0) throw new Exception($"Erreur Code {p.ExitCode} pour {exe}");
            });
        }

        private async Task ConfigureAppSettings()
        {
            string path = Path.Combine(InstallPath, "appsettings.json");

            for (int i = 0; i < 10; i++) { if (File.Exists(path)) break; await Task.Delay(500); }

            if (!File.Exists(path)) throw new FileNotFoundException($"Fichier de config introuvable : {path}");

            string connectionString;
            string provider;

            if (RadioSqlServer.IsChecked == true)
            {
                provider = "SqlServer";
                connectionString = GetSqlConnectionString();
            }
            else
            {
                provider = "Sqlite";
                Directory.CreateDirectory(DataPath);
                connectionString = $"Data Source={SqlitePath}";
            }

            var appSettings = new
            {
                Database = new
                {
                    Provider = provider,
                    ConnectionStrings = new
                    {
                        SqlServer = (provider == "SqlServer") ? connectionString : "",
                        Sqlite = provider == "Sqlite" ? connectionString : $"Data Source={SqlitePath}"
                    }
                },
                Logging = new
                {
                    LogLevel = new { Default = "Information", Microsoft = "Warning" }
                },
                // Seuls le nom public et localhost sont acceptés (protection contre l'usurpation d'en-tête Host)
                AllowedHosts = $"{TxtPublicHost.Text.Trim()};localhost",
                // Opérations du spouleur déléguées au service AutoprintSpooler (le site tourne sans privilège)
                Spooler = new { Mode = "Service" },
                Jwt = new
                {
                    Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                    Issuer = "AutoprintServer",
                    Audience = "AutoprintClient",
                    // Jeton court, renouvelé automatiquement par la console (backlog B19)
                    ExpireMinutes = 30
                },
                ConnectionStrings = new
                {
                    DefaultConnection = connectionString
                }
            };

            string jsonString = JsonSerializer.Serialize(appSettings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, jsonString);
        }

        private async Task RunMigrationProcess()
        {
            TxtInstallLog.Text = "Migration de la base de données...";
            string exePath = Path.Combine(InstallPath, "Autoprint.Server.exe");

            if (!File.Exists(exePath)) return;

            await Task.Run(() =>
            {
                var info = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "--migrate-only",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = InstallPath
                };

                using var proc = Process.Start(info);
                if (proc == null) return;

                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();

                proc.WaitForExit();

                if (proc.ExitCode != 0 && _isUpgrade && _sqlGrantFailed)
                {
                    // L'installeur n'a pas de droit SQL (script à exécuter par un administrateur SQL) : sa migration échoue
                    // forcément ; le site l'appliquera à son démarrage sous le compte du pool, une fois les droits accordés (B73)
                    Dispatcher.Invoke(() => TxtInstallLog.Text += "\n[Attention] Migration de la base différée : le site l'appliquera à son démarrage, après exécution du script SQL.");
                    return;
                }

                if (proc.ExitCode != 0)
                {
                    string details = "";
                    if (!string.IsNullOrEmpty(output)) details += "\n[MIGRATION-LOG] :\n" + output;
                    if (!string.IsNullOrEmpty(error)) details += "\n[MIGRATION-ERR] :\n" + error;
                    throw new Exception("Erreur lors de la migration." + details);
                }
            });
        }

        private void RestartIISSite()
        {
            try
            {
                using var mgr = new ServerManager();
                // Le pool a été arrêté au début de l'installation (StopIISPool) : un pool arrêté ne se recycle pas,
                // il faut le redémarrer, sinon le site reste en erreur 503 après chaque mise à jour (B61)
                var pool = mgr.ApplicationPools.FirstOrDefault(p => p.Name == "Autoprint");
                if (pool != null)
                {
                    if (pool.State is ObjectState.Stopped or ObjectState.Stopping)
                    {
                        for (int i = 0; i < 10 && pool.State == ObjectState.Stopping; i++) System.Threading.Thread.Sleep(500);
                        pool.Start();
                    }
                    else pool.Recycle();
                }

                var site = mgr.Sites.FirstOrDefault(s => s.Name == "Autoprint");
                if (site != null)
                {
                    if (site.State == ObjectState.Started)
                    {
                        site.Stop();
                        System.Threading.Thread.Sleep(1000);
                    }
                    if (site.State != ObjectState.Started) site.Start();
                }
            }
            catch (Exception ex)
            {
                // Ne plus masquer l'échec : l'administrateur doit savoir que le site n'a pas redémarré
                TxtInstallLog.Text += $"\n[Attention] Redémarrage du site Autoprint impossible ({ex.Message}) : démarrer le pool « Autoprint » dans le Gestionnaire IIS.";
            }
        }

        private void CleanupIIS()
        {
            try
            {
                using var mgr = new ServerManager();
                var site = mgr.Sites.FirstOrDefault(s => s.Name == "Autoprint");
                if (site != null) mgr.Sites.Remove(site);

                var pool = mgr.ApplicationPools.FirstOrDefault(p => p.Name == "Autoprint");
                if (pool != null) mgr.ApplicationPools.Remove(pool);

                mgr.CommitChanges();
            }
            catch { }
        }

        private void DetectExistingConfiguration()
        {
            string configPath = Path.Combine(InstallPath, "appsettings.json");

            bool iisSiteExists = false;
            try
            {
                using var mgr = new ServerManager();
                iisSiteExists = mgr.Sites.Any(s => s.Name == "Autoprint");
            }
            catch { }

            if (File.Exists(configPath) && iisSiteExists)
            {
                _isUpgrade = true;
                try
                {
                    string json = File.ReadAllText(configPath);
                    using var doc = JsonDocument.Parse(json);
                    string allowedHosts = doc.RootElement.TryGetProperty("AllowedHosts", out var hostsElement) ? hostsElement.GetString() ?? "" : "";
                    _needsPublicHost = string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Trim() == "*";

                    // Port du site existant : liaison IIS en priorité (backlog B11), à défaut l'ancien paramètre ClientUrl
                    try
                    {
                        using var mgr = new ServerManager();
                        var binding = mgr.Sites.FirstOrDefault(s => s.Name == "Autoprint")?.Bindings.FirstOrDefault(b => b.EndPoint != null);
                        if (binding != null) _existingPort = binding.EndPoint.Port;
                    }
                    catch { }

                    if (_existingPort == 0 && doc.RootElement.TryGetProperty("ClientUrl", out var urlElement))
                    {
                        string url = urlElement.GetString() ?? "";
                        var parts = url.Split(':');
                        if (parts.Length > 0 && int.TryParse(parts.Last(), out int port))
                        {
                            _existingPort = port;
                        }
                    }
                }
                catch {  }
            }
            else
            {
                _isUpgrade = false;
            }
        }

        // Mise à jour d'une installation SQLite : la base quitte le dossier du programme pour ProgramData (backlog B25)
        private async Task MoveSqliteDatabaseToProgramData(string configPath)
        {
            var root = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!.AsObject();
            string provider = root["Database"]?["Provider"]?.GetValue<string>() ?? "";
            if (!provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)) return;

            string cs = root["Database"]?["ConnectionStrings"]?["Sqlite"]?.GetValue<string>() ?? "Data Source=Autoprint.db";
            string source = cs.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) ? cs.Substring("Data Source=".Length).Trim() : "Autoprint.db";
            string sourcePath = Path.IsPathRooted(source) ? source : Path.Combine(InstallPath, source);

            // Déjà ailleurs que dans le dossier du programme : rien à faire
            if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(sourcePath))?.TrimEnd('\\'), InstallPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;

            Directory.CreateDirectory(DataPath);
            if (File.Exists(sourcePath))
            {
                if (File.Exists(SqlitePath))
                    throw new Exception($"Une base existe déjà dans {DataPath} : déplacement annulé pour ne rien écraser. Vérifiez manuellement.");

                // Le site doit être arrêté pour libérer la base
                using (var mgr = new ServerManager())
                {
                    var appPool = mgr.ApplicationPools.FirstOrDefault(ap => ap.Name == "Autoprint");
                    if (appPool != null && appPool.State == ObjectState.Started) appPool.Stop();
                }
                await Task.Delay(3000);

                foreach (var suffix in new[] { "", "-wal", "-shm" })
                {
                    if (File.Exists(sourcePath + suffix)) File.Move(sourcePath + suffix, SqlitePath + suffix);
                }
                TxtInstallLog.Text += $"\nBase SQLite déplacée vers {SqlitePath}";
            }

            root["Database"]!["ConnectionStrings"]!["Sqlite"] = $"Data Source={SqlitePath}";
            await File.WriteAllTextAsync(configPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        // Mise à jour : « AllowedHosts » passe de « * » au nom public saisi (+ localhost)
        private async Task ApplyPublicHost(string configPath)
        {
            var root = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!.AsObject();
            root["AllowedHosts"] = $"{TxtPublicHost.Text.Trim()};localhost";
            await File.WriteAllTextAsync(configPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        // Durée des jetons : 30 min depuis B19 (renouvellement automatique par la console). La fusion de la configuration
        // conservait l'ancienne valeur (120 min en 1.1.x) : on la ramène à 30 min si elle est plus longue (B63).
        private const int MaxJwtMinutes = 30;
        private static async Task CapJwtLifetime(string configPath)
        {
            var root = JsonNode.Parse(await File.ReadAllTextAsync(configPath))?.AsObject();
            if (root?["Jwt"] is not JsonObject jwt) return;
            int current = int.TryParse(jwt["ExpireMinutes"]?.ToString(), out int v) ? v : 0;
            if (current is > 0 and <= MaxJwtMinutes) return;
            jwt["ExpireMinutes"] = MaxJwtMinutes;
            await File.WriteAllTextAsync(configPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        private void UpdateUiForUpgrade()
        {
            if (_isUpgrade)
            {
                TxtWelcomeTitle.Text = "Mise à jour détectée";
                TxtWelcomeDesc.Text = "Une version existante a été trouvée. L'assistant va mettre à jour le serveur en conservant vos données.";
                this.Title = "Mise à jour Autoprint Server";
            }
        }

        private void ConfigureNtfsPermissions()
        {
            try
            {
                // Les versions précédentes donnaient « Modification » à NETWORK SERVICE sur tout le dossier : n'importe quel service
                // tournant sous ce compte pouvait remplacer les DLL chargées en SYSTEM par le pool IIS. Le pool tourne en LocalSystem :
                // ce droit est inutile, on le retire (y compris lors d'une mise à jour).
                var dirInfo = new DirectoryInfo(InstallPath);
                var security = dirInfo.GetAccessControl();
                var networkService = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);
                security.PurgeAccessRules(networkService);
                dirInfo.SetAccessControl(security);
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors de l'application des droits NTFS : {ex.Message}");
            }
        }

        /// <summary>
        /// SID du compte virtuel « IIS AppPool\&lt;pool&gt; » : S-1-5-82 suivi du SHA-1 du nom du pool en minuscules (UTF-16).
        /// Identique sur tous les serveurs et indépendant de la langue ; disponible avant même la création du pool.
        /// </summary>
        internal static SecurityIdentifier AppPoolSid(string poolName)
        {
            byte[] hash = SHA1.HashData(System.Text.Encoding.Unicode.GetBytes(poolName.ToLowerInvariant()));
            var parts = Enumerable.Range(0, 5).Select(i => BitConverter.ToUInt32(hash, i * 4));
            return new SecurityIdentifier("S-1-5-82-" + string.Join("-", parts));
        }

        // Fichiers contenant des secrets (clé JWT, chaîne SQL, base SQLite, clés de chiffrement) : accès réservé à SYSTEM
        // (identité du pool IIS) et aux Administrateurs, sans héritage des droits « Utilisateurs » de Program Files / ProgramData (audit #9).
        private void HardenSensitiveFiles()
        {
            var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

            // Identité du pool IIS : SID calculé, car la résolution par nom échoue tant qu'IIS n'a pas pris en compte
            // le pool (installation neuve : le site n'avait alors aucun droit sur appsettings.json et plantait, B51)
            SecurityIdentifier pool = AppPoolSid(PoolIdentity.Substring(PoolIdentity.IndexOf('\\') + 1));

            foreach (var file in new[] { Path.Combine(InstallPath, "appsettings.json") }.Where(File.Exists))
            {
                var fileSecurity = new FileSecurity();
                fileSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                fileSecurity.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, AccessControlType.Allow));
                fileSecurity.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, AccessControlType.Allow));
                // appsettings.json : lecture seule pour le site
                if (pool != null) fileSecurity.AddAccessRule(new FileSystemAccessRule(pool, FileSystemRights.Read, AccessControlType.Allow));
                new FileInfo(file).SetAccessControl(fileSecurity);
            }

            // Même emplacement que le serveur (Program.cs, DataProtection:KeysPath par défaut)
            string keysPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Autoprint", "Keys");
            var keysDir = Directory.CreateDirectory(keysPath);
            var dirSecurity = new DirectorySecurity();
            dirSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            dirSecurity.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            dirSecurity.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            // Le site crée et renouvelle ses clés de chiffrement (rotation automatique tous les 90 jours)
            if (pool != null) dirSecurity.AddAccessRule(new FileSystemAccessRule(pool, FileSystemRights.Modify, inherit, PropagationFlags.None, AccessControlType.Allow));
            keysDir.SetAccessControl(dirSecurity);

            // Base SQLite (et ses fichiers -wal / -shm) : dossier dédié, réservé à SYSTEM, aux Administrateurs et au site
            if (Directory.Exists(DataPath))
            {
                var dataSecurity = new DirectorySecurity();
                dataSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                dataSecurity.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
                dataSecurity.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
                if (pool != null) dataSecurity.AddAccessRule(new FileSystemAccessRule(pool, FileSystemRights.Modify, inherit, PropagationFlags.None, AccessControlType.Allow));
                new DirectoryInfo(DataPath).SetAccessControl(dataSecurity);
                foreach (var dbFile in Directory.GetFiles(DataPath))
                {
                    var fs = new FileInfo(dbFile).GetAccessControl();
                    fs.SetAccessRuleProtection(isProtected: false, preserveInheritance: false); // héritage du dossier
                    new FileInfo(dbFile).SetAccessControl(fs);
                }
            }

            // Aucun droit explicite du site sur le dossier du programme (il ne doit pas pouvoir modifier ses propres DLL)
            if (pool != null)
            {
                var installDir = new DirectoryInfo(InstallPath);
                var installSecurity = installDir.GetAccessControl();
                installSecurity.PurgeAccessRules(pool);
                installDir.SetAccessControl(installSecurity);
            }
        }

        private void ConfigureIIS()
        {
            using var mgr = new ServerManager();
            string poolName = "Autoprint";
            string siteName = "Autoprint";
            int port = int.Parse(TxtWebPort.Text);

            var existingSite = mgr.Sites.FirstOrDefault(s => s.Name == siteName);
            if (existingSite != null) mgr.Sites.Remove(existingSite);

            var existingPool = mgr.ApplicationPools.FirstOrDefault(p => p.Name == poolName);
            if (existingPool != null) mgr.ApplicationPools.Remove(existingPool);

            mgr.CommitChanges();

            var newPool = mgr.ApplicationPools.Add(poolName);
            newPool.ManagedRuntimeVersion = "";
            // Compte automatique IIS (« IIS AppPool\Autoprint ») : le site n'a plus aucun privilège local (audit #8).
            // Les opérations sur les files d'impression passent par le service Windows AutoprintSpooler.
            newPool.ProcessModel.IdentityType = ProcessModelIdentityType.ApplicationPoolIdentity;
            newPool.ProcessModel.IdleTimeout = TimeSpan.Zero;
            newPool.ProcessModel.LoadUserProfile = true;

            var newSite = mgr.Sites.Add(siteName, InstallPath, port);
            newSite.ApplicationDefaults.ApplicationPoolName = poolName;

            mgr.CommitChanges();
        }

        /// <summary>
        /// Attend que Windows résolve le compte du pool (SQL Server le recherche par son nom pour CREATE LOGIN) ;
        /// juste après la création du pool, le nom peut être encore inconnu : démarrage du pool puis nouvelles tentatives.
        /// </summary>
        private static void WaitForPoolAccount()
        {
            for (int i = 0; i < 60; i++)
            {
                try { new NTAccount(PoolIdentity).Translate(typeof(SecurityIdentifier)); return; }
                catch (IdentityNotMappedException) { }
                if (i == 3)
                {
                    try
                    {
                        using var mgr = new ServerManager();
                        var pool = mgr.ApplicationPools.FirstOrDefault(p => p.Name == "Autoprint");
                        if (pool != null && pool.State != ObjectState.Started) pool.Start();
                    }
                    catch { }
                }
                System.Threading.Thread.Sleep(1000);
            }
        }

        private const string PoolIdentity = @"IIS AppPool\Autoprint";
        private const string FirewallRuleName = "Autoprint Server";

        /// <summary>
        /// Ouvre le port du site dans le pare-feu Windows (B52 : sans cette règle, la console n'était joignable que depuis le serveur).
        /// La règle est recréée à chaque installation ou mise à jour pour suivre le port configuré.
        /// </summary>
        private static void EnsureFirewallRule(int port)
        {
            if (port < 1) return;
            RunNetsh($"advfirewall firewall delete rule name=\"{FirewallRuleName}\"", ignoreErrors: true);
            RunNetsh($"advfirewall firewall add rule name=\"{FirewallRuleName}\" dir=in action=allow protocol=TCP localport={port} profile=any", ignoreErrors: false);
        }

        private static void RunNetsh(string args, bool ignoreErrors)
        {
            using var p = Process.Start(new ProcessStartInfo { FileName = "netsh.exe", Arguments = args, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })
                          ?? throw new Exception("netsh introuvable");
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0 && !ignoreErrors) throw new Exception($"Règle de pare-feu non créée (netsh {p.ExitCode}) : {output.Trim()}");
        }

        // Mise à jour : les anciennes versions faisaient tourner le pool en LocalSystem
        private void SwitchPoolToApplicationPoolIdentity()
        {
            using var mgr = new ServerManager();
            var pool = mgr.ApplicationPools.FirstOrDefault(p => p.Name == "Autoprint");
            if (pool == null) return;
            if (pool.ProcessModel.IdentityType != ProcessModelIdentityType.ApplicationPoolIdentity)
            {
                pool.ProcessModel.IdentityType = ProcessModelIdentityType.ApplicationPoolIdentity;
                mgr.CommitChanges();
            }
        }

        // Le service peut avoir démarré avant la création du pool : on le redémarre pour qu'il autorise immédiatement son identité
        private static void RestartSpoolerService()
        {
            try
            {
                using var sc = new System.ServiceProcess.ServiceController("AutoprintSpooler");
                if (sc.Status != System.ServiceProcess.ServiceControllerStatus.Stopped)
                {
                    sc.Stop();
                    sc.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }
                sc.Start();
                sc.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }
            catch (Exception ex)
            {
                throw new Exception($"Le service AutoprintSpooler n'a pas pu être démarré : {ex.Message}");
            }
        }

        /// <summary>
        /// Compte Windows sous lequel le site se connecte à SQL Server en authentification Windows :
        /// SQL sur ce serveur => identité du pool ; SQL sur une autre machine => compte machine de ce serveur (DOMAINE\SERVEUR$).
        /// </summary>
        private static string GetSqlLoginForSite(string connectionString)
        {
            var dataSource = new SqlConnectionStringBuilder(connectionString).DataSource ?? "";
            string host = dataSource.Replace("tcp:", "", StringComparison.OrdinalIgnoreCase).Split('\\', ',')[0].Trim();

            bool isLocal = host is "." or "" || host.StartsWith("(local", StringComparison.OrdinalIgnoreCase)
                           || host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1" || host == "::1"
                           || host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)
                           || host.StartsWith(Environment.MachineName + ".", StringComparison.OrdinalIgnoreCase);
            if (isLocal) return PoolIdentity;

            // Compte machine résolu par l'annuaire (nom NetBIOS du domaine), à défaut domaine de l'utilisateur courant
            try
            {
                var sid = (SecurityIdentifier)new NTAccount(Environment.MachineName + "$").Translate(typeof(SecurityIdentifier));
                return ((NTAccount)sid.Translate(typeof(NTAccount))).Value;
            }
            catch
            {
                return $"{Environment.UserDomainName}\\{Environment.MachineName}$";
            }
        }

        /// <summary>Donne au site l'accès db_owner à sa base (authentification Windows uniquement).</summary>
        private static async Task GrantSqlAccessToSite(string adminConnectionString, string databaseName, string login)
        {
            string safeLogin = login.Replace("]", "]]");
            string safeLoginLiteral = login.Replace("'", "''");
            string safeDb = databaseName.Replace("]", "]]");

            string sql = $@"
                USE [master];
                IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = N'{safeLoginLiteral}')
                    CREATE LOGIN [{safeLogin}] FROM WINDOWS;

                USE [{safeDb}];
                IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = N'{safeLoginLiteral}')
                    CREATE USER [{safeLogin}] FOR LOGIN [{safeLogin}];

                ALTER ROLE db_owner ADD MEMBER [{safeLogin}];
            ";

            using var connection = new SqlConnection(WithCatalog(adminConnectionString, "master"));
            await connection.OpenAsync();
            using var cmd = new SqlCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync();
        }

        private static string WithCatalog(string connectionString, string catalog) =>
            new SqlConnectionStringBuilder(connectionString) { InitialCatalog = catalog }.ConnectionString;

        /// <summary>Crée la base si elle n'existe pas (installation neuve sur un serveur SQL vierge), connexion par master.</summary>
        private static async Task EnsureSqlDatabase(string connectionString, string databaseName)
        {
            string safeDb = databaseName.Replace("]", "]]");
            using var connection = new SqlConnection(WithCatalog(connectionString, "master"));
            await connection.OpenAsync();
            using var cmd = new SqlCommand($"IF DB_ID(@db) IS NULL CREATE DATABASE [{safeDb}];", connection);
            cmd.Parameters.AddWithValue("@db", databaseName);
            await cmd.ExecuteNonQueryAsync();
        }

        // Mise à jour : droits SQL du nouveau compte du site, avec la chaîne de connexion existante (exécutée sous le compte de l'installeur)
        private async Task GrantSqlAccessForUpgrade()
        {
            string configPath = Path.Combine(InstallPath, "appsettings.json");
            var root = JsonNode.Parse(await File.ReadAllTextAsync(configPath));
            string provider = root?["Database"]?["Provider"]?.GetValue<string>() ?? "SqlServer";
            string? cs = root?["Database"]?["ConnectionStrings"]?["SqlServer"]?.GetValue<string>();
            if (!provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(cs)) return;

            var builder = new SqlConnectionStringBuilder(cs);
            if (!builder.IntegratedSecurity) return; // authentification SQL : rien à faire

            string login = GetSqlLoginForSite(cs);
            try
            {
                await GrantSqlAccessToSite(cs, builder.InitialCatalog, login);
            }
            catch (Exception ex)
            {
                _sqlGrantFailed = true;
                TxtInstallLog.Text += $"\n[Attention] Droits SQL à accorder manuellement à [{login}] (db_owner sur [{builder.InitialCatalog}]) : {ex.Message}";

                // Script fourni sous forme de fichier et dans le presse-papiers (avant : seulement lisible dans le message) (B73)
                string safeLogin = login.Replace("]", "]]");
                string safeDb = builder.InitialCatalog.Replace("]", "]]");
                string script =
                    $"-- Autoprint : accès du site web à sa base (à exécuter par un administrateur SQL sur {builder.DataSource})\r\n" +
                    $"USE [master];\r\nIF SUSER_ID(N'{login.Replace("'", "''")}') IS NULL CREATE LOGIN [{safeLogin}] FROM WINDOWS;\r\n" +
                    $"USE [{safeDb}];\r\nIF USER_ID(N'{login.Replace("'", "''")}') IS NULL CREATE USER [{safeLogin}] FOR LOGIN [{safeLogin}];\r\n" +
                    $"ALTER ROLE db_owner ADD MEMBER [{safeLogin}];\r\n";
                string scriptPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Autoprint-droits-SQL.sql");
                bool saved = false, copied = false;
                try { File.WriteAllText(scriptPath, script); saved = true; } catch { }
                try { Clipboard.SetText(script); copied = true; } catch { }

                MessageBox.Show(
                    "Le compte de l'installeur n'a pas pu donner l'accès à la base au site web.\n\n" +
                    "Un administrateur SQL doit exécuter le script suivant sur le serveur SQL" +
                    (saved ? $" (enregistré dans « {scriptPath} »" + (copied ? " et copié dans le presse-papiers)" : ")") : copied ? " (copié dans le presse-papiers)" : "") + " :\n\n" +
                    script + "\n" +
                    "La mise à jour continue ; le site appliquera lui-même les éventuelles mises à niveau de la base à son démarrage, une fois le script exécuté.",
                    "Droits SQL", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Mise à jour : passage en mode « Service » (l'ancienne configuration, conservée par la fusion, n'a pas cette clé)
        private async Task EnableSpoolerServiceMode()
        {
            string configPath = Path.Combine(InstallPath, "appsettings.json");
            var root = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!.AsObject();
            root["Spooler"] = new JsonObject { ["Mode"] = "Service" };
            await File.WriteAllTextAsync(configPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        private void BtnOpenIis_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "inetmgr.exe", UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur : " + ex.Message);
            }
        }

        private void MergeJsonNodes(JsonObject target, JsonObject source)
        {
            foreach (var property in source)
            {
                var key = property.Key;
                var sourceValue = property.Value;

                if (target.ContainsKey(key) && target[key] is JsonObject targetObj && sourceValue is JsonObject sourceObj)
                {
                    MergeJsonNodes(targetObj, sourceObj);
                }
                else
                {
                    target[key] = sourceValue?.DeepClone();
                }
            }
        }
        private void StopIISPool()
        {
            try
            {
                using var mgr = new ServerManager();
                var pool = mgr.ApplicationPools.FirstOrDefault(p => p.Name == "Autoprint");

                if (pool != null && (pool.State == ObjectState.Started || pool.State == ObjectState.Starting))
                {
                    pool.Stop();
                    System.Threading.Thread.Sleep(3000);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Impossible d'arrêter le pool (ou inexistant) : " + ex.Message);
            }
        }
    }
}