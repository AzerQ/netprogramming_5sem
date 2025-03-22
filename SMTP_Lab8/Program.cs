using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Text;
using System.Security.Authentication;
using System.IO;

// -------------------------------------------------------------------------
// Лабораторная работа №8: Протокол SMTP (C# пример с STARTTLS)
// -------------------------------------------------------------------------
//
// 1. Подключаемся к smtp.ethereal.email на порт 587 (STARTTLS).
// 2. Читаем приветствие.
// 3. Отправляем EHLO.
// 4. Отправляем STARTTLS, затем создаём SslStream для TLS-шифрования.
// 5. Повторяем EHLO.
// 6. AUTH LOGIN с логином/паролем в base64.
// 7. MAIL FROM, RCPT TO, DATA, шлём письмо, QUIT.
// -------------------------------------------------------------------------

public static class Program
{
    private static string Input(string prompt)
    {
        Console.Write("{0}: ", prompt);
        return Console.ReadLine();
    }

    private static string SmtpHost = Input("Введите адрес SMTP сервера");
    private static int SmtpPort = int.Parse(Input("Введите порт")); // 587 STARTTLS порт

    private static string SenderEmail = Input("Email отправителя"); // ваш "логин"
    private static string SenderPassword = Input("Пароль отправителя"); // ваш "пароль"

    // Получатель
     private static string RecipientEmail = Input("Почта получателя");

    // Текст письма
    private const string Subject = "Test Email from C# (Lab 8)";
    private const string Body = "Hello from C# SMTP client with STARTTLS!\r\nThis is a test message.\r\n";

    public static void Main()
    {
        try
        {
            // 1. Подключаемся по TCP к smtp.ethereal.email:587
            using var tcpClient = new TcpClient();
            tcpClient.Connect(SmtpHost, SmtpPort);
            using var networkStream = tcpClient.GetStream();

            // Читаем приветствие сервера
            ReadSmtpResponse(networkStream);

            // 2. Отправляем EHLO (Plain TCP)
            SendSmtpCommand(networkStream, "EHLO localhost\r\n");
            ReadSmtpResponse(networkStream);

            // 3. Запрашиваем STARTTLS
            SendSmtpCommand(networkStream, "STARTTLS\r\n");
            ReadSmtpResponse(networkStream);

            // 4. Создаём SslStream поверх существующего TCP-соединения
            using var sslStream = new SslStream(networkStream, leaveInnerStreamOpen: true);
            sslStream.AuthenticateAsClient(SmtpHost, null, SslProtocols.Tls12, checkCertificateRevocation: false);

            // 5. Повторяем EHLO (уже внутри TLS)
            SendSmtpCommand(sslStream, "EHLO localhost\r\n");
            ReadSmtpResponse(sslStream);

            // 6. AUTH LOGIN
            SendSmtpCommand(sslStream, "AUTH LOGIN\r\n");
            ReadSmtpResponse(sslStream);

            // Отправляем логин (base64)
            SendSmtpCommand(sslStream, ToBase64(SenderEmail) + "\r\n");
            ReadSmtpResponse(sslStream);

            // Отправляем пароль (base64)
            SendSmtpCommand(sslStream, ToBase64(SenderPassword) + "\r\n");
            ReadSmtpResponse(sslStream);

            // 7. MAIL FROM / RCPT TO / DATA
            SendSmtpCommand(sslStream, $"MAIL FROM:<{SenderEmail}>\r\n");
            ReadSmtpResponse(sslStream);

            SendSmtpCommand(sslStream, $"RCPT TO:<{RecipientEmail}>\r\n");
            ReadSmtpResponse(sslStream);

            SendSmtpCommand(sslStream, "DATA\r\n");
            ReadSmtpResponse(sslStream);

            // Формируем письмо
            // Включаем "Subject:...", пустая строка, текст письма, завершаем точкой
            string message =
                $"Subject: {Subject}\r\n" +
                $"\r\n" +
                $"{Body}\r\n" +
                ".\r\n";  // точка -> конец DATA

            SendSmtpCommand(sslStream, message);
            ReadSmtpResponse(sslStream);

            // 8. QUIT
            SendSmtpCommand(sslStream, "QUIT\r\n");
            ReadSmtpResponse(sslStream);

            Console.WriteLine("Email sent successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    /// <summary>
    /// Отправка команды (string) в SMTP-сервер по нешифрованному NetworkStream
    /// </summary>
    private static void SendSmtpCommand(Stream networkStream, string command)
    {
        var buffer = Encoding.ASCII.GetBytes(command);
        networkStream.Write(buffer, 0, buffer.Length);
        networkStream.Flush();
        Console.WriteLine($">>> {command.TrimEnd('\r', '\n')}");
    }

    /// <summary>
    /// Чтение ответа (одной порции) от SMTP-сервера, plain TCP
    /// </summary>
    private static void ReadSmtpResponse(Stream networkStream)
    {
        var buffer = new byte[1024];
        int bytesRead = networkStream.Read(buffer, 0, buffer.Length);
        if (bytesRead > 0)
        {
            var response = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.WriteLine(response);
        }
    }

    /// <summary>
    /// Утилита Base64-кодирования строки (логин/пароль)
    /// </summary>
    private static string ToBase64(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return Convert.ToBase64String(bytes);
    }
}
