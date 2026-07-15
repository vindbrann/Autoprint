using System;
using System.Collections.Generic;
using System.Linq;
using Autoprint.Shared;

namespace Autoprint.Server.Services
{
    public class PredictiveService : IPredictiveService
    {
        public int? PredictDaysRemaining(List<TonerHistory> history)
        {
            if (history == null || history.Count < 2)
            {
                return null;
            }

            var entries = history.OrderBy(h => h.RecordedAt).ToList();

            int startIndex = 0;
            for (int i = 1; i < entries.Count; i++)
            {
                if (entries[i].LevelPercent > entries[i - 1].LevelPercent)
                {
                    startIndex = i;
                }
            }

            var filteredEntries = entries.Skip(startIndex).ToList();
            if (filteredEntries.Count < 2)
            {
                return null;
            }

            double sumX = 0;
            double sumY = 0;
            double sumXY = 0;
            double sumXX = 0;
            int n = filteredEntries.Count;

            var t0 = filteredEntries[0].RecordedAt;
            foreach (var entry in filteredEntries)
            {
                double x = (entry.RecordedAt - t0).TotalDays;
                double y = entry.LevelPercent;

                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumXX += x * x;
            }

            double denominator = (n * sumXX - sumX * sumX);
            if (Math.Abs(denominator) < 0.0001)
            {
                return -99;
            }

            double slope = (n * sumXY - sumX * sumY) / denominator;

            if (slope >= 0)
            {
                return -99;
            }

            double currentLevel = filteredEntries.Last().LevelPercent;
            double remainingDays = -currentLevel / slope;

            return (int)Math.Max(0, Math.Round(remainingDays));
        }
    }
}
