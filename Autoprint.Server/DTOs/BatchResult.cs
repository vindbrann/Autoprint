namespace Autoprint.Server.DTOs
{
    public class BatchResult
    {
        public int TotalTraites { get; set; }
        public int SuccesBdd { get; set; }
        public int SuccesSysteme { get; set; }
        public int Erreurs { get; set; }
        public List<string> DetailsErreurs { get; set; } = new List<string>();
    }

    // Cet objet sert à recevoir une liste d'imprimantes à créer sans l'ID (puisque c'est nouveau)
    public class ImprimanteDto
    {
        public string NomAffiche { get; set; } = string.Empty;
        public string AdresseIp { get; set; } = string.Empty;
        public bool EstPartagee { get; set; }
        public string? NomPartage { get; set; }
        public string? Commentaire { get; set; }
        public bool EstParDefaut { get; set; }
        public int EmplacementId { get; set; }
        public string? Localisation { get; set; }
        public int ModeleId { get; set; }
        public int PiloteId { get; set; }
    }
}