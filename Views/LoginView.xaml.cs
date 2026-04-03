using System.Windows;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using SolarWarehouseApp.Data;

namespace SolarWarehouseApp.Views
{
    public partial class LoginView : Window
    {
        private readonly DatabaseService _dbService;

        public string? LoggedInUserId { get; private set; }
        public string? LoggedInUserLogin { get; private set; }
        public string? LoggedInUserRole { get; private set; }

        public LoginView(DatabaseService dbService)
        {
            InitializeComponent();
            _dbService = dbService;
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            AttemptLogin();
        }

        private void LoginBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) PasswordBox.Focus();
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AttemptLogin();
        }

        private void AttemptLogin()
        {
            string login = LoginBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Введіть логін та пароль!");
                return;
            }

            // Use parameterized query to prevent SQL injection
            string query = "SELECT Id, Login, Role FROM AppUsers WHERE Login = @login AND PasswordHash = @password AND IsActive = 1";
            SqlParameter[] parameters =
            {
                new SqlParameter("@login", login),
                new SqlParameter("@password", password)
            };

            var result = _dbService.ExecuteQueryWithParameters(query, parameters);

            if (result != null && result.Rows.Count > 0)
            {
                LoggedInUserId = result.Rows[0]["Id"].ToString();
                LoggedInUserLogin = result.Rows[0]["Login"].ToString();
                LoggedInUserRole = result.Rows[0]["Role"].ToString();

                var mainWindow = new MainWindow(_dbService, LoggedInUserLogin, LoggedInUserRole);
                mainWindow.Show();
                Close();
            }
            else
            {
                ShowError("Неправильний логін або пароль!");
                PasswordBox.Clear();
            }
        }

        private void ShowError(string message)
        {
            ErrorMessage.Text = message;
            ErrorMessage.Visibility = Visibility.Visible;
        }
    }
}
