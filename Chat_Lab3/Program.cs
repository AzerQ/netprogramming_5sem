using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;

// Топ-левел инструкция для входной точки приложения.
// Пример вызова (PowerShell/командная строка):
//   Сервер: ChatApp server 127.0.0.1 5000
//   Клиент: ChatApp client 127.0.0.1 5000 MyNickname

if (args.Length < 3)
{
    Console.WriteLine("""
        Некорректные аргументы.
        Формат:
          server <IP> <PORT>
          client <IP> <PORT> <NICKNAME>
        """);
    return;
}

var mode = args[0].ToLowerInvariant(); // "server" или "client"
var ipStr = args[1];
var portStr = args[2];

if (!int.TryParse(portStr, out var port))
{
    Console.WriteLine("Порт должен быть числом.");
    return;
}

switch (mode)
{
    case "server":
        await RunServerAsync(ipStr, port);
        break;
    case "client":
        if (args.Length < 4)
        {
            Console.WriteLine("Для клиента необходимо указать NICKNAME: client <IP> <PORT> <NICKNAME>");
            return;
        }
        var nickname = args[3];
        await RunClientAsync(ipStr, port, nickname);
        break;
    default:
        Console.WriteLine("Первый аргумент должен быть 'server' или 'client'.");
        break;
}

/// <summary>
/// Запуск серверной части: слушаем входящие подключения, создаём потоки (Tasks) на каждого клиента.
/// </summary>
static async Task RunServerAsync(string ip, int port)
{
    var ipAddress = IPAddress.Parse(ip);
    var listener = new TcpListener(ipAddress, port);

    // Общая коллекция для всех подключений
    var clients = new ConcurrentBag<TcpClient>();

    Console.WriteLine($"Сервер запущен на {ip}:{port}. Ожидаем подключения...");

    // Запуск прослушивания
    listener.Start();

    // Запускаем бесконечный цикл ожидания клиентов
    _ = Task.Run(async () =>
    {
        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            clients.Add(client);
            Console.WriteLine($"Новый клиент подключен: {client.Client.RemoteEndPoint}");

            // Запускаем отдельную задачу для приёма сообщений от клиента
            _ = HandleClientAsync(client, clients);
        }
    });

    Console.WriteLine("Нажмите Enter для завершения работы сервера...");
    Console.ReadLine();

    // Останавливаем сервер
    listener.Stop();
    Console.WriteLine("Сервер остановлен.");
}

/// <summary>
/// Обрабатывает взаимодействие с конкретным клиентом:
/// - получает сообщения
/// - рассылает их всем остальным клиентам
/// </summary>
static async Task HandleClientAsync(TcpClient client, ConcurrentBag<TcpClient> allClients)
{
    try
    {
        using var networkStream = client.GetStream();
        var buffer = new byte[1024];
        int bytesRead;

        while ((bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            // Логируем полученное сообщение на сервере
            lock (Console.Out)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"Получено: {message}");
                Console.ResetColor();
            }

            // Рассылаем всем клиентам, кроме отправившего
            foreach (var c in allClients)
            {
                if (c == client) continue; // не отправляем обратно автору

                try
                {
                    var cStream = c.GetStream();
                    var msgBytes = Encoding.UTF8.GetBytes(message);
                    await cStream.WriteAsync(msgBytes, 0, msgBytes.Length);
                }
                catch
                {
                    // Игнорируем проблемы с отдельными клиентами
                }
            }
        }
    }
    catch (Exception ex)
    {
        lock (Console.Out)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Ошибка в HandleClientAsync: {ex.Message}");
            Console.ResetColor();
        }
    }
    finally
    {
        // Клиент отключился или произошла ошибка
        client.Close();
        lock (Console.Out)
        {
            Console.WriteLine($"Клиент отключён: {client.Client.RemoteEndPoint}");
        }
        // Удалять из ConcurrentBag не так просто (нет Remove),
        // поэтому оставим клиента "мёртвым" в коллекции,
        // либо можно использовать другую структуру (например, ConcurrentDictionary).
    }
}

/// <summary>
/// Запуск клиентской части: подключаемся к серверу, создаём поток (Task) приёма сообщений, 
/// отправляем сообщения из консоли.
/// </summary>
static async Task RunClientAsync(string ip, int port, string nickname)
{
    var client = new TcpClient();
    try
    {
        Console.WriteLine($"Подключение к серверу {ip}:{port}...");
        await client.ConnectAsync(IPAddress.Parse(ip), port);
        Console.WriteLine("Подключение установлено.");

        // Запуск задачи, принимающей входящие сообщения от сервера
        _ = Task.Run(async () =>
        {
            try
            {
                using var stream = client.GetStream();
                var buf = new byte[1024];
                int len;
                while ((len = await stream.ReadAsync(buf, 0, buf.Length)) > 0)
                {
                    var msg = Encoding.UTF8.GetString(buf, 0, len);
                    lock (Console.Out)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine(msg);
                        Console.ResetColor();
                    }
                }
            }
            catch
            {
                // Если сервер недоступен или соединение разорвано
                lock (Console.Out)
                {
                    Console.WriteLine("Поток чтения сервера завершён.");
                }
            }
        });

        // Основной цикл чтения из консоли и отправки сообщений
        while (true)
        {
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase))
            {
                // Завершаем работу клиента
                break;
            }

            // Форматируем сообщение: [nickname] текст
            var finalMessage = $"[{nickname}] {input}";
            var bytes = Encoding.UTF8.GetBytes(finalMessage);
            var netStream = client.GetStream();
            await netStream.WriteAsync(bytes, 0, bytes.Length);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка клиента: {ex.Message}");
    }
    finally
    {
        client.Close();
        Console.WriteLine("Клиент завершил работу.");
    }
}
