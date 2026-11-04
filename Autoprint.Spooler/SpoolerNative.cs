using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Autoprint.Spooler
{
    /// <summary>Déclarations Win32 du spouleur, partagées par le serveur (lectures) et le service AutoprintSpooler (écritures).</summary>
    [SupportedOSPlatform("windows")]
    public static class SpoolerNative
    {
        public const int PRINTER_ATTRIBUTE_QUEUED = 0x00000001;
        public const int PRINTER_ATTRIBUTE_DIRECT = 0x00000002;
        public const int PRINTER_ACCESS_ADMINISTER = 0x00000004;
        public const int PRINTER_ACCESS_USE = 0x00000008;
        public const int STANDARD_RIGHTS_REQUIRED = 0x000F0000;
        public const int PRINTER_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | PRINTER_ACCESS_ADMINISTER | PRINTER_ACCESS_USE);
        public const int PRINTER_ENUM_LOCAL = 0x00000002;
        public const uint PRINTER_ATTRIBUTE_SHARED = 0x00000008;

        public const uint REG_DWORD = 4;
        public const int ERROR_SUCCESS = 0;
        public const int ERROR_INSUFFICIENT_BUFFER = 122;


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct PRINTER_INFO_2
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pServerName;
            [MarshalAs(UnmanagedType.LPStr)] public string pPrinterName;
            [MarshalAs(UnmanagedType.LPStr)] public string pShareName;
            [MarshalAs(UnmanagedType.LPStr)] public string pPortName;
            [MarshalAs(UnmanagedType.LPStr)] public string pDriverName;
            [MarshalAs(UnmanagedType.LPStr)] public string pComment;
            [MarshalAs(UnmanagedType.LPStr)] public string pLocation;
            public IntPtr pDevMode;
            [MarshalAs(UnmanagedType.LPStr)] public string pSepFile;
            [MarshalAs(UnmanagedType.LPStr)] public string pPrintProcessor;
            [MarshalAs(UnmanagedType.LPStr)] public string pDatatype;
            [MarshalAs(UnmanagedType.LPStr)] public string pParameters;
            public IntPtr pSecurityDescriptor;
            public uint Attributes;
            public uint Priority;
            public uint DefaultPriority;
            public uint StartTime;
            public uint UntilTime;
            public uint Status;
            public uint cJobs;
            public uint AveragePPM;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PRINTER_DEFAULTS
        {
            public IntPtr pDatatype;
            public IntPtr pDevMode;
            public int DesiredAccess;
        }

        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, ref PRINTER_DEFAULTS pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern bool GetPrinter(IntPtr hPrinter, int Level, IntPtr pPrinter, int cbBuf, out int pcbNeeded);

        [DllImport("winspool.drv", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern bool SetPrinter(IntPtr hPrinter, int Level, IntPtr pPrinter, int Command);

        [DllImport("winspool.drv", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern bool EnumPrinters(int Flags, string? Name, int Level, IntPtr pPrinterEnum, int cbBuf, out int pcbNeeded, out int pcReturned);

        [DllImport("winspool.drv", EntryPoint = "SetPrinterDataExA", SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern uint SetPrinterDataEx(IntPtr hPrinter, string pKeyName, string pValueName, uint Type, ref int pData, int cbData);

        [DllImport("winspool.drv", EntryPoint = "GetPrinterDataExA", SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern uint GetPrinterDataEx(IntPtr hPrinter, string pKeyName, string pValueName, out uint pType, out int pData, int nndSize, out int pcbNeeded);
    }
}
