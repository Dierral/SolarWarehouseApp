using System.IO;
using System.Text.Json;

namespace SolarWarehouseApp.Helpers
{
    public class ConfigHelper
    {
        // Ім'я файлу конфігурації підключення до СУБД
        private const string CONFIG_FILE = "connectionConfig.json";

        // Модель для збереження параметрів підключення
        public class ConnectionConfig
        {
            public string Server { get; set; } = "localhost\\SQLEXPRESS";
            public string Database { get; set; } = "SolarWarehouseDB";
            public string UserId { get; set; } = "sa";
            public string Password { get; set; } = "Poipoipo_123123";
        }

        public static ConnectionConfig LoadConfig()
        {
            // Спроба прочитати конфігурацію з JSON‑файлу; у разі помилки повертається конфігурація за замовчуванням
            if (File.Exists(CONFIG_FILE))
            {
                try
                {
                    string json = File.ReadAllText(CONFIG_FILE);
                    var config = JsonSerializer.Deserialize<ConnectionConfig>(json);
                    return config ?? GetDefaultConfig();
                }
                catch
                {
                    return GetDefaultConfig();
                }
            }
            return GetDefaultConfig();
        }

        public static void SaveConfig(ConnectionConfig config)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(CONFIG_FILE, json);
            }
            catch (Exception ex)
            {
                // У разі помилки збереження лише фіксуємо її в дебаг‑виводі
                System.Diagnostics.Debug.WriteLine($"Помилка збереження конфігу: {ex.Message}");
            }
        }

        public static ConnectionConfig GetDefaultConfig()
        {
            // Повертає стандартні параметри підключення
            return new ConnectionConfig();
        }

        public static string BuildConnectionString(ConnectionConfig config)
        {
            // Формування рядка підключення до SQL Server на основі об'єкта конфігурації
            return $"Server={config.Server};Database={config.Database};User Id={config.UserId};Password={config.Password};Encrypt=true;TrustServerCertificate=true;";
        }
    }
}