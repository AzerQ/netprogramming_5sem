<?php
// test.php — пример интересного CGI-скрипта для лабораторной работы

// Заголовок HTML
echo "<!DOCTYPE html>";
echo "<html lang='en'>";
echo "<head>";
echo "  <meta charset='UTF-8'>";
echo "  <title>CGI Test Script</title>";
echo "  <style>";
echo "    body { font-family: Arial, sans-serif; margin: 20px; background: #f0f0f0; }";
echo "    h1 { color: #444; }";
echo "    .box { background: #fff; padding: 10px; border-radius: 5px; margin: 10px 0; }";
echo "    pre { background: #eee; padding: 10px; }";
echo "  </style>";
echo "</head>";
echo "<body>";

echo "<h1>CGI Test Page</h1>";

// Текущее время на сервере
echo "<div class='box'>";
echo "<strong>Server time: </strong>" . date('Y-m-d H:i:s');
echo "</div>";

// Параметры запроса (GET)
echo "<div class='box'>";
echo "<strong>GET parameters:</strong>";
echo "<pre>";
print_r($_GET);
echo "</pre>";
echo "</div>";

// Переменные окружения (выборочно)
echo "<div class='box'>";
echo "<strong>Relevant environment / server variables:</strong>";
echo "<pre>";
foreach ($_SERVER as $key => $val) {
    // Покажем только некоторые ключи, содержащие 'REMOTE' или 'HTTP'
    if (stripos($key, 'REMOTE') !== false || stripos($key, 'HTTP') !== false) {
        echo "$key = $val\n";
    }
}
echo "</pre>";
echo "</div>";

// Выполним системную команду (Windows: route PRINT; Linux/macOS: route -n)
echo "<div class='box'>";
echo "<strong>System route table (Windows example):</strong>";
echo "<pre>";
// Попробуем Windows-команду, если не сработает — просто игнорируем
@system("route PRINT");
echo "</pre>";
echo "</div>";

// Демонстрационная ссылка (добавит в GET ?param=123)
echo "<div class='box'>";
echo "<p>Try clicking: <a href='?param=123'>?param=123</a></p>";
echo "</div>";

echo "</body></html>";
