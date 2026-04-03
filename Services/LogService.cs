using System.Text;
using SolarWarehouseApp.Data;

namespace SolarWarehouseApp.Services
{
    public class LogService
    {
        private readonly string _logsFolder;
        private string _currentSessionLogPath = "";
        private StreamWriter? _logWriter;
        private StringBuilder? _logContent;
        private int _currentUserId;
        private string _currentUserLogin = "";
        private readonly DatabaseService _dbService;

        public LogService(DatabaseService dbService)
        {
            _dbService = dbService;

            string exePath = AppDomain.CurrentDomain.BaseDirectory;

            // Всі файли логів зберігаються в підпапці "logs" поруч із виконуваним файлом
            _logsFolder = Path.Combine(exePath, "logs");
            Directory.CreateDirectory(_logsFolder);
        }

        public string StartSession(int userId, string userLogin)
        {
            _currentUserId = userId;
            _currentUserLogin = userLogin;
            _logContent = new StringBuilder();

            // Створюємо підкаталог для поточної дати, щоб групувати логи по днях
            string todayFolder = Path.Combine(_logsFolder, DateTime.Now.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(todayFolder);

            // Ім'я файлу логу для поточної сесії
            string logFileName = $"log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
            _currentSessionLogPath = Path.Combine(todayFolder, logFileName);

            _logWriter = new StreamWriter(_currentSessionLogPath, true, Encoding.UTF8)
            {
                AutoFlush = true
            };

            // Фіксуємо початок сесії користувача
            LogEvent("LOGIN", null, null, $"User '{userLogin}' logged in");

            return _currentSessionLogPath;
        }

        public void LogEvent(string actionType, string? tableName, int? recordId, string description)
        {
            if (_logWriter == null || _logContent == null) return;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Формування текстового рядка логу за узгодженим форматом
            string logLine = $"[{timestamp}][{actionType}]";

            if (!string.IsNullOrEmpty(tableName))
                logLine += $"[{tableName}]";

            if (recordId.HasValue)
                logLine += $"[ID={recordId}]";

            logLine += $" {description}";

            _logWriter.WriteLine(logLine);
            _logContent.AppendLine(logLine);
        }

        public void EndSession()
        {
            if (_logWriter == null || _logContent == null) return;

            // Фіксуємо завершення сесії користувача
            LogEvent("LOGOUT", null, null, $"User '{_currentUserLogin}' logged out");

            _logWriter.Close();
            _logWriter.Dispose();
            _logWriter = null;

            // Після завершення сесії зберігаємо вміст логу до бази даних
            string logFileName = Path.GetFileName(_currentSessionLogPath);
            byte[] fileContent = Encoding.UTF8.GetBytes(_logContent.ToString());

            _dbService.SaveLogToDatabase(_currentUserId, logFileName, fileContent);
        }

        public string ReadLogFile(byte[] logFileContent)
        {
            if (logFileContent == null || logFileContent.Length == 0)
                return "ERROR: Log file not found";

            // Перетворення вмісту файлу логу з байтів у текст UTF‑8
            return Encoding.UTF8.GetString(logFileContent);
        }
    }
}