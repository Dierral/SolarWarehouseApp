using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using SolarWarehouseApp.Data;
using SolarWarehouseApp.Services;

namespace SolarWarehouseApp.Views
{
    public partial class EquipmentTypesView : Page
    {
        private readonly DatabaseService _dbService;
        private readonly LogService? _logService;

        public EquipmentTypesView(DatabaseService dbService, LogService? logService)
        {
            InitializeComponent();
            _dbService = dbService;
            _logService = logService;
            Loaded += (s, e) => LoadData();
        }

        private void LoadData()
        {
            var result = _dbService.ExecuteQuery("SELECT Id, Name, Description, IsActive FROM EquipmentTypes ORDER BY Name");
            Grid.ItemsSource = result?.DefaultView;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputTextDialog("Нова категорія обладнання", "Назва:", "Опис:");
            if (dialog.ShowDialog() == true)
            {
                string query = "INSERT INTO EquipmentTypes (Name, Description, IsActive) VALUES (@name, @desc, 1)";
                SqlParameter[] p = { new("@name", dialog.Value1), new("@desc", dialog.Value2) };
                if (_dbService.ExecuteNonQueryWithParameters(query, p))
                {
                    _logService?.LogEvent("CREATE", "EquipmentTypes", null, $"Added: {dialog.Value1}");
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
            string desc = row["Description"].ToString() ?? "";

            var dialog = new InputTextDialog("Редагування категорії", "Назва:", "Опис:", name, desc);
            if (dialog.ShowDialog() == true)
            {
                string query = "UPDATE EquipmentTypes SET Name=@name, Description=@desc WHERE Id=@id";
                SqlParameter[] p = { new("@name", dialog.Value1), new("@desc", dialog.Value2), new("@id", id) };
                if (_dbService.ExecuteNonQueryWithParameters(query, p))
                {
                    _logService?.LogEvent("UPDATE", "EquipmentTypes", id, $"Updated ID={id}");
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
                string query = "DELETE FROM EquipmentTypes WHERE Id=@id";
                if (_dbService.ExecuteNonQueryWithParameters(query, new[] { new SqlParameter("@id", id) }))
                {
                    _logService?.LogEvent("DELETE", "EquipmentTypes", id, $"Deleted ID={id}");
                    LoadData();
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadData();
    }
}
