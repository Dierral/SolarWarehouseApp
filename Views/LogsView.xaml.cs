using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SolarWarehouseApp.Data;
using SolarWarehouseApp.Services;

namespace SolarWarehouseApp.Views
{
    public partial class LogsView : Page
    {
        private readonly DatabaseService _dbService;
        private readonly LogService? _logService;
        private bool _isLoading = false;

        public LogsView(DatabaseService dbService, LogService? logService)
        {
            InitializeComponent();
            _dbService = dbService;
            _logService = logService;
            Loaded += LogsView_Loaded;
        }

        private void LogsView_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            LoadUserFilter();
            _isLoading = false;
            LoadLogs();
        }

        private void LoadUserFilter()
        {
            UserFilter.Items.Add(new FilterItem { Id = 0, Name = "(Всі користувачі)" });
            var result = _dbService.ExecuteQuery("SELECT Id, Login FROM AppUsers WHERE IsActive = 1 ORDER BY Login");
            if (result != null)
                foreach (DataRow row in result.Rows)
                    UserFilter.Items.Add(new FilterItem { Id = (int)row["Id"], Name = row["Login"].ToString() ?? "" });
            UserFilter.SelectedIndex = 0;
        }

        private void LoadLogs()
        {
            int? userId = null;
            DateTime? dateFrom = null;
            DateTime? dateTo = null;

            if (UserFilter.SelectedItem is FilterItem fi && fi.Id > 0)
                userId = fi.Id;

            if (DateFrom.SelectedDate.HasValue) dateFrom = DateFrom.SelectedDate.Value;
            if (DateTo.SelectedDate.HasValue) dateTo = DateTo.SelectedDate.Value;

            var result = _dbService.GetLogs(userId, dateFrom, dateTo);
            LogsGrid.ItemsSource = result?.DefaultView;
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (!_isLoading) LoadLogs();
        }

        private void ResetFilter_Click(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            UserFilter.SelectedIndex = 0;
            DateFrom.SelectedDate = null;
            DateTo.SelectedDate = null;
            _isLoading = false;
            LoadLogs();
        }

        private void ViewLog_Click(object sender, RoutedEventArgs e) => ViewSelectedLog();

        private void LogsGrid_DoubleClick(object sender, MouseButtonEventArgs e) => ViewSelectedLog();

        private void ViewSelectedLog()
        {
            if (LogsGrid.SelectedItem is not DataRowView row)
            {
                MessageBox.Show("Виберіть лог для перегляду!", "Попередження",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int logId = (int)row["LogId"];
            _logService?.LogEvent("VIEW_LOG", "Logs", logId, "");

            byte[] fileContent = _dbService.GetLogFileContent(logId);
            string logText = _logService?.ReadLogFile(fileContent) ?? System.Text.Encoding.UTF8.GetString(fileContent);

            // Show in a new window
            var viewWindow = new Window
            {
                Title = $"Лог #{logId} — {row["UserLogin"]}",
                Width = 900,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E))
            };

            var textBox = new TextBox
            {
                Text = logText,
                IsReadOnly = true,
                FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New"),
                FontSize = 12,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x4E, 0xC9, 0xB0)),
                BorderThickness = new Thickness(0),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(12)
            };

            viewWindow.Content = textBox;
            viewWindow.ShowDialog();
        }

        private void DeleteLog_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = LogsGrid.SelectedItems;
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Виберіть логи для видалення!", "Попередження",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Видалити {selectedItems.Count} лог(ів)?", "Підтвердження",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var logIds = selectedItems.Cast<DataRowView>()
                    .Select(r => (int)r["LogId"]).ToArray();

                if (_dbService.DeleteLogs(logIds))
                {
                    _logService?.LogEvent("DELETE", "Logs", null, $"Deleted {logIds.Length} logs");
                    MessageBox.Show("Логи видалено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadLogs();
                }
            }
        }
    }
}
