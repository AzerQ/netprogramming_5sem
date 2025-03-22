using System.Net;
using System.Net.Sockets;

// Топ-левел инструкция: вместо метода Main в классе Program
// Код начинает выполняться сразу с тела файла Program.cs.

// Проверка и парсинг аргументов командной строки
if (args.Length < 3)
{
    Console.WriteLine("""
        Некорректное число аргументов.
        Формат:
          server <IP> <PORT>
          client <IP> <PORT>
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

var task = mode switch
{
    "server" => RunServerAsync(ipStr, port),
    "client" => RunClientAsync(ipStr, port),
    _ => Task.Run(() => Console.WriteLine("Первый аргумент должен быть 'server' или 'client'."))
};

await task;


/// <summary>
/// Запуск сервера: приём подключения и отправка файла
/// </summary>
static async Task RunServerAsync(string ip, int port)
{
    try
    {
        var ipAddress = IPAddress.Parse(ip);
        using var listener = new TcpListener(ipAddress, port);

        listener.Start();
        Console.WriteLine($"Сервер запущен. Ожидаем подключения на {ip}:{port}...");

        // Принимаем клиента (блокирующая операция)
        using var client = await listener.AcceptTcpClientAsync();
        Console.WriteLine("Клиент подключён.");

        // Открываем сетевой поток
        await using var networkStream = client.GetStream();

        // Файл, который будем отправлять
        const string filePath = "file_to_send.txt";
        if (!File.Exists(filePath))
        {
            Console.WriteLine("""
                Файл для отправки не найден. 
                Ожидается file_to_send.txt рядом с исполняемым файлом.
                """);
            return;
        }

        // Читаем файл из ФС и отправляем по сети
        await using var fileStream = File.OpenRead(filePath);
        var buffer = new byte[4096];
        int bytesRead;
        while ((bytesRead = await fileStream.ReadAsync(buffer)) > 0)
        {
            await networkStream.WriteAsync(buffer.AsMemory(0, bytesRead));
        }

        Console.WriteLine("Отправка файла завершена.");

        // Останавливаем прослушивание
        listener.Stop();
        Console.WriteLine("Сервер завершил работу.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка в работе сервера: {ex.Message}");
    }
}

/// <summary>
/// Запуск клиента: подключение к серверу и приём файла
/// </summary>
static async Task RunClientAsync(string ip, int port)
{
    try
    {
        using var client = new TcpClient();
        Console.WriteLine($"Подключение к {ip}:{port}...");

        await client.ConnectAsync(IPAddress.Parse(ip), port);
        Console.WriteLine("Подключение установлено.");

        // Получаем сетевой поток
        await using var networkStream = client.GetStream();

        // Файл для записи полученных данных
        const string receivedFilePath = "received_file.txt";
        await using var fileStream = File.Create(receivedFilePath);

        // Приём данных и запись их в файл
        var buffer = new byte[4096];
        int bytesRead;
        while ((bytesRead = await networkStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
        }

        Console.WriteLine("""
            Файл успешно получен и сохранён как received_file.txt
            """);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка в работе клиента: {ex.Message}");
    }
}
