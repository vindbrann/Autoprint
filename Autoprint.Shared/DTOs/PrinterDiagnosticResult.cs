using System.Collections.Generic;

namespace Autoprint.Shared.DTOs
{
    public class PrinterDiagnosticResult
    {
        public bool PingSuccess { get; set; }
        public long PingRoundtripTimeMs { get; set; }
        public string Status { get; set; } = "Inconnu";
        public List<PrinterAlert> Alerts { get; set; } = new List<PrinterAlert>();
        public long UptimeSeconds { get; set; }
        public long PageCounter { get; set; }
        /// <summary>Consommables : toners (Kind = Toner) et éléments de maintenance (unité de fusion, photoconducteur…).</summary>
        public List<TonerLevelResult> Toners { get; set; } = new List<TonerLevelResult>();
        public List<PaperTrayResult> Trays { get; set; } = new List<PaperTrayResult>();
        public string? SerialNumber { get; set; }
        public bool HasSnmpProfile { get; set; } = true;
        /// <summary>Seuil « bas » appliqué (en %), pour l'affichage.</summary>
        public int LowThresholdPercent { get; set; } = 20;
        /// <summary>Délai d'approvisionnement appliqué (en jours), pour l'affichage (#51).</summary>
        public int SupplyLeadTimeDays { get; set; } = 14;
    }

    /// <summary>État d'un consommable ou d'un bac, indépendant du niveau (#49) : beaucoup d'imprimantes ne donnent qu'un état.</summary>
    public enum SupplyState
    {
        Unknown = 0,
        Ok = 1,
        Low = 2,
        Empty = 3
    }

    public enum SupplyKind
    {
        Toner = 0,
        Maintenance = 1
    }

    /// <summary>Fiabilité d'une prévision (#51) : selon la baisse observée et la durée des relevés.</summary>
    public enum ForecastConfidence
    {
        None = 0,
        Low = 1,
        Medium = 2,
        Good = 3
    }

    public enum AlertSeverity
    {
        Info = 0,
        Warning = 1,
        Critical = 2
    }

    public class PrinterAlert
    {
        public AlertSeverity Severity { get; set; } = AlertSeverity.Info;
        public string Text { get; set; } = string.Empty;

        public PrinterAlert() { }
        public PrinterAlert(AlertSeverity severity, string text) { Severity = severity; Text = text; }

        public override string ToString() => Text;
    }

    public class TonerLevelResult
    {
        public string Color { get; set; } = "Noir";
        /// <summary>Niveau en %, ou -1 si l'imprimante ne le communique pas (voir State).</summary>
        public int CurrentLevel { get; set; } = -1;
        public int MaxCapacity { get; set; }
        public int? EstimatedDaysRemaining { get; set; }
        public string ColorHex { get; set; } = "#212529";
        public SupplyState State { get; set; } = SupplyState.Unknown;
        public SupplyKind Kind { get; set; } = SupplyKind.Toner;
        /// <summary>Clé stable de l'élément dans la Printer MIB (« supply:1.2 »), vide pour un élément de profil manuel (#50).</summary>
        public string? Key { get; set; }
        /// <summary>
        /// Réceptacle qui se remplit (bac de récupération de toner…) : le niveau est l'espace restant (RFC 3805),
        /// Low = presque plein, Empty = plein.
        /// </summary>
        public bool IsReceptacle { get; set; }

        // #51 : prévision et approvisionnement
        public ForecastConfidence ForecastConfidence { get; set; } = ForecastConfidence.None;
        /// <summary>« pages » (consommation par page imprimée) ou « temps » (faute de compteur de pages).</summary>
        public string? ForecastMethod { get; set; }
        /// <summary>Date depuis laquelle l'élément est Bas / Vide (consommable sans niveau notamment).</summary>
        public DateTime? LowSince { get; set; }
        /// <summary>Durée moyenne observée entre deux changements de ce consommable.</summary>
        public int? AverageLifetimeDays { get; set; }
        /// <summary>À commander : Bas / Vide, ou fin prévue dans moins que le délai d'approvisionnement.</summary>
        public bool NeedsOrder { get; set; }
        /// <summary>Date du dernier changement détecté de ce consommable (#52).</summary>
        public DateTime? LastReplacedAt { get; set; }

        public bool HasLevel => CurrentLevel >= 0;
        public bool NeedsAttention => State is SupplyState.Low or SupplyState.Empty;
    }

    /// <summary>Ligne de la vue « Consommables à commander » (#52).</summary>
    public class SupplyToOrderDto
    {
        public int ImprimanteId { get; set; }
        public string Imprimante { get; set; } = string.Empty;
        public string? Lieu { get; set; }
        public string? Modele { get; set; }
        public string? AdresseIp { get; set; }
        public string Consommable { get; set; } = string.Empty;
        public SupplyKind Kind { get; set; }
        public int Level { get; set; } = -1;
        public SupplyState State { get; set; }
        public bool IsReceptacle { get; set; }
        public int? DaysRemaining { get; set; }
        public ForecastConfidence Confidence { get; set; }
        public DateTime? LowSince { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PaperTrayResult
    {
        public string Name { get; set; } = "Bac 1";
        /// <summary>Niveau en % (ou en feuilles au-delà de 100), -1 si inconnu (voir State).</summary>
        public int CurrentLevel { get; set; } = -1;
        public int MaxCapacity { get; set; }
        public SupplyState State { get; set; } = SupplyState.Unknown;
        /// <summary>Clé stable du bac dans la Printer MIB (« input:1.2 »), vide pour un élément de profil manuel (#50).</summary>
        public string? Key { get; set; }

        public bool HasLevel => CurrentLevel >= 0;
    }

    public class SnmpScanRequestDto
    {
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; } = 161;
        public string Community { get; set; } = "public";
        public int Version { get; set; } = 2; // 1 = v1, 2 = v2c
        // Si renseigné, le profil d'identifiants SNMP enregistré est utilisé à la place de Community / Version
        public int? CredentialId { get; set; }
        // Profil en cours de modification : sa communauté enregistrée sert si Community est vide (#40)
        public int? ProfileId { get; set; }
        public List<string> CustomOidBranches { get; set; } = new List<string>();
        public bool ScanEnterpriseMib { get; set; } = false;
        public List<SnmpProfileItem>? ItemsToTest { get; set; }
    }

    public class DiscoveredOidDto
    {
        public string Oid { get; set; } = string.Empty;
        public string? OidMaxCapacity { get; set; }
        public string Value { get; set; } = string.Empty;
        public string ValueType { get; set; } = string.Empty;
        public string SuggestedName { get; set; } = string.Empty;
        public SnmpItemCategory SuggestedCategory { get; set; } = SnmpItemCategory.Status;
        public bool IsRelevant { get; set; } = true;
    }

    public class SnmpTestProfileRequestDto
    {
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; } = 161;
        public string Community { get; set; } = "public";
        public int Version { get; set; } = 2;
        public int? CredentialId { get; set; }
        public int? ProfileId { get; set; }
        public List<SnmpProfileItem> Items { get; set; } = new();
    }

    public class SnmpTestProfileResultDto
    {
        public string Oid { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool Success { get; set; }
    }
}

