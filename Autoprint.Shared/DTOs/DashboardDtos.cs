namespace Autoprint.Shared.DTOs
{
    public class ChartDataDto
    {
        public string Label { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    public class DashboardStatsDto
    {
        public int TotalImprimantes { get; set; }
        public int TotalPilotes { get; set; }
        public int TotalLieux { get; set; }

        public List<ChartDataDto> RepartitionModeles { get; set; } = new();
        public bool IsSpoolerRunning { get; set; }
        public int SyncErrorCount { get; set; }
        public string ServerVersion { get; set; } = "1.0.0";
    }
}