using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

int localPort = 11000;
IPAddress broadcastAddress = IPAddress.Broadcast;
Console.Write("Введите свое имя: ");
string? username = Console.ReadLine();

Task.Run(ReceiveMessageAsync);
await SendMessageAsync();

// отправка сообщений в группу
async Task SendMessageAsync()
{
    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    socket.EnableBroadcast = true;
    var broadcastEndpoint = new IPEndPoint(broadcastAddress, localPort);

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
        await socket.SendToAsync(data, broadcastEndpoint);
    }
}
// получение сообщений из группы
async Task ReceiveMessageAsync()
{

    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    socket.EnableBroadcast = true;
    var broadcastEndpoint = new IPEndPoint(broadcastAddress, localPort);
    socket.Bind(broadcastEndpoint);

    while (true)
    {

        await Task.Delay(1000);

        var buffer = new byte[1024];
        await socket.ReceiveAsync(buffer);

        string message = Encoding.UTF8.GetString(buffer);

        if (!string.IsNullOrWhiteSpace(message))
            Console.WriteLine(message);
    }
}