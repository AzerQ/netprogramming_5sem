using System.Net;

namespace UdpChat_Lab4
{
   public record BroadcastInfo(IPAddress Adress, IPAddress MaskAddress, string Description);
}
