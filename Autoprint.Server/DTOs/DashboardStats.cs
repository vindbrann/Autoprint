namespace Autoprint.Server.DTOs
{
    public class DashboardStats
    {
        public int TotalImprimantes { get; set; }
        public int TotalLieux { get; set; }
        public int ImprimantesEnErreur { get; set; } // Basé sur les logs récents
        public int TotalPilotes { get; set; }

        // On pourra ajouter des listes plus tard (ex: "Top 5 des erreurs")
    }
}