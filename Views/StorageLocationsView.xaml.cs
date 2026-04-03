using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using SolarWarehouseApp.Data;
using SolarWarehouseApp.Services;

namespace SolarWarehouseApp.Views
{
    public partial class StorageLocationsView : Page
    {
        private readonly DatabaseService _dbService;
        private readonly LogService? _logService;

        public StorageLocationsView(DatabaseService dbService, LogService? logService)
        {
            InitializeComponent();
            _dbService = dbService;
            _logService = logService;
            Loaded += (s, e) => LoadData();
        }

        private void LoadData()
        {
            var result = _dbService.ExecuteQuery("SELECT Id, WarehouseName, Rack, Shelf, Description, IsActive FROM StorageLocations ORDER BY WarehouseName, Rack, Shelf");
            Grid.ItemsSource = result?.DefaultView;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputTextDialog("Нове місце зберігання", "Склад:", "Стелаж:", "Полиця:", "Опис:");
            if (dialog.ShowDialog() == true)
            {
                string query = "INSERT INTO StorageLocations (WarehouseName, Rack, Shelf, Description, IsActive) VALUES (@wh, @rack, @shelf, @desc, 1)";
                SqlParameter[] p =
                {
                    new("@wh", dialog.Value1), new("@rack", dialog.Value2),
                    new("@shelf", dialog.Value3), new("@desc", dialog.Value4)
                };
                if (_dbService.ExecuteNonQueryWithParameters(query, p))
                {
                    _logService?.LogEvent("CREATE", "StorageLocations", null, $"Added: {dialog.Value1}-{dialog.Value2}-{dialog.Value3}");
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
            string wh = row["WarehouseName"].ToString() ?? "";
            string rack = row["Rack"].ToString() ?? "";
            string shelf = row["Shelf"].ToString() ?? "";
            string desc = row["Description"].ToString() ?? "";

            var dialog = new InputTextDialog("Редагування місця зберігання",
                "Склад:", "Стелаж:", "Полиця:", "Опис:", wh, rack, shelf, desc);
            if (dialog.ShowDialog() == true)
            {
                string query = "UPDATE StorageLocations SET WarehouseName=@wh, Rack=@rack, Shelf=@shelf, Description=@desc WHERE Id=@id";
                SqlParameter[] p =
                {
                    new("@wh", dialog.Value1), new("@rack", dialog.Value2),
                    new("@shelf", dialog.Value3), new("@desc", dialog.Value4),
                    new("@id", id)
                };
                if (_dbService.ExecuteNonQueryWithParameters(query, p))
                {
                    _logService?.LogEvent("UPDATE", "StorageLocations", id, $"Updated ID={id}");
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
            string name = $"{row["WarehouseName"]}-{row["Rack"]}-{row["Shelf"]}";
            if (MessageBox.Show($"Видалити '{name}'?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                if (_dbService.ExecuteNonQueryWithParameters("DELETE FROM StorageLocations WHERE Id=@id", new[] { new SqlParameter("@id", id) }))
                {
                    _logService?.LogEvent("DELETE", "StorageLocations", id, $"Deleted ID={id}");
                    LoadData();
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadData();
    }
}
