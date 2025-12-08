using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text; // Nécessaire pour StringBuilder

namespace Autoprint.Installer.Server.UI.Services
{
    public class PrerequisiteCheckResult
    {
        public bool IsIisInstalled { get; set; }
        public bool IsPrintServerRoleInstalled { get; set; }
        public bool IsNet10Installed { get; set; }
        public bool IsServerOS { get; set; }

        public bool IsAllGood => IsIisInstalled && IsPrintServerRoleInstalled && IsNet10Installed;
    }

    public class PrerequisiteService
    {
        private readonly bool _isServerOs;

        public PrerequisiteService()
        {
            _isServerOs = IsWindowsServerEdition();
        }

        public PrerequisiteCheckResult CheckSystem()
        {
            return new PrerequisiteCheckResult
            {
                IsServerOS = _isServerOs,
                IsIisInstalled = CheckIis(),
                IsPrintServerRoleInstalled = CheckPrintServer(),
                // Nouvelle méthode de détection infaillible
                IsNet10Installed = CheckDotNet10Runtime()
            };
        }

        // ... (Méthodes IsWindowsServerEdition, CheckIis, CheckPrintServer, Install... INCHANGÉES) ...
        // Je remets les méthodes pour avoir le fichier complet et éviter les erreurs de copier-coller

        private bool IsWindowsServerEdition()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ProductType FROM Win32_OperatingSystem");
                foreach (ManagementObject os in searcher.Get())
                {
                    var type = uint.Parse(os["ProductType"].ToString()!);
                    return type != 1; // 1 = Client, Autres = Serveur
                }
                return false;
            }
            catch { return RuntimeInformation.OSDescription.Contains("Server"); }
        }

        private bool CheckIis()
        {
            if (_isServerOs) return CheckWmiServerFeature(2);
            return CheckWmiOptionalFeature("IIS-WebServerRole");
        }

        private bool CheckPrintServer()
        {
            if (_isServerOs) return CheckWmiServerFeature(7);
            return CheckWmiOptionalFeature("Printing-Foundation-Features");
        }

        public async Task InstallIis()
        {
            string cmd = _isServerOs
                ? "Install-WindowsFeature -Name Web-Server -IncludeManagementTools -Verbose"
                : "Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole -All -Verbose";
            await RunPowershell(cmd);
        }

        public async Task InstallPrintServer()
        {
            string cmd = _isServerOs
                ? "Install-WindowsFeature -Name Print-Server -IncludeManagementTools -Verbose"
                : "Add-WindowsCapability -Online -Name Print.Management.Console~~~~0.0.1.0";
            await RunPowershell(cmd);
        }

        // --- NOUVELLE DÉTECTION .NET (The Truth) ---

        private bool CheckDotNet10Runtime()
        {
            try
            {
                // 1. On cherche l'exécutable au chemin standard (Program Files)
                // C'est mieux que le PATH car le PATH n'est pas mis à jour pour le process en cours
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string dotnetPath = Path.Combine(programFiles, "dotnet", "dotnet.exe");

                if (!File.Exists(dotnetPath)) return false;

                // 2. On interroge le moteur : "Quels runtimes as-tu ?"
                var psi = new ProcessStartInfo
                {
                    FileName = dotnetPath,
                    Arguments = "--list-runtimes",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // 3. On analyse la réponse
                // On cherche "Microsoft.AspNetCore.App 10." (C'est le module IIS Server)
                // On ne cherche pas juste "Microsoft.NETCore.App" qui est la base.
                if (output.Contains("Microsoft.AspNetCore.App 10."))
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        // --- HELPERS (Inchangés) ---

        private bool CheckWmiServerFeature(uint featureId)
        {
            try
            {
                var scope = new ManagementScope(@"\\.\root\cimv2");
                scope.Connect();
                var query = new ObjectQuery($"SELECT ID FROM Win32_ServerFeature WHERE ID = {featureId}");
                using var searcher = new ManagementObjectSearcher(scope, query);
                return searcher.Get().Count > 0;
            }
            catch { return false; }
        }

        private bool CheckWmiOptionalFeature(string featureName)
        {
            try
            {
                var scope = new ManagementScope(@"\\.\root\cimv2");
                scope.Connect();
                var query = new ObjectQuery($"SELECT Name FROM Win32_OptionalFeature WHERE Name = '{featureName}' AND InstallState = 1");
                using var searcher = new ManagementObjectSearcher(scope, query);
                return searcher.Get().Count > 0;
            }
            catch { return false; }
        }

        private async Task RunPowershell(string command)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}; Start-Sleep -Seconds 2\"",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };
            var p = Process.Start(psi);
            if (p != null) await p.WaitForExitAsync();
        }
    }
}