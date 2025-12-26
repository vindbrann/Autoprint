using System;

namespace Autoprint.Shared
{
    [Flags]
    public enum ScanDays
    {
        None = 0,
        Monday = 1,
        Tuesday = 2,
        Wednesday = 4,
        Thursday = 8,
        Friday = 16,
        Saturday = 32,
        Sunday = 64,
        EveryDay = 127
    }
}