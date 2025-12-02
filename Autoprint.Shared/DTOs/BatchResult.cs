namespace Autoprint.Shared.DTOs
{
    public class BatchResult
    {
        // --- PROPRIÉTÉS EXISTANTES (Compatibilité Import) ---
        public int TotalTraites { get; set; }
        public int SuccesBdd { get; set; }
        public int SuccesSysteme { get; set; }
        public int Erreurs { get; set; }
        public List<string> DetailsErreurs { get; set; } = new List<string>();

        // --- PROPRIÉTÉS POUR LA SYNCHRO ---
        public bool Success { get; set; } = true;
        public List<string> Messages { get; set; } = new List<string>();

        // >>> AJOUTS POUR LA SYNCHRO PILOTES<<<
        public int Added { get; set; }
        public int Updated { get; set; }
        public int Deleted { get; set; }
    }

    // On déplace aussi cette classe qui était souvent dans le même fichier
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