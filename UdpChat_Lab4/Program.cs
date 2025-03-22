using System.Net;
using System.Net.Sockets;
using System.Text;

const int BroadcastPort = 8888;           // Порт для широковещательной рассылки
const string BroadcastAddress = "255.255.255.255"; // Широковещательный IP

// Пример вызова (из папки сборки):
//   UdpChatApp.exe 5000 MyNickname
//
// Аргументы:
//   1) Локальный порт (на котором принимаем UDP-сообщения)
//   2) Ваш ник (имя пользователя) для чата

if (args.Length != 2)
{
    Console.WriteLine("""
        Неверные аргументы.
        Правильный формат:
          UdpChatApp.exe <LOCAL_PORT> <NICKNAME>

        Пример:
          UdpChatApp.exe 5000 Alex
          UdpChatApp.exe 5001 Bob
        """);
    return;
}

// Извлекаем аргументы
if (!int.TryParse(args[0], out int localPort))
{
    Console.WriteLine("Порт должен быть целым числом.");
    return;
}

string nickname = args[1];

// Создаём сокет (UDP)
using var udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

try
{
    // Привязываем сокет к локальному адресу:порт (IPAddress.Any = 0.0.0.0)
    var localEndPoint = new IPEndPoint(IPAddress.Any, localPort);
    udpSocket.Bind(localEndPoint);

    // Включаем неблокирующий режим
    udpSocket.Blocking = false;

    // Разрешаем широковещательную отправку
    udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

    // Широковещательный EndPoint, на который будем отправлять
    var broadcastEP = new IPEndPoint(IPAddress.Parse(BroadcastAddress), BroadcastPort);

    // Запускаем задание для периодического "обнаружения" в сети (DISCOVER_CHAT)
    _ = Task.Run(() => BroadcastDiscoveryLoop(udpSocket, broadcastEP));

    // Запускаем задание для приёма входящих UDP-сообщений
    _ = Task.Run(() => ReceiveLoop(udpSocket));

    // Основной цикл отправки сообщений
    SendMessagesLoop(udpSocket, broadcastEP, nickname);
}
catch (Exception ex)
{
    Console.WriteLine($"Ошибка: {ex.Message}");
}
finally
{
    // Корректно закрываем сокет
    udpSocket.Close();
}

static async Task BroadcastDiscoveryLoop(Socket socket, EndPoint broadcastEP)
{
    // Периодически рассылаем "DISCOVER_CHAT"
    var discoveryMessage = Encoding.UTF8.GetBytes("DISCOVER_CHAT");
    while (true)
    {
        try
        {
            socket.SendTo(discoveryMessage, broadcastEP);
        }
        catch
        {
            // Сокет закрыт или другая ошибка — выходим из цикла
            break;
        }

        await Task.Delay(TimeSpan.FromSeconds(7));
    }
}

static void ReceiveLoop(Socket socket)
{
    // Приём входящих пакетов в неблокирующем режиме
    var buffer = new byte[1024];

    while (true)
    {
        try
        {
            EndPoint senderEP = new IPEndPoint(IPAddress.Any, 0);
            int bytesRead = socket.ReceiveFrom(buffer, ref senderEP);

            if (bytesRead > 0)
            {
                var msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                lock (Console.Out)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine(msg);
                    Console.ResetColor();
                }
            }
        }
        catch (SocketException ex)
        {
            // Если данных нет -> WSAEWOULDBLOCK (10035). Делаем небольшую паузу.
            if (ex.SocketErrorCode == SocketError.WouldBlock)
            {
                Thread.Sleep(100);
            }
            else
            {
                // Любая другая ошибка => завершаем поток
                break;
            }
        }
        catch
        {
            // Если сокет закрыт или другая ошибка — завершаем поток
            break;
        }
    }
}

static void SendMessagesLoop(Socket socket, EndPoint broadcastEP, string nickname)
{
    // Чтение из консоли, отправка в широковещание
    while (true)
    {
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input)) continue;

        // Команда выхода
        if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }

        // Формируем сообщение: [nickname] ...
        var fullMsg = $"[{nickname}] {input}";
        var msgBytes = Encoding.UTF8.GetBytes(fullMsg);

        socket.SendTo(msgBytes, broadcastEP);
    }
}
