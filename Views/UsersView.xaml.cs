using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using SolarWarehouseApp.Data;
using SolarWarehouseApp.Services;

namespace SolarWarehouseApp.Views
{
    public partial class UsersView : Page
    {
        private readonly DatabaseService _dbService;
        private readonly LogService? _logService;

        public UsersView(DatabaseService dbService, LogService? logService)
        {
            InitializeComponent();
            _dbService = dbService;
            _logService = logService;
            Loaded += (s, e) => LoadData();
        }

        private void LoadData()
        {
            var result = _dbService.ExecuteQuery("SELECT Id, Login, PasswordHash, Role, IsActive, UpdatedAt FROM AppUsers ORDER BY Login");
            Grid.ItemsSource = result?.DefaultView;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new UserEditDialog("Додати користувача");
            if (dialog.ShowDialog() == true)
            {
                string query = "INSERT INTO AppUsers (Login, PasswordHash, Role, IsActive, UpdatedAt) VALUES (@login, @pass, @role, @active, GETDATE())";
                SqlParameter[] p =
                {
                    new("@login", dialog.UserLogin),
                    new("@pass", dialog.UserPassword),
                    new("@role", dialog.UserRole),
                    new("@active", dialog.IsActive)
                };
                if (_dbService.ExecuteNonQueryWithParameters(query, p))
                {
                    _logService?.LogEvent("CREATE", "AppUsers", null, $"Added: {dialog.UserLogin}");
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
            string login = row["Login"].ToString() ?? "";
            string role = row["Role"].ToString() ?? "Operator";
            bool isActive = (bool)(row["IsActive"] ?? true);

            var dialog = new UserEditDialog("Редагувати користувача", login, role, isActive, isEditMode: true);
            if (dialog.ShowDialog() == true)
            {
                string query;
                SqlParameter[] p;

                if (!string.IsNullOrWhiteSpace(dialog.UserPassword))
                {
                    query = "UPDATE AppUsers SET Role=@role, IsActive=@active, PasswordHash=@pass, UpdatedAt=GETDATE() WHERE Id=@id";
                    p = new[] { new SqlParameter("@role", dialog.UserRole), new SqlParameter("@active", dialog.IsActive),
                                new SqlParameter("@pass", dialog.UserPassword), new SqlParameter("@id", id) };
                }
                else
                {
                    query = "UPDATE AppUsers SET Role=@role, IsActive=@active, UpdatedAt=GETDATE() WHERE Id=@id";
                    p = new[] { new SqlParameter("@role", dialog.UserRole), new SqlParameter("@active", dialog.IsActive),
                                new SqlParameter("@id", id) };
                }

                if (_dbService.ExecuteNonQueryWithParameters(query, p))
                {
                    _logService?.LogEvent("UPDATE", "AppUsers", id, $"Updated: {login}");
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
            string login = row["Login"].ToString() ?? "";
            if (MessageBox.Show($"Видалити користувача '{login}'?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                if (_dbService.ExecuteNonQueryWithParameters("DELETE FROM AppUsers WHERE Id=@id", new[] { new SqlParameter("@id", id) }))
                {
                    _logService?.LogEvent("DELETE", "AppUsers", id, $"Deleted: {login}");
                    LoadData();
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadData();
    }
}
