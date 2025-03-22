using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UdpChat_Lab4;

int localPort = 11000;
IPAddress brodcastAddress = SelectBroadcastAdress();
Console.Write("Введите свое имя: ");
string? username = Console.ReadLine();

Task.Run(ReceiveMessageAsync);
await SendMessageAsync();

// отправка сообщений в группу
async Task SendMessageAsync()
{
    using var sender = new UdpClient(); // создаем UdpClient для отправки
    // отправляем сообщения
    while (true)
    {
        string? message = Console.ReadLine(); // сообщение для отправки
        // если введена пустая строка, выходим из цикла и завершаем ввод сообщений
        if (string.IsNullOrWhiteSpace(message)) break;
        // иначе добавляем к сообщению имя пользователя
        message = $"{username}: {message}";
        byte[] data = Encoding.UTF8.GetBytes(message);
        // и отправляем в группу
        await sender.SendAsync(data, new IPEndPoint(brodcastAddress, localPort));
    }
}
// получение сообщений из группы
async Task ReceiveMessageAsync()
{
    using var receiver = new UdpClient(localPort); // UdpClient для получения данных
    receiver.JoinMulticastGroup(brodcastAddress);
    receiver.MulticastLoopback = false; // отключаем получение своих же сообщений
    while (true)
    {
        var result = await receiver.ReceiveAsync();
        string message = Encoding.UTF8.GetString(result.Buffer);
        Console.WriteLine(message);
    }
}

IPAddress SelectBroadcastAdress()
{
    var broadcastInfo = GetBroadCastInfo().DistinctBy(b => b.MaskAddress);
    byte userChoise = 0;
    int i = 0;
    foreach (var broadcast in broadcastInfo)
    {
        Console.WriteLine("{0}: {1}", i++, broadcast);
    }
    Console.Write("Choise broadcast: ");
    userChoise = byte.Parse(Console.ReadLine());
    return Tools.GetBroadcastAddress(broadcastInfo.ElementAt(userChoise));
}

IEnumerable<NetInfo> GetBroadCastInfo()
{
    NetworkInterface[] Interfaces = NetworkInterface.GetAllNetworkInterfaces();
    foreach (NetworkInterface Interface in Interfaces)
    {
        if (Interface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
        if (Interface.OperationalStatus != OperationalStatus.Up) continue;
        UnicastIPAddressInformationCollection UnicastIPInfoCol = Interface.GetIPProperties().UnicastAddresses;
        foreach (UnicastIPAddressInformation UnicatIPInfo in UnicastIPInfoCol)
        {
            yield return new NetInfo(UnicatIPInfo.Address, UnicatIPInfo.IPv4Mask, Interface.Description);
        }
    }
}