using System;
using System.Net;

namespace Autoprint.Client.Services
{
    public static class IpHelper
    {
        /// <summary>
        /// Vérifie si une adresse IP appartient à un sous-réseau CIDR (ex: 192.168.1.5 dans 192.168.1.0/24)
        /// </summary>
        public static bool IsIpInCidr(string ipAddress, string cidr)
        {
            if (string.IsNullOrEmpty(ipAddress) || string.IsNullOrEmpty(cidr)) return false;

            try
            {
                // On sépare l'adresse du masque (ex: "192.168.1.0" et "24")
                string[] parts = cidr.Split('/');
                if (parts.Length != 2) return false;

                string cidrIp = parts[0];
                if (!int.TryParse(parts[1], out int cidrMaskLength)) return false;

                // Conversion en objets IPAddress
                IPAddress ip = IPAddress.Parse(ipAddress);
                IPAddress network = IPAddress.Parse(cidrIp);

                // On vérifie que ce sont bien des IPv4
                if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
                    network.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return false;
                }

                // Calcul mathématique des masques (opérations bit à bit)
                byte[] ipBytes = ip.GetAddressBytes();
                byte[] networkBytes = network.GetAddressBytes();
                byte[] maskBytes = CreateMask(cidrMaskLength);

                // Comparaison octet par octet
                for (int i = 0; i < 4; i++)
                {
                    if ((ipBytes[i] & maskBytes[i]) != (networkBytes[i] & maskBytes[i]))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                // Si l'IP ou le CIDR est malformé, on renvoie faux
                return false;
            }
        }

        private static byte[] CreateMask(int length)
        {
            uint mask = 0xffffffff;
            mask <<= (32 - length);

            // Inversion pour gérer le boutisme (Endianness)
            uint reversedMask = ((mask & 0x000000ff) << 24) |
                                ((mask & 0x0000ff00) << 8) |
                                ((mask & 0x00ff0000) >> 8) |
                                ((mask & 0xff000000) >> 24);

            return BitConverter.GetBytes(reversedMask);
        }
    }
}