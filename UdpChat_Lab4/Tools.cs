using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;

namespace UdpChat_Lab4
{
    static class Tools
    {
        public static IPAddress GetLocalAddress()
        {
            List<string> result = new List<string>();
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                var items = networkInterface.GetIPProperties().UnicastAddresses
                    .Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork && IsInLocalRange(x.Address));
                result.AddRange(items.Select(ip => ip.Address.ToString()));
            }

            return IPAddress.Parse(result.Last());

            bool IsInLocalRange(IPAddress address)
            {
                byte[] bytes = address.GetAddressBytes();
                return bytes[0] == 192 && bytes[1] == 168;
            }
        }

        public static IPAddress GetLocalMask()
        {
            List<string> mask = new List<string>();
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                var items = networkInterface.GetIPProperties().UnicastAddresses
                    .Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork && IsInLocalRange(x.Address));
                mask.AddRange(items.Select(ip => ip.IPv4Mask.ToString()));
            }

            return IPAddress.Parse(mask.Last());

            bool IsInLocalRange(IPAddress address)
            {
                byte[] bytes = address.GetAddressBytes();
                return bytes[0] == 192 && bytes[1] == 168;
            }
        }
        public static IPAddress GetBroadcastAddress(string ipAddress, string subnetMask)
        {
            if (string.IsNullOrEmpty(ipAddress) || string.IsNullOrEmpty(subnetMask))
            {
                throw new ArgumentException("IP address and subnet mask cannot be null or empty");
            }

            byte[] ipBytes = IPAddress.Parse(ipAddress).GetAddressBytes();
            byte[] maskBytes = IPAddress.Parse(subnetMask).GetAddressBytes();

            if (ipBytes.Length != maskBytes.Length)
            {
                throw new ArgumentException("Invalid IP address or subnet mask format");
            }

            byte[] broadcastBytes = new byte[ipBytes.Length];

            for (int i = 0; i < broadcastBytes.Length; i++)
            {
                broadcastBytes[i] = (byte)(ipBytes[i] | (byte)~maskBytes[i]);
            }

            return new IPAddress(broadcastBytes);
        }

        public static IPAddress GetBroadcastAddress(NetInfo netInfo) => GetBroadcastAddress(netInfo.Adress.ToString(), netInfo.MaskAddress.ToString());
    }
}
