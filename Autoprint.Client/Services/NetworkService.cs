using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Autoprint.Client.Services
{
    public class NetworkService
    {
        public string GetActiveLocalIpAddress(string targetHost)
        {
            if (!string.IsNullOrWhiteSpace(targetHost))
            {
                try
                {
                    string host = targetHost.Replace("http://", "").Replace("https://", "").Trim('/');

                    if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return "127.0.0.1";

                    using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.Connect(host, 65530);
                    if (socket.LocalEndPoint is IPEndPoint endPoint)
                    {
                        return endPoint.Address.ToString();
                    }
                }
                catch
                {
                }
            }

            try
            {
                var firstUpInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up
                                         && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                                         && n.GetIPProperties().GatewayAddresses.Any());

                if (firstUpInterface != null)
                {
                    var ipProp = firstUpInterface.GetIPProperties().UnicastAddresses
                        .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork);

                    if (ipProp != null) return ipProp.Address.ToString();
                }
            }
            catch { }

            return "127.0.0.1";
        }
    }
}