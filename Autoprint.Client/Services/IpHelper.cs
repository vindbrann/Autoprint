using System;
using System.Net;

namespace Autoprint.Client.Services
{
    public static class IpHelper
    {
        public static bool IsIpInCidr(string ipAddress, string cidr)
        {
            if (string.IsNullOrEmpty(ipAddress) || string.IsNullOrEmpty(cidr)) return false;

            try
            {
                string[] parts = cidr.Split('/');
                if (parts.Length != 2) return false;

                string cidrIp = parts[0];
                if (!int.TryParse(parts[1], out int cidrMaskLength)) return false;

                if (!IPAddress.TryParse(ipAddress, out IPAddress? clientIp) ||
                    !IPAddress.TryParse(cidrIp, out IPAddress? networkIp))
                {
                    return false;
                }

                if (clientIp.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
                    networkIp.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return false;
                }

                byte[] clientBytes = clientIp.GetAddressBytes();
                byte[] networkBytes = networkIp.GetAddressBytes();

                for (int i = 0; i < 4; i++)
                {
                    byte mask = 0;

                    if (cidrMaskLength >= 8)
                    {
                        mask = 255;
                        cidrMaskLength -= 8;
                    }
                    else if (cidrMaskLength > 0)
                    {
                        mask = (byte)(255 << (8 - cidrMaskLength));
                        cidrMaskLength = 0;
                    }
                    else
                    {
                        mask = 0;
                    }

                    if ((clientBytes[i] & mask) != (networkBytes[i] & mask))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}