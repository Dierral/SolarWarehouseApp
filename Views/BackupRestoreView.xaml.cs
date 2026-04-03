using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using SolarWarehouseApp.Data;
using SolarWarehouseApp.Services;

namespace SolarWarehouseApp.Views
{
    public partial class BackupRestoreView : Page
    {
        private readonly DatabaseService _dbService;
        private readonly LogService? _logService;
        private System.Threading.Timer? _backupTimer;

        public BackupRestoreView(DatabaseService dbService, LogService? logService)
        {
            InitializeComponent();
            _dbService = dbService;
            _logService = logService;
            Loaded += BackupRestoreView_Loaded;
            Unloaded += BackupRestoreView_Unloaded;
        }

        private void BackupRestoreView_Loaded(object sender, RoutedEventArgs e)
        {
            int interval = LoadAutoBackupInterval();
            IntervalBox.Text = interval.ToString();
            LoadBackupList();
            StartAutoBackupTimer(interval);
        }

        private void BackupRestoreView_Unloaded(object sender, RoutedEventArgs e)
        {
            _backupTimer?.Dispose();
        }

        private void LoadBackupList()
        {
            var items = new List<BackupItem>();
            string baseDir = GetBackupBaseDirectory();

            if (!Directory.Exists(baseDir))
            {
                BackupGrid.ItemsSource = items;
                return;
            }

            foreach (var dirPath in Directory.GetDirectories(baseDir).OrderByDescending(d => d))
            {
                string dirName = Path.GetFileName(dirPath);
                items.Add(new BackupItem
                {
                    FileName = dirName,
                    Type = "📁",
                    Size = "",
                    CreatedAt = "",
                    FullPath = dirPath
                });

                foreach (var filePath in Directory.GetFiles(dirPath, "*.bak").OrderByDescending(f => f))
                {
                    var fi = new FileInfo(filePath);
                    items.Add(new BackupItem
                    {
                        FileName = fi.Name,
                        Type = "📄",
                        Size = FormatFileSize(fi.Length),
                        CreatedAt = fi.CreationTime.ToString("dd.MM.yyyy HH:mm:ss"),
                        FullPath = filePath
                    });
                }
            }

            BackupGrid.ItemsSource = items;
        }

        private void CreateBackup_Click(object sender, RoutedEventArgs e)
        {
            CreateBackupNow();
        }

        private void CreateBackupNow()
        {
            try
            {
                string backupPath = GetBackupFilePath();
                string? backupDir = Path.GetDirectoryName(backupPath);
                if (backupDir != null && !Directory.Exists(backupDir))
                    Directory.CreateDirectory(backupDir);

                string backupQuery = $"BACKUP DATABASE [SolarWarehouseDB] TO DISK = '{backupPath}'";

                if (_dbService.ExecuteNonQuery(backupQuery))
                {
                    _logService?.LogEvent("BACKUP_CREATE", null, null, backupPath);
                    MessageBox.Show($"Резервну копію успішно створено!\n\n{backupPath}", "Успіх",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadBackupList();
                }
                else
                {
                    MessageBox.Show("Помилка при створенні резервної копії!", "Помилка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка:\n{ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            if (BackupGrid.SelectedItem is not BackupItem item || item.Type != "📄")
            {
                MessageBox.Show("Виберіть файл (.bak) для відновлення!", "Попередження",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Відновити базу даних з файлу?\n{item.FileName}\n\nЦя дія перезапише поточну базу даних!",
                "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
                RestoreBackupFromFile(item.FullPath);
        }

        private void RestoreBackupFromFile(string backupFilePath)
        {
            try
            {
                if (!File.Exists(backupFilePath))
                {
                    MessageBox.Show($"Файл не знайдено:\n{backupFilePath}", "Помилка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string restoreQuery = @"
                    USE master;
                    ALTER DATABASE [SolarWarehouseDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    RESTORE DATABASE [SolarWarehouseDB] FROM DISK = @backupPath WITH REPLACE;
                    ALTER DATABASE [SolarWarehouseDB] SET MULTI_USER;";

                SqlParameter[] parameters = { new SqlParameter("@backupPath", backupFilePath) };

                if (_dbService.ExecuteNonQueryWithParameters(restoreQuery, parameters))
                {
                    _logService?.LogEvent("BACKUP_RESTORE", null, null, backupFilePath);
                    MessageBox.Show("Базу даних успішно відновлено!", "Успіх",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка:\n{ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string backupDir = GetBackupBaseDirectory();
            if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = backupDir,
                UseShellExecute = true
            });
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadBackupList();

        private void SaveInterval_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(IntervalBox.Text, out int interval) && interval > 0)
            {
                SaveAutoBackupInterval(interval);
                StartAutoBackupTimer(interval);
                AutoBackupStatus.Text = $"✓ Авто-бекап кожні {interval} хв.";
                _logService?.LogEvent("BACKUP_INTERVAL_CHANGE", null, null, $"Interval={interval}min");
            }
            else
            {
                MessageBox.Show("Введіть число більше 0!", "Попередження",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void StartAutoBackupTimer(int intervalMinutes)
        {
            _backupTimer?.Dispose();
            _backupTimer = new System.Threading.Timer(
                callback: _ => Dispatcher.Invoke(CreateBackupNow),
                state: null,
                dueTime: TimeSpan.FromMinutes(intervalMinutes),
                period: TimeSpan.FromMinutes(intervalMinutes));
        }

        #region Helpers

        private string GetBackupBaseDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
        }

        private string GetBackupFilePath()
        {
            string baseDir = GetBackupBaseDirectory();
            string dateFolderPath = Path.Combine(baseDir, DateTime.Now.ToString("dd-MM-yyyy"));
            Directory.CreateDirectory(dateFolderPath);
            return Path.Combine(dateFolderPath, $"SolarWarehouse_backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.bak");
        }

        private string GetBackupConfigPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backup_config.cfg");
        }

        private int LoadAutoBackupInterval()
        {
            try
            {
                string configPath = GetBackupConfigPath();
                if (File.Exists(configPath))
                {
                    string content = File.ReadAllText(configPath).Trim();
                    if (int.TryParse(content, out int interval) && interval > 0)
                        return interval;
                }
            }
            catch { }
            return 10;
        }

        private void SaveAutoBackupInterval(int minutes)
        {
            try { File.WriteAllText(GetBackupConfigPath(), minutes.ToString()); }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка збереження налаштувань:\n{ex.Message}", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
            if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }

        #endregion
    }

    public class BackupItem
    {
        public string FileName { get; set; } = "";
        public string Type { get; set; } = "";
        public string Size { get; set; } = "";
        public string CreatedAt { get; set; } = "";
        public string FullPath { get; set; } = "";
    }
}
