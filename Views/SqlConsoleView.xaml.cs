using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SolarWarehouseApp.Data;
using SolarWarehouseApp.Services;

namespace SolarWarehouseApp.Views
{
    public partial class SqlConsoleView : Page
    {
        private readonly DatabaseService _dbService;
        private readonly LogService? _logService;

        public SqlConsoleView(DatabaseService dbService, LogService? logService)
        {
            InitializeComponent();
            _dbService = dbService;
            _logService = logService;
        }

        private void Execute_Click(object sender, RoutedEventArgs e)
        {
            ExecuteQuery();
        }

        private void SqlInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5) ExecuteQuery();
        }

        private void ExecuteQuery()
        {
            string sql = SqlInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(sql)) return;

            _logService?.LogEvent("SQL", null, null, $"Executed: {sql.Substring(0, Math.Min(100, sql.Length))}");

            try
            {
                var upper = sql.TrimStart().ToUpperInvariant();
                if (upper.StartsWith("SELECT") || upper.StartsWith("WITH") || upper.StartsWith("EXEC"))
                {
                    var result = _dbService.ExecuteQuery(sql);
                    if (result != null)
                    {
                        ResultsGrid.ItemsSource = result.DefaultView;
                        StatusText.Text = $"✓ Рядків: {result.Rows.Count}";
                        StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(0x66, 0xBB, 0x6A));
                    }
                }
                else
                {
                    bool ok = _dbService.ExecuteNonQuery(sql);
                    ResultsGrid.ItemsSource = null;
                    StatusText.Text = ok ? "✓ Команда виконана успішно" : "✗ Помилка виконання";
                    StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                        ok ? System.Windows.Media.Color.FromRgb(0x66, 0xBB, 0x6A)
                           : System.Windows.Media.Color.FromRgb(0xEF, 0x53, 0x50));
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"✗ {ex.Message}";
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xEF, 0x53, 0x50));
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            SqlInput.Clear();
            ResultsGrid.ItemsSource = null;
            StatusText.Text = "";
        }
    }
}
