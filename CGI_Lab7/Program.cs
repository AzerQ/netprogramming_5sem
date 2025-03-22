using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;

// -------------------------------------------------------------------------
// Лабораторная работа № 7: CGI (упрощённая реализация на C#)
// -------------------------------------------------------------------------
//
// Принцип:
//   - Сервер слушает TCP-порт 8081 по HTTP.
//   - Обрабатывает GET-запросы:
//       * /cgi-bin/<имя_скрипта>?<параметры>
//         -> Запускаем локальный php-процесс, передаём скрипт и query в аргументах.
//         -> Возвращаем stdout клиента как HTML.
//       * / или /index.html
//         -> Ищем файл "index.html", если нет — 404
//       * Любой другой путь
//         -> Пытаемся найти файл на диске, если нет — 404
//   - Многопоточность: на каждый клиент создаём Task.
//
// Для тестирования CGI:
//   1. Установите PHP (чтобы в системе была команда "php").
//   2. Положите ваш PHP-скрипт, например `test.php`, в папку `cgi-bin` рядом с EXE.
//   3. В браузере: http://127.0.0.1:8081/cgi-bin/test.php?param=value
//      должен вернуть сгенерированную страницу.
//
// -------------------------------------------------------------------------

public static class Program
{
    // Порт для нашего HTTP-сервера
    const int ServerPort = 8081;

    public static void Main()
    {
        var listener = new TcpListener(IPAddress.Any, ServerPort);
        listener.Start();
        Console.WriteLine($"HTTP Server running on port {ServerPort}...");
        OpenUrl($"http://localhost:{ServerPort}/cgi-bin/test.php?param=123");

        // Основной цикл: принимаем подключения и обрабатываем в отдельных задачах
        while (true)
        {
            // Блокирующий вызов до появления нового клиента
            var client = listener.AcceptTcpClient();
            Task.Run(() => HandleClient(client));
        }
    }

    /// <summary>
    /// Обработка клиента: парсим запрос, проверяем путь,
    /// либо вызываем CGI, либо отдаём статический файл, либо 404.
    /// </summary>
    static void HandleClient(TcpClient client)
    {
        using var netStream = client.GetStream();
        try
        {
            using var reader = new StreamReader(netStream, Encoding.ASCII, leaveOpen: true);
            using var writer = new StreamWriter(netStream, Encoding.ASCII, leaveOpen: true)
            {
                AutoFlush = true
            };

            // Читаем первую строку HTTP-запроса
            var requestLine = reader.ReadLine();
            if (string.IsNullOrEmpty(requestLine))
            {
                client.Close();
                return;
            }

            // Пример: "GET /cgi-bin/test.php?param=value HTTP/1.1"
            var parts = requestLine.Split(' ');
            if (parts.Length < 3)
            {
                client.Close();
                return;
            }

            var method = parts[0]; // "GET"
            var fullPath = parts[1]; // "/cgi-bin/test.php?param=value"
            var version = parts[2]; // "HTTP/1.1"

            // Считываем остальные заголовки, пока не встретим пустую строку
            while (!string.IsNullOrEmpty(reader.ReadLine())) { }

            // Поддерживаем только GET
            if (method != "GET")
            {
                SendError(writer, 405, "Method Not Allowed", "<h1>405 Method Not Allowed</h1>");
                return;
            }

            // Если путь начинается с /cgi-bin/
            if (fullPath.StartsWith("/cgi-bin/"))
            {
                // Пример: /cgi-bin/test.php?param=value
                // scriptPath: ./cgi-bin/test.php
                // queryString: param=value
                var cgiResult = HandleCgi(fullPath);
                if (cgiResult != null)
                {
                    SendResponse(writer, cgiResult, "text/html; charset=utf-8");
                }
                else
                {
                    SendError(writer, 500, "Internal Server Error", "<h1>Failed to execute CGI script</h1>");
                }
            }
            else if (fullPath == "/" || fullPath == "/index.html")
            {
                // Отдаём index.html (если есть)
                var indexFile = Path.Combine(Directory.GetCurrentDirectory(), "index.html");
                if (File.Exists(indexFile))
                {
                    var content = File.ReadAllText(indexFile, Encoding.UTF8);
                    SendResponse(writer, content, "text/html; charset=utf-8");
                }
                else
                {
                    SendError(writer, 404, "Not Found", "<h1>404 Not Found</h1>");
                }
            }
            else
            {
                // Считаем, что это запрос статического файла из текущей директории
                // Например: /img/pic.png -> ./img/pic.png
                var localPath = fullPath.TrimStart('/');
                var fullLocalPath = Path.Combine(Directory.GetCurrentDirectory(), localPath);

                if (File.Exists(fullLocalPath))
                {
                    // Отправляем как бинарный файл (application/octet-stream)
                    var fileBytes = File.ReadAllBytes(fullLocalPath);
                    SendResponseRaw(writer, fileBytes, "application/octet-stream");
                }
                else
                {
                    SendError(writer, 404, "Not Found", "<h1>404 Not Found</h1>");
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
    /// Функция обрабатывает путь вида "/cgi-bin/test.php?param=value",
    /// запускает PHP и возвращает stdout (или null, если ошибка).
    /// </summary>
    static string HandleCgi(string fullPath)
    {
        // Пример входа: "/cgi-bin/test.php?param=value"
        // Убираем "/cgi-bin/" -> "test.php?param=value"
        var subPath = fullPath.Substring(9); // пропускаем "/cgi-bin/"
        // Ищем query
        string queryString = "";
        var queryIndex = subPath.IndexOf('?');
        if (queryIndex >= 0)
        {
            queryString = subPath.Substring(queryIndex + 1);
            subPath = subPath.Substring(0, queryIndex); // оставим только имя файла
        }

        // Полный путь к скрипту
        var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "cgi-bin", subPath);
        if (!File.Exists(scriptPath))
        {
            // Скрипт не найден
            return $"<html><body><h1>404 Not Found</h1><p>Script {scriptPath} not found</p></body></html>";
        }


        queryString = string.Join(" ", queryString.Split("&"));

        // Формируем команду: "php scriptPath queryString"
        // В реальном CGI можно передавать параметры через переменные окружения,
        // но для упрощённой версии — командная строка.
        var startInfo = new ProcessStartInfo
        {
            FileName = "php-cgi",             // предполагается, что php доступен в PATH
            Arguments = $"\"{scriptPath}\" {queryString}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(startInfo);
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output; // вернём вывод скрипта
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Не удалось запустить php: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Отправка текстового HTTP-ответа (строка content).
    /// По умолчанию статус 200 OK.
    /// </summary>
    static void SendResponse(StreamWriter writer, string content, string contentType, int statusCode = 200, string statusText = "OK")
    {
        var utf8Bytes = Encoding.UTF8.GetBytes(content); // длину считаем в байтах UTF8
        writer.WriteLine($"HTTP/1.1 {statusCode} {statusText}");
        writer.WriteLine($"Content-Type: {contentType}");
        writer.WriteLine($"Content-Length: {utf8Bytes.Length}");
        writer.WriteLine("Connection: close");
        writer.WriteLine();
        writer.Flush();

        // Пишем само тело
        writer.BaseStream.Write(utf8Bytes, 0, utf8Bytes.Length);
        writer.BaseStream.Flush();
    }

    /// <summary>
    /// Отправка бинарного ответа (например, статических файлов).
    /// </summary>
    static void SendResponseRaw(StreamWriter writer, byte[] data, string contentType, int statusCode = 200, string statusText = "OK")
    {
        writer.WriteLine($"HTTP/1.1 {statusCode} {statusText}");
        writer.WriteLine($"Content-Type: {contentType}");
        writer.WriteLine($"Content-Length: {data.Length}");
        writer.WriteLine("Connection: close");
        writer.WriteLine();
        writer.Flush();

        // Пишем бинарные данные
        writer.BaseStream.Write(data, 0, data.Length);
        writer.BaseStream.Flush();
    }

    /// <summary>
    /// Отправка страницы ошибки (любого кода).
    /// </summary>
    static void SendError(StreamWriter writer, int code, string text, string htmlBody)
    {
        SendResponse(writer, htmlBody, "text/html; charset=utf-8", code, text);
    }

    static void OpenUrl(string url)
    {
        try
        {
            Process.Start(url);
        }
        catch
        {
            // hack because of this: https://github.com/dotnet/corefx/issues/10361
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                throw;
            }
        }
    }
}

