using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Autoprint.Client.Services
{
    public class NetworkService
    {
        public List<string> GetAllLocalIpAddresses()
        {
            var ips = new List<string>();

            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(c => c.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            c.OperationalStatus == OperationalStatus.Up);

            foreach (var item in interfaces)
            {
                var props = item.GetIPProperties();

                var ipInfos = props.UnicastAddresses
                    .Where(d => d.Address.AddressFamily == AddressFamily.InterNetwork);

                foreach (var ip in ipInfos)
                {
                    if (!ip.Address.ToString().StartsWith("169.254"))
                    {
                        ips.Add(ip.Address.ToString());
                    }
                }
            }

            return ips;
        }
    }
}