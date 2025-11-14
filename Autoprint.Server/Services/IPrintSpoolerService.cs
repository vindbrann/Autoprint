namespace Autoprint.Server.Services
{
    public interface IPrintSpoolerService
    {
        // Vérifie si une imprimante existe déjà sur le serveur Windows
        bool ImprimanteExiste(string nomImprimante);

        // Crée un Port TCP/IP Standard (ex: 192.168.1.50)
        void CreerPortTcp(string nomPort, string adresseIp);

        // Crée l'imprimante partagée liée à un pilote et un port
        void CreerImprimante(string nom, string nomDriver, string nomPort, string commentaire, string nomPartage);

        // Supprime une imprimante
        void SupprimerImprimante(string nom);
    }
}