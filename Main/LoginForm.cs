using SolarWarehouseApp.Helpers;
using SolarWarehouseApp.Data;

namespace SolarWarehouseApp.Main
{
    public class LoginForm : Form
    {
        // Сервіс доступу до бази даних для перевірки облікових даних
        private DatabaseService? _dbService;

        // Результат успішної авторизації
        public string? LoggedInUserId { get; private set; }
        public string? LoggedInUserLogin { get; private set; }
        public string? LoggedInUserRole { get; private set; }

        public LoginForm()
        {
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            Text = "Solar Warehouse - Логін";
            Width = 420;
            Height = 280;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Завантаження конфігурації підключення до БД та ініціалізація сервісу БД
            var config = ConfigHelper.LoadConfig();
            string connectionString = ConfigHelper.BuildConnectionString(config);
            _dbService = new DatabaseService(connectionString);

            // Побудова елементів керування форми авторизації
            CreateLoginControls();
        }

        private void CreateLoginControls()
        {
            Label loginLabel = new Label
            {
                Text = "Логін:",
                Location = new Point(20, 40),
                Width = 80,
                Font = new Font("Arial", 10)
            };
            Controls.Add(loginLabel);

            TextBox loginBox = new TextBox
            {
                Name = "loginBox",
                Location = new Point(110, 40),
                Width = 270,
                Font = new Font("Arial", 10)
            };
            Controls.Add(loginBox);

            Label passwordLabel = new Label
            {
                Text = "Пароль:",
                Location = new Point(20, 90),
                Width = 80,
                Font = new Font("Arial", 10)
            };
            Controls.Add(passwordLabel);

            TextBox passwordBox = new TextBox
            {
                Name = "passwordBox",
                Location = new Point(110, 90),
                Width = 270,
                UseSystemPasswordChar = true,
                Font = new Font("Arial", 10)
            };
            Controls.Add(passwordBox);

            Button loginButton = new Button
            {
                Text = "Логін",
                Location = new Point(110, 170),
                Width = 120,
                Height = 35,
                Font = new Font("Arial", 10)
            };
            // Авторизація при натисканні кнопки
            loginButton.Click += (s, e) => LoginClick(loginBox.Text, passwordBox.Text);
            Controls.Add(loginButton);

            Button cancelButton = new Button
            {
                Text = "Вихід",
                Location = new Point(240, 170),
                Width = 120,
                Height = 35,
                Font = new Font("Arial", 10)
            };
            cancelButton.Click += (s, e) => Close();
            Controls.Add(cancelButton);
        }

        private void LoginClick(string login, string password)
        {
            // Перевірка заповнення полів логіна та пароля
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Введіть логін та пароль!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Запит на перевірку облікових даних у таблиці AppUsers
            string query = $@"
                SELECT Id, Login, Role 
                FROM AppUsers 
                WHERE Login = '{login}' 
                AND PasswordHash = '{password}'  
                AND IsActive = 1";

            var result = _dbService?.ExecuteQuery(query);

            if (result != null && result.Rows.Count > 0)
            {
                // Збереження інформації про авторизованого користувача
                LoggedInUserId = result.Rows[0]["Id"].ToString();
                LoggedInUserLogin = result.Rows[0]["Login"].ToString();
                LoggedInUserRole = result.Rows[0]["Role"].ToString();
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show("Неправильний логін або пароль!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}