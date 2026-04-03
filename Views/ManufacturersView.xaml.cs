using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using SolarWarehouseApp.Data;
using SolarWarehouseApp.Services;

namespace SolarWarehouseApp.Views
{
    public partial class ManufacturersView : Page
    {
        private readonly DatabaseService _dbService;
        private readonly LogService? _logService;

        public ManufacturersView(DatabaseService dbService, LogService? logService)
        {
            InitializeComponent();
            _dbService = dbService;
            _logService = logService;
            Loaded += (s, e) => LoadData();
        }

        private void LoadData()
        {
            var result = _dbService.ExecuteQuery("SELECT Id, Name, Country, IsActive FROM Manufacturers ORDER BY Name");
            Grid.ItemsSource = result?.DefaultView;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputTextDialog("Новий виробник", "Назва:", "Країна:");
            if (dialog.ShowDialog() == true)
            {
                if (string.IsNullOrWhiteSpace(dialog.Value2))
                {
                    MessageBox.Show("Заповніть всі поля!", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                string query = "INSERT INTO Manufacturers (Name, Country, IsActive) VALUES (@name, @country, 1)";
                SqlParameter[] p = { new("@name", dialog.Value1), new("@country", dialog.Value2) };
                if (_dbService.ExecuteNonQueryWithParameters(query, p))
                {
                    _logService?.LogEvent("CREATE", "Manufacturers", null, $"Added: {dialog.Value1}");
                    LoadData();
                }
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();

        private void Grid_DoubleClick(object sender, MouseButtonEventArgs e) => EditSelected();

        private void EditSelected()
        {
            if (Grid.SelectedItem is not DataRowView row)
            {
                MessageBox.Show("Виберіть запис!", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            int id = (int)row["Id"];
            string name = row["Name"].ToString() ?? "";
            string country = row["Country"].ToString() ?? "";

            var dialog = new InputTextDialog("Редагування виробника", "Назва:", "Країна:", name, country);
            if (dialog.ShowDialog() == true)
            {
                string query = "UPDATE Manufacturers SET Name=@name, Country=@country WHERE Id=@id";
                SqlParameter[] p = { new("@name", dialog.Value1), new("@country", dialog.Value2), new("@id", id) };
                if (_dbService.ExecuteNonQueryWithParameters(query, p))
                {
                    _logService?.LogEvent("UPDATE", "Manufacturers", id, $"Updated ID={id}");
                    LoadData();
                }
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (Grid.SelectedItem is not DataRowView row)
            {
                MessageBox.Show("Виберіть запис!", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            int id = (int)row["Id"];
            string name = row["Name"].ToString() ?? "";
            if (MessageBox.Show($"Видалити '{name}'?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                if (_dbService.ExecuteNonQueryWithParameters("DELETE FROM Manufacturers WHERE Id=@id", new[] { new SqlParameter("@id", id) }))
                {
                    _logService?.LogEvent("DELETE", "Manufacturers", id, $"Deleted ID={id}");
                    LoadData();
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadData();
    }
}
