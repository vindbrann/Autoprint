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
        /// <summary>Version la plus récente du runtime ASP.NET Core 10 trouvée (null si absent).</summary>
        public Version? InstalledAspNetCoreVersion { get; set; }
        /// <summary>Version minimale exigée (= Hosting Bundle embarqué dans l'installeur).</summary>
        public Version RequiredAspNetCoreVersion { get; set; } = PrerequisiteService.RequiredAspNetCoreVersion;
        public bool IsServerOS { get; set; }

        public bool IsAllGood => IsIisInstalled && IsPrintServerRoleInstalled && IsNet10Installed;
    }

    internal static class PrerequisiteCheckResultExtensions
    {
        public static PrerequisiteCheckResult WithRuntimeStatus(this PrerequisiteCheckResult r)
        {
            r.IsNet10Installed = r.InstalledAspNetCoreVersion != null && r.InstalledAspNetCoreVersion >= r.RequiredAspNetCoreVersion;
            return r;
        }
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
                InstalledAspNetCoreVersion = FindAspNetCore10Runtime(),
            }.WithRuntimeStatus();
        }

        /// <summary>
        /// Version du Hosting Bundle embarqué (propriété HostingBundleVersion du projet). Un runtime plus ancien
        /// est considéré comme à mettre à jour : il ne contient pas les derniers correctifs de sécurité.
        /// </summary>
        public static Version RequiredAspNetCoreVersion { get; } = ReadRequiredVersion();

        private static Version ReadRequiredVersion()
        {
            var value = typeof(PrerequisiteService).Assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
                .OfType<System.Reflection.AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "HostingBundleVersion")?.Value;
            return Version.TryParse(value, out var v) ? v : new Version(10, 0, 0);
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


        /// <summary>Plus haute version « Microsoft.AspNetCore.App 10.x » listée par dotnet --list-runtimes.</summary>
        private Version? FindAspNetCore10Runtime()
        {
            try
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string dotnetPath = Path.Combine(programFiles, "dotnet", "dotnet.exe");

                if (!File.Exists(dotnetPath)) return null;

                var psi = new ProcessStartInfo
                {
                    FileName = dotnetPath,
                    Arguments = "--list-runtimes",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return null;

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return ParseHighestAspNetCore10(output);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Ex. « Microsoft.AspNetCore.App 10.0.14 [C:\Program Files\dotnet\...] » ; les préversions sont ignorées.</summary>
        internal static Version? ParseHighestAspNetCore10(string listRuntimesOutput)
        {
            Version? best = null;
            foreach (var line in listRuntimesOutput.Split('\n'))
            {
                var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || parts[0] != "Microsoft.AspNetCore.App") continue;
                if (!Version.TryParse(parts[1], out var v) || v.Major != 10) continue;
                if (best == null || v > best) best = v;
            }
            return best;
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