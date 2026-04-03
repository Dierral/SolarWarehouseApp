using System.Windows;
using Microsoft.Data.SqlClient;
using SolarWarehouseApp.Data;

namespace SolarWarehouseApp.Views
{
    public partial class FirstAdminView : Window
    {
        private readonly DatabaseService _dbService;

        public FirstAdminView(DatabaseService dbService)
        {
            InitializeComponent();
            _dbService = dbService;
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginBox.Text.Trim();
            string password = PasswordBox.Password;
            string confirm = ConfirmBox.Password;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                ShowStatus("Заповніть усі поля!", isError: true);
                return;
            }

            if (password != confirm)
            {
                ShowStatus("Паролі не збігаються!", isError: true);
                return;
            }

            if (login.Length < 3)
            {
                ShowStatus("Логін повинен мати щонайменше 3 символи!", isError: true);
                return;
            }

            if (password.Length < 6)
            {
                ShowStatus("Пароль повинен мати щонайменше 6 символів!", isError: true);
                return;
            }

            // Use parameterized query to prevent SQL injection
            string checkQuery = "SELECT COUNT(*) as cnt FROM AppUsers WHERE Login = @login";
            SqlParameter[] checkParams = { new SqlParameter("@login", login) };
            var checkResult = _dbService.ExecuteQueryWithParameters(checkQuery, checkParams);

            if (checkResult != null && checkResult.Rows.Count > 0)
            {
                int count = int.Parse(checkResult.Rows[0]["cnt"].ToString() ?? "0");
                if (count > 0)
                {
                    ShowStatus("Користувач з таким логіном вже існує!", isError: true);
                    return;
                }
            }

            try
            {
                string insertQuery = "INSERT INTO AppUsers (Login, PasswordHash, Role, IsActive, UpdatedAt) VALUES (@login, @password, 'Admin', 1, GETDATE())";
                SqlParameter[] insertParams =
                {
                    new SqlParameter("@login", login),
                    new SqlParameter("@password", password)
                };

                bool success = _dbService.ExecuteNonQueryWithParameters(insertQuery, insertParams);

                if (success)
                {
                    MessageBox.Show("Адміністратор створений успішно!\nПерезапустіть програму.", "Успіх",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                else
                {
                    ShowStatus("Помилка при створенні адміністратора.", isError: true);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Помилка: {ex.Message}", isError: true);
            }
        }

        private void ShowStatus(string message, bool isError = false)
        {
            StatusMessage.Text = message;
            StatusMessage.Foreground = isError
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEF, 0x53, 0x50))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x66, 0xBB, 0x6A));
            StatusMessage.Visibility = Visibility.Visible;
        }
    }
}
