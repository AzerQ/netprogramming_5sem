using System.Net;
using System.Net.Sockets;
using System.Text;

int localPort = 8001;
IPAddress brodcastAddress = IPAddress.Parse("235.5.5.11");
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