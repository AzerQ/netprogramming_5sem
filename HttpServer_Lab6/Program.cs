//using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Net;

public static class Program
{
    // Порт сервера
    const int ServerPort = 8081;

    public static void Main()
    {
        // Создаём TcpListener (работаем по TCP на указанном порту)
        var listener = new TcpListener(IPAddress.Any, ServerPort);
        listener.Start();
        Console.WriteLine($"HTTP Server running on port {ServerPort}...");

        // Бесконечный цикл ожидания клиентов
        while (true)
        {
            // Принимаем входящее соединение (блокирующе)
            var client = listener.AcceptTcpClient();

            // Запускаем новую задачу/поток для этого клиента
            Task.Run(() => HandleClient(client));
        }
    }

    /// <summary>
    /// Обработка клиента: читаем запрос, парсим, формируем ответ
    /// </summary>
    static void HandleClient(TcpClient client)
    {
        using var networkStream = client.GetStream();

        try
        {
            // Читаем «сырые» данные из сокета
            using var reader = new StreamReader(networkStream, Encoding.UTF8, leaveOpen: true);
            // Пишем туда же
            using var writer = new StreamWriter(networkStream, Encoding.UTF8, leaveOpen: true)
            {
                AutoFlush = true
            };

            // Считываем первую строку запроса (например: "GET /index.html HTTP/1.1")
            var requestLine = reader.ReadLine();
            if (string.IsNullOrEmpty(requestLine))
            {
                client.Close();
                return;
            }

            // Парсим метод (GET), путь (/index.html) и протокол (HTTP/1.1)
            var parts = requestLine.Split(' ');
            if (parts.Length < 3)
            {
                client.Close();
                return;
            }

            var method = parts[0];
            var path = parts[1];  // Начинается обычно с '/'
            var version = parts[2];

            Console.WriteLine("Method: {0}, Path: {1}, IP: {2}", method, path, client.Client.RemoteEndPoint);

            // Нас интересует только GET
            if (method != "GET")
            {
                // Пока не реализуем другие методы, просто закроем
                client.Close();
                return;
            }

            // Если path == "/" -> /index.html
            if (path == "/" || path == "/index.html")
            {
                var indexFile = Path.Combine(Directory.GetCurrentDirectory(), "index.html");
                if (File.Exists(indexFile))
                {
                    // Читаем index.html и отправляем
                    var content = File.ReadAllText(indexFile, Encoding.UTF8);
                    SendResponse(writer, content, "text/html; charset=utf-8");
                }
                else
                {
                    // Если index.html нет, отдаём встроенную HTML-страницу
                    var builtInHtml = GetBuiltInIndexHtml();
                    SendResponse(writer, builtInHtml, "text/html; charset=utf-8");
                }
            }
            else
            {
                // Пробуем вернуть файл из "текущей папки"
                // Например, GET /images/pic.png -> берём .\images\pic.png
                var localPath = path.TrimStart('/'); // убираем слеш
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), localPath);

                if (File.Exists(fullPath))
                {
                    // Читаем содержимое файла (бинарно), но для упрощения - в текст
                    // Можно отдельно обработать content-type по расширению
                    var bytes = File.ReadAllBytes(fullPath);

                    // Для удобства возьмём content-type = application/octet-stream
                    // (можно определять по MIME, если нужно)
                    SendResponseRaw(writer, bytes, "application/octet-stream");
                }
                else
                {
                    // Файл не найден -> 404
                    var notFoundPage = "<html><body><h1>404 Not Found</h1></body></html>";
                    SendResponse(writer, notFoundPage, "text/html; charset=utf-8", 404, "Not Found");
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
        }
    }

    /// <summary>
    /// Отправка HTTP-ответа (текстового)
    /// </summary>
    static void SendResponse(StreamWriter writer, string content, string contentType, int statusCode = 200, string statusText = "OK")
    {
        // Формируем заголовки HTTP
        writer.WriteLine($"HTTP/1.1 {statusCode} {statusText}");
        writer.WriteLine($"Content-Type: {contentType}");
        writer.WriteLine($"Content-Length: {Encoding.UTF8.GetByteCount(content)}");
        writer.WriteLine("Connection: close");
        writer.WriteLine(); // пустая строка
        writer.Write(content); // тело ответа (text)
    }

    /// <summary>
    /// Отправка HTTP-ответа в «сыром» бинарном виде (для файлов).
    /// </summary>
    static void SendResponseRaw(StreamWriter writer, byte[] content, string contentType, int statusCode = 200, string statusText = "OK")
    {
        // Сперва пишем заголовки
        writer.WriteLine($"HTTP/1.1 {statusCode} {statusText}");
        writer.WriteLine($"Content-Type: {contentType}");
        writer.WriteLine($"Content-Length: {content.Length}");
        writer.WriteLine("Connection: close");
        writer.WriteLine();
        writer.Flush(); // важно сбросить заголовки

        // Потом пишем бинарные данные
        writer.BaseStream.Write(content, 0, content.Length);
        writer.BaseStream.Flush();
    }

    /// <summary>
    /// Небольшая «красивая» HTML-страница, которую отдаём, если index.html нет на диске.
    /// </summary>
    static string GetBuiltInIndexHtml()
    {
        return @"
<!DOCTYPE html>
<html lang='en'>
<head>
  <meta charset='UTF-8'>
  <title>Моя страница</title>
  <style>
    body {
      background-color: #F9F7F4;
      font-family: sans-serif;
      color: #333;
      margin: 2em;
    }
    h1 {
      color: #444;
      border-bottom: 1px solid #ccc;
      padding-bottom: 0.5em;
    }
    p {
      line-height: 1.5em;
    }
    .footer {
      margin-top: 2em;
      font-size: 0.9em;
      color: #999;
    }
  </style>
</head>
<body>
  <h1>Добро пожаловать!</h1>
  <p>Это страница, возвращаемая встроенным HTTP-сервером на C#.<br/>
     Попробуйте разместить рядом с EXE-файлом файл <code>index.html</code>, 
     тогда сервер отдаст его вместо этой встроенной страницы.</p>

  <div class='footer'>Lab 6: Простой HTTP-сервер. &copy; 2025</div>
</body>
</html>
";
    }
}
