namespace Autoprint.Server.Services
{
    // Cette classe sert de "bouchon" pour les environnements non-Windows ou de test
    public class StubPrintSpoolerService : IPrintSpoolerService
    {
        public void CreerImprimante(string nom, string nomDriver, string nomPort, string commentaire, string nomPartage)
        {
            // On ne fait rien (simulation)
        }

        public void CreerPortTcp(string nomPort, string adresseIp)
        {
            // On ne fait rien
        }

        public bool ImprimanteExiste(string nomImprimante)
        {
            return false; // On simule qu'elle n'existe pas pour forcer la création (virtuelle)
        }

        public void SupprimerImprimante(string nom)
        {
            // On ne fait rien
        }
    }
}