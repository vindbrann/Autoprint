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

namespace Autoprint.Installer.Server.UI
{
    public partial class MainWindow : Window
    {
        private int _currentStep = 1;
        private readonly PrerequisiteService _prereqService;
        private bool _prereqsValidated = false;

        private const string InstallPath = @"C:\Program Files\Autoprint Server";

        private const string NamespacePrefix = "Autoprint.Installer.Server.UI.Resources.";
        private const string MsiResourceName = NamespacePrefix + "Autoprint.Server.Setup.msi";
        private const string LicenseResourceName = NamespacePrefix + "License.rtf";
        private const string DotNetResourceName = NamespacePrefix + "dotnet-hosting-10.exe";

        public MainWindow()
        {
            InitializeComponent();
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
            catch { /* Ignorer erreur RTF */ }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateCurrentStep()) return;

            if (_currentStep == 5)
            {
                _currentStep = 6;
                UpdateView();
                _ = RunInstallationSequence();
                return;
            }

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
            if (_currentStep == 5 && (!int.TryParse(TxtWebPort.Text, out int p) || p < 1))
            {
                MessageBox.Show("Port Web invalide.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
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

                MessageBox.Show("Runtime .NET installé.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private string GetSqlConnectionString(int timeout = 30)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = TxtSqlServer.Text,
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
                string cnx = GetSqlConnectionString(3);
                await Task.Run(() => { using var c = new SqlConnection(cnx); c.Open(); });
                MessageBox.Show("Connexion réussie !", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
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
                TxtInstallLog.Text = "Extraction des fichiers...";
                string msi = Path.Combine(Path.GetTempPath(), "AutoprintSetup.msi");
                await ExtractResource(MsiResourceName, msi);

                TxtInstallLog.Text = "Installation des composants (MSI)...";
                await RunProcess("msiexec.exe", $"/i \"{msi}\" /qn /norestart");

                TxtInstallLog.Text = "Configuration des droits (NTFS)...";
                await Task.Run(() => ConfigureNtfsPermissions());

                TxtInstallLog.Text = "Configuration Application (JSON)...";
                await ConfigureAppSettings();

                TxtInstallLog.Text = "Configuration IIS...";
                ConfigureIIS();

                _currentStep = 7;
                UpdateView();
            }
            catch (Exception ex)
            {
                TxtInstallLog.Text = "ERREUR : " + ex.Message; // On verra ici où ça plante
                TxtInstallLog.Foreground = Brushes.Red;
                PrgInstall.IsIndeterminate = false;
                BtnNext.Visibility = Visibility.Visible;
                BtnNext.Content = "Quitter";
                BtnNext.IsEnabled = true;
                BtnNext.Click += (s, e) => Close();
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
                p.WaitForExit();
                if (p.ExitCode != 0) throw new Exception($"Erreur Code {p.ExitCode} pour {exe}");
            });
        }

        private async Task ConfigureAppSettings()
        {
            string path = Path.Combine(InstallPath, "appsettings.json");

            for (int i = 0; i < 10; i++) { if (File.Exists(path)) break; await Task.Delay(500); }

            if (!File.Exists(path)) throw new FileNotFoundException($"Fichier de config introuvable : {path}");

            string jsonContent = await File.ReadAllTextAsync(path);
            var root = JsonNode.Parse(jsonContent);

            if (root == null) return;

            string provider = "";
            string connectionString = "";

            if (RadioSqlServer.IsChecked == true)
            {
                provider = "SqlServer";
                connectionString = GetSqlConnectionString();
            }
            else
            {
                provider = "Sqlite";
                connectionString = "Data Source=Autoprint.db";
            }

            if (root["Database"] is JsonObject dbSection)
            {
                dbSection["Provider"] = provider;
            }
            else
            {
                root["Database"] = new JsonObject { ["Provider"] = provider };
            }

            if (root["ConnectionStrings"] is not JsonObject connSection)
            {
                connSection = new JsonObject();
                root["ConnectionStrings"] = connSection;
            }
            root["ConnectionStrings"]!["DefaultConnection"] = connectionString;

            if (root["DatabaseProvider"] != null) root.AsObject().Remove("DatabaseProvider");

            if (root["ClientUrl"] == null) root["ClientUrl"] = "";
            root["ClientUrl"] = $"http://localhost:{TxtWebPort.Text}";

            if (root["Jwt"] is not JsonObject jwtSection)
            {
                jwtSection = new JsonObject();
                root["Jwt"] = jwtSection;
                jwtSection["Issuer"] = "AutoprintServer";
                jwtSection["Audience"] = "AutoprintClient";
                jwtSection["ExpireMinutes"] = 120;
            }

            string secretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            root["Jwt"]!["Key"] = secretKey;

            await File.WriteAllTextAsync(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        private void ConfigureNtfsPermissions()
        {
            try
            {
                var dirInfo = new DirectoryInfo(InstallPath);

                var security = dirInfo.GetAccessControl();

                var networkService = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);

                var rule = new FileSystemAccessRule(
                    networkService,
                    FileSystemRights.Modify,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow);

                security.AddAccessRule(rule);

                dirInfo.SetAccessControl(security);
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors de l'application des droits NTFS : {ex.Message}");
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

            newPool.ProcessModel.IdentityType = ProcessModelIdentityType.LocalSystem;

            newPool.ProcessModel.IdleTimeout = TimeSpan.Zero;

            var newSite = mgr.Sites.Add(siteName, InstallPath, port);
            newSite.ApplicationDefaults.ApplicationPoolName = poolName;

            mgr.CommitChanges();
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
    }
}