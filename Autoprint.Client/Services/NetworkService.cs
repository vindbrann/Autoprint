using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Autoprint.Client.Services
{
    public class NetworkService
    {
        public string? GetLocalIpAddress()
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(c => c.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            c.OperationalStatus == OperationalStatus.Up);

            foreach (var item in interfaces)
            {
                var props = item.GetIPProperties();

                var ipInfo = props.UnicastAddresses
                    .FirstOrDefault(d => d.Address.AddressFamily == AddressFamily.InterNetwork);

                if (ipInfo != null)
                {
                    return ipInfo.Address.ToString();
                }
            }

            return null;
        }
    }
}