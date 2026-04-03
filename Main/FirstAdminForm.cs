using SolarWarehouseApp.Data;

namespace SolarWarehouseApp.Main
{
    public class FirstAdminForm : Form
    {
        // Сервіс для роботи з базою даних (створення першого адміністратора)
        private readonly DatabaseService _dbService;

        public FirstAdminForm(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            Text = "Solar Warehouse - Створення адміністратора";
            Width = 520;
            Height = 320;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Створення елементів керування форми
            CreateControls();
        }

        private void CreateControls()
        {
            // Інформаційний заголовок форми
            Label infoLabel = new Label
            {
                Text = "Створіть першого адміністратора",
                Location = new Point(20, 20),
                Width = 450,
                Font = new Font("Arial", 11, FontStyle.Bold)
            };
            Controls.Add(infoLabel);

            Label loginLabel = new Label
            {
                Text = "Логін:",
                Location = new Point(20, 60),
                Width = 130,
                Font = new Font("Arial", 10)
            };
            Controls.Add(loginLabel);

            TextBox loginBox = new TextBox
            {
                Name = "loginBox",
                Location = new Point(160, 60),
                Width = 320,
                Font = new Font("Arial", 10)
            };
            Controls.Add(loginBox);

            Label passwordLabel = new Label
            {
                Text = "Пароль:",
                Location = new Point(20, 100),
                Width = 130,
                Font = new Font("Arial", 10)
            };
            Controls.Add(passwordLabel);

            TextBox passwordBox = new TextBox
            {
                Name = "passwordBox",
                Location = new Point(160, 100),
                Width = 320,
                UseSystemPasswordChar = true,
                Font = new Font("Arial", 10)
            };
            Controls.Add(passwordBox);

            Label confirmLabel = new Label
            {
                Text = "Підтвердити:",
                Location = new Point(20, 140),
                Width = 130,
                Font = new Font("Arial", 10)
            };
            Controls.Add(confirmLabel);

            TextBox confirmBox = new TextBox
            {
                Name = "confirmBox",
                Location = new Point(160, 140),
                Width = 320,
                UseSystemPasswordChar = true,
                Font = new Font("Arial", 10)
            };
            Controls.Add(confirmBox);

            Button createButton = new Button
            {
                Text = "Створити",
                Location = new Point(160, 190),
                Width = 140,
                Height = 35,
                Font = new Font("Arial", 10)
            };
            // Обробник натискання кнопки створення адміністратора
            createButton.Click += (s, e) => CreateAdminClick(
                loginBox.Text,
                passwordBox.Text,
                confirmBox.Text
            );
            Controls.Add(createButton);

            Button cancelButton = new Button
            {
                Text = "Вихід",
                Location = new Point(310, 190),
                Width = 140,
                Height = 35,
                Font = new Font("Arial", 10)
            };
            cancelButton.Click += (s, e) => Close();
            Controls.Add(cancelButton);
        }

        private void CreateAdminClick(string login, string password, string confirm)
        {
            // Базова валідація введених даних
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Заповніть усі поля!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (password != confirm)
            {
                MessageBox.Show("Паролі не збігаються!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (login.Length < 3)
            {
                MessageBox.Show("Логін повинен мати щонайменше 3 символи!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (password.Length < 6)
            {
                MessageBox.Show("Пароль повинен мати щонайменше 6 символів!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Перевірка, що користувач з таким логіном ще не існує
            string checkQuery = $"SELECT COUNT(*) as cnt FROM AppUsers WHERE Login = '{login}'";
            var checkResult = _dbService.ExecuteQuery(checkQuery);

            if (checkResult != null && checkResult.Rows.Count > 0)
            {
                int count = int.Parse(checkResult.Rows[0]["cnt"].ToString() ?? "0");
                if (count > 0)
                {
                    MessageBox.Show("Користувач з таким логіном вже існує!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            try
            {
                // Додавання першого адміністратора до таблиці AppUsers
                string insertQuery = $@"
                    INSERT INTO AppUsers (Login, PasswordHash, Role, IsActive, UpdatedAt)
                    VALUES ('{login}', '{password}', 'Admin', 1, GETDATE())";

                bool success = _dbService.ExecuteNonQuery(insertQuery);

                if (success)
                {
                    MessageBox.Show("Адміністратор створений успішно!\nПерезапустіть програму.", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    MessageBox.Show("Помилка при створенні адміністратора.", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}