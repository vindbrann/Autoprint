using System;
using System.Runtime.InteropServices;
using System.Text;

public static class MsiHelper
{
    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    private static extern uint MsiOpenPackage(string szPackagePath, out IntPtr hProduct);

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    private static extern uint MsiGetProductProperty(IntPtr hProduct, string szProperty, StringBuilder lpValueBuf, ref int pcchValueBuf);

    [DllImport("msi.dll")]
    private static extern uint MsiCloseHandle(IntPtr hAny);

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    private static extern int MsiEnumRelatedProducts(string lpUpgradeCode, int reserved, int iProductIndex, StringBuilder lpProductBuf);

    public static string GetUpgradeCodeFromFile(string msiPath)
    {
        IntPtr hProduct = IntPtr.Zero;
        try
        {
            if (MsiOpenPackage(msiPath, out hProduct) != 0) return null;

            StringBuilder sb = new StringBuilder(39);
            int capacity = sb.Capacity;

            if (MsiGetProductProperty(hProduct, "UpgradeCode", sb, ref capacity) == 0)
            {
                return sb.ToString();
            }
            return null;
        }
        finally
        {
            if (hProduct != IntPtr.Zero) MsiCloseHandle(hProduct);
        }
    }

    public static bool IsSoftwareInstalled(string upgradeCode)
    {
        if (string.IsNullOrEmpty(upgradeCode)) return false;
        StringBuilder sb = new StringBuilder(39);
        return MsiEnumRelatedProducts(upgradeCode, 0, 0, sb) == 0;
    }
}