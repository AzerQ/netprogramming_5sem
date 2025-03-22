using System.Net;
using System.Net.Sockets;
using System.Text;

static class Program
{
    // Текущая рабочая директория (общая для всех клиентов, как в исходном примере)
    static string currentDirectory = Directory.GetCurrentDirectory();

    // Размер буфера при чтении данных от клиента
    const int BufferSize = 1024;
    // Порт для нашего «FTP-сервера»
    const int ServerPort = 2121;

    public static void Main()
    {
        // Запускаем сервер на отдельном методе
        RunFtpServer();
    }

    static void RunFtpServer()
    {
        try
        {
            // Создаём TCP-сокет, слушаем на 0.0.0.0:2121
            var localEndPoint = new IPEndPoint(IPAddress.Any, ServerPort);
            var listener = new TcpListener(localEndPoint);

            listener.Start();
            Console.WriteLine($"FTP Server running on port {ServerPort}.");
            PrintServerIPs();

            // Основной цикл: принимаем клиентов
            while (true)
            {
                // accept блокируется до подключения клиента
                var client = listener.AcceptTcpClient();
                Console.WriteLine("Клиент подключен.");

                // Запускаем новую задачу (или поток) на каждого клиента
                Task.Run(() => HandleClient(client));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка сервера: {ex.Message}");
        }
    }

    /// <summary>
    /// Печать IP-адресов сервера (аналог printServerIP в C++).
    /// </summary>
    static void PrintServerIPs()
    {
        try
        {
            // Имя хоста
            var hostName = Dns.GetHostName();
            Console.WriteLine($"Hostname: {hostName}");

            // Все адреса, связанные с этим хостом (IPv4, IPv6)
            var addresses = Dns.GetHostAddresses(hostName);
            foreach (var addr in addresses)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    Console.WriteLine($"Server IP: {addr}");
                }
            }
        }
        catch
        {
            Console.WriteLine("Не удалось определить IP-адреса сервера.");
        }
    }

    /// <summary>
    /// Обработка подключенного клиента (чтение команд, выполнение, отправка ответа).
    /// </summary>
    static void HandleClient(TcpClient client)
    {
        using var netStream = client.GetStream();
        try
        {
            // Для удобства используем StreamReader/StreamWriter (текстовый протокол)
            // Буферизация, кодировка ASCII/UTF8
            using var reader = new StreamReader(netStream, Encoding.UTF8, leaveOpen: true);
            using var writer = new StreamWriter(netStream, Encoding.UTF8, leaveOpen: true)
            {
                AutoFlush = true
            };

            // Бесконечный цикл чтения команд
            while (true)
            {
                // Считываем одну строку (одну команду)
                var line = reader.ReadLine();
                if (line == null) // клиент закрыл соединение
                    break;

                // Удаляем пробелы в начале/конце
                var command = line.Trim();

                // Обрабатываем
                if (command.StartsWith("LIST", StringComparison.OrdinalIgnoreCase))
                {
                    var response = ListFiles();
                    writer.Write(response);
                }
                else if (command.StartsWith("CWD", StringComparison.OrdinalIgnoreCase))
                {
                    // Формат: CWD <путь>
                    // Нужно извлечь <путь> после "CWD "
                    var parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                    {
                        writer.WriteLine("Invalid CWD usage. Use: CWD <path>");
                        continue;
                    }

                    var newDir = parts[1].Trim();
                    // Попытаемся перейти
                    if (Directory.Exists(newDir))
                    {
                        currentDirectory = Path.GetFullPath(newDir);
                        writer.WriteLine($"Directory changed to: {currentDirectory}");
                    }
                    else
                    {
                        writer.WriteLine("Failed to change directory.");
                    }
                }
                else if (command.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                {
                    writer.WriteLine("Goodbye!");
                    break; // выходим из цикла => закроем соединение
                }
                else
                {
                    writer.WriteLine("Unknown command.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при обработке клиента: {ex.Message}");
        }
        finally
        {
            client.Close();
            Console.WriteLine("Клиент отключен.");
        }
    }

    /// <summary>
    /// Возвращает строковое представление списка файлов и директорий в текущей директории,
    /// по аналогии с 'listFiles' из исходного кода на C++.
    /// </summary>
    static string ListFiles()
    {
        // Для удобства используем StringBuilder
        var sb = new StringBuilder();

        try
        {
            var dirInfo = new DirectoryInfo(currentDirectory);
            // Перебираем содержимое каталога
            foreach (var entry in dirInfo.EnumerateFileSystemInfos())
            {
                // Определяем тип
                bool isDirectory = (entry.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
                var typeStr = isDirectory ? "<DIR> " : "<FILE>";

                // Дата последнего изменения
                var lastWrite = entry.LastWriteTime;
                // Форматируем время
                string timeStr = lastWrite.ToString("yyyy-MM-dd HH:mm:ss");

                // Размер (только для файлов)
                string sizeInfo = "";
                if (!isDirectory)
                {
                    var fileInfo = new FileInfo(entry.FullName);
                    sizeInfo = fileInfo.Length + " bytes";
                }

                // Пример вывода строки:
                // <DIR> subfolder	2023-01-10 12:34:56
                // <FILE> file.txt  2023-05-20 09:10:00 1234 bytes
                sb.AppendLine($"{typeStr} {entry.Name}\t{timeStr}\t{sizeInfo}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Ошибка чтения каталога: {ex.Message}");
        }

        return sb.ToString();
    }
}
