using Autoprint.Server.Data;
using Autoprint.Shared;
using System.Text.RegularExpressions;

namespace Autoprint.Server.Services
{
    public class NamingService : INamingService
    {
        private readonly ApplicationDbContext _context;

        public NamingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenererNomAsync(Imprimante imp)
        {
            var setting = await _context.ServerSettings.FindAsync("NamingTemplate");
            string template = setting?.Value ?? "IMP_{IP}";

            return GenererNom(imp, template);
        }

        public string GenererNom(Imprimante imp, string template)
        {
            if (string.IsNullOrEmpty(template)) return imp.NomAffiche;

            string resultat = template;

            string lieu = imp.Emplacement?.Nom ?? "Unknown";
            string lieuCode = imp.Emplacement?.Code ?? "000";

            resultat = resultat.Replace("{LIEU}", NettoyerChaine(lieu), StringComparison.OrdinalIgnoreCase);
            resultat = resultat.Replace("{LIEU_CODE}", NettoyerChaine(lieuCode), StringComparison.OrdinalIgnoreCase);

            string marque = imp.Modele?.Marque?.Nom ?? "Generic";
            resultat = resultat.Replace("{MARQUE}", NettoyerChaine(marque), StringComparison.OrdinalIgnoreCase);

            string modele = imp.Modele?.Nom ?? "Device";
            resultat = resultat.Replace("{MODELE}", NettoyerChaine(modele), StringComparison.OrdinalIgnoreCase);

            string ip = imp.AdresseIp ?? "0.0.0.0";
            resultat = resultat.Replace("{IP}", ip, StringComparison.OrdinalIgnoreCase);

            string ipLast = "0";
            var segments = ip.Split('.');
            if (segments.Length == 4) ipLast = segments[3];
            resultat = resultat.Replace("{IP_LAST}", ipLast, StringComparison.OrdinalIgnoreCase);

            string impCode = imp.Code ?? "000";
            resultat = resultat.Replace("{IMP_CODE}", NettoyerChaine(impCode), StringComparison.OrdinalIgnoreCase);

            return resultat.ToUpper();
        }

        private string NettoyerChaine(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            string clean = input.Trim().Replace(" ", "_");
            clean = Regex.Replace(clean, "[^a-zA-Z0-9_]", "");
            return clean;
        }
    }
}