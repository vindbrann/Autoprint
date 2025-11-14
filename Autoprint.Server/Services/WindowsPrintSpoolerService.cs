using System.Management; // Nécessaire pour WMI
using System.Runtime.Versioning; // Pour l'attribut [SupportedOSPlatform]

namespace Autoprint.Server.Services
{
    // On précise que cette classe ne marche QUE sur Windows
    [SupportedOSPlatform("windows")]
    public class WindowsPrintSpoolerService : IPrintSpoolerService
    {
        public bool ImprimanteExiste(string nomImprimante)
        {
            string query = $"SELECT * FROM Win32_Printer WHERE Name = '{nomImprimante}'";
            using (var searcher = new ManagementObjectSearcher(query))
            {
                return searcher.Get().Count > 0;
            }
        }

        public void CreerPortTcp(string nomPort, string adresseIp)
        {
            // 1. Vérifier si le port existe déjà pour éviter une erreur
            string query = $"SELECT * FROM Win32_TCPIPPrinterPort WHERE Name = '{nomPort}'";
            using (var searcher = new ManagementObjectSearcher(query))
            {
                if (searcher.Get().Count > 0) return; // Il existe déjà, on ne fait rien
            }

            // 2. Création du port via WMI
            var processClass = new ManagementClass("Win32_TCPIPPrinterPort");
            var portObj = processClass.CreateInstance();

            portObj["Name"] = nomPort;
            portObj["HostAddress"] = adresseIp;
            portObj["Protocol"] = 1; // 1 = RAW, 2 = LPR
            portObj["PortNumber"] = 9100; // Standard RAW port
            portObj["SNMPEnabled"] = false; // On désactive souvent SNMP pour éviter des lenteurs

            portObj.Put(); // Valide la création dans Windows
        }

        public void CreerImprimante(string nom, string nomDriver, string nomPort, string commentaire, string nomPartage)
        {
            if (ImprimanteExiste(nom)) return;

            var printerClass = new ManagementClass("Win32_Printer");
            var printerObj = printerClass.CreateInstance();

            printerObj["Name"] = nom;
            printerObj["DriverName"] = nomDriver; // Doit correspondre EXACTEMENT au nom du pilote installé
            printerObj["PortName"] = nomPort;
            printerObj["DeviceID"] = nom;
            printerObj["Comment"] = commentaire;

            // Gestion du partage
            if (!string.IsNullOrEmpty(nomPartage))
            {
                printerObj["Shared"] = true;
                printerObj["ShareName"] = nomPartage;
            }

            printerObj.Put(); // Crée l'imprimante
        }

        public void SupprimerImprimante(string nom)
        {
            string query = $"SELECT * FROM Win32_Printer WHERE Name = '{nom}'";
            using (var searcher = new ManagementObjectSearcher(query))
            {
                foreach (ManagementObject printer in searcher.Get())
                {
                    printer.Delete(); // Supprime de Windows
                }
            }
        }
    }
}