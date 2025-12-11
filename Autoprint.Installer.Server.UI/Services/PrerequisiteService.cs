using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

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
                IsNet10Installed = CheckDotNet10Runtime()
            };
        }

        private bool IsWindowsServerEdition()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ProductType FROM Win32_OperatingSystem");
                foreach (ManagementObject os in searcher.Get())
                {
                    var type = uint.Parse(os["ProductType"].ToString()!);
                    return type != 1; 
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


        private bool CheckDotNet10Runtime()
        {
            try
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string dotnetPath = Path.Combine(programFiles, "dotnet", "dotnet.exe");

                if (!File.Exists(dotnetPath)) return false;

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