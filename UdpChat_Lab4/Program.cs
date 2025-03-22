using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

int localPort = 11000;
IPAddress brodcastAddress = IPAddress.Broadcast;
Console.Write("Введите свое имя: ");
string? username = Console.ReadLine();

Task.Run(ReceiveMessage);
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
        await sender.SendAsync(data, data.Length, new IPEndPoint(brodcastAddress, localPort));
    }
}
// получение сообщений из группы
void ReceiveMessage()
{
    using var receiver = new UdpClient(localPort);
    IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, localPort);
    Console.WriteLine($"Listening for UDP broadcasts on port {localPort}...");
   
    while (true)
    {
        byte[] receivedBytes = receiver.Receive(ref endPoint);
        string message = Encoding.UTF8.GetString(receivedBytes);
        Console.WriteLine(message);
    }
}