namespace SolarWarehouseApp.Forms
{
    // Форма для створення та редагування користувачів додатку (AppUsers)
    public partial class AppUserForm : Form
    {
        public string UserLogin { get; private set; } = "";
        public string UserPassword { get; private set; } = "";
        public string UserRole { get; private set; } = "Operator";
        public bool UserIsActive { get; private set; } = true;

        // Прапорець, що вказує на режим редагування (а не створення)
        private readonly bool _isEditMode;

        public AppUserForm()
        {
            InitializeComponent();
            _isEditMode = false;
            Text = "Додати користувача";
        }

        public AppUserForm(string login, string role, bool isActive)
        {
            InitializeComponent();
            _isEditMode = true;
            Text = "Редагувати користувача";

            // Попереднє заповнення полів для редагування
            if (Controls["loginTextBox"] is TextBox loginTextBox)
            {
                loginTextBox.Text = login;
                loginTextBox.ReadOnly = true;
            }

            if (Controls["roleComboBox"] is ComboBox roleComboBox)
                roleComboBox.SelectedItem = role;

            if (Controls["isActiveCheckBox"] is CheckBox isActiveCheckBox)
                isActiveCheckBox.Checked = isActive;

            if (Controls["passwordLabel"] is Label passwordLabel)
                passwordLabel.Text = "Пароль (залишити пусто щоб не змінювати):";
        }

        private void InitializeComponent()
        {
            Width = 400;
            Height = 280;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            Label loginLabel = new Label
            {
                Text = "Логін:",
                Location = new Point(10, 20),
                Width = 80
            };
            Controls.Add(loginLabel);

            TextBox loginTextBox = new TextBox
            {
                Name = "loginTextBox",
                Location = new Point(100, 20),
                Width = 250,
                Height = 23
            };
            Controls.Add(loginTextBox);

            Label passwordLabel = new Label
            {
                Name = "passwordLabel",
                Text = "Пароль:",
                Location = new Point(10, 60),
                Width = 80
            };
            Controls.Add(passwordLabel);

            TextBox passwordTextBox = new TextBox
            {
                Name = "passwordTextBox",
                Location = new Point(100, 60),
                Width = 250,
                Height = 23,
                UseSystemPasswordChar = true
            };
            Controls.Add(passwordTextBox);

            Label roleLabel = new Label
            {
                Text = "Роль:",
                Location = new Point(10, 100),
                Width = 80
            };
            Controls.Add(roleLabel);

            ComboBox roleComboBox = new ComboBox
            {
                Name = "roleComboBox",
                Location = new Point(100, 100),
                Width = 250,
                Height = 23
            };
            roleComboBox.Items.Add("Admin");
            roleComboBox.Items.Add("Operator");
            roleComboBox.SelectedIndex = 1;
            Controls.Add(roleComboBox);

            CheckBox isActiveCheckBox = new CheckBox
            {
                Name = "isActiveCheckBox",
                Text = "Активний",
                Location = new Point(100, 140),
                Width = 150,
                Checked = true
            };
            Controls.Add(isActiveCheckBox);

            Button okButton = new Button
            {
                Text = "Зберегти",
                Location = new Point(150, 180),
                Width = 100,
                Height = 30,
                DialogResult = DialogResult.OK
            };
            // Перед закриттям форми проводимо валідацію введених даних
            okButton.Click += (s, e) => ValidateAndClose();
            Controls.Add(okButton);

            Button cancelButton = new Button
            {
                Text = "Скасувати",
                Location = new Point(260, 180),
                Width = 100,
                Height = 30,
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(cancelButton);
        }

        private void ValidateAndClose()
        {
            if (Controls["loginTextBox"] is not TextBox loginTextBox ||
                Controls["passwordTextBox"] is not TextBox passwordTextBox ||
                Controls["roleComboBox"] is not ComboBox roleComboBox ||
                Controls["isActiveCheckBox"] is not CheckBox isActiveCheckBox)
            {
                MessageBox.Show("Помилка ініціалізації форми!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Перевірка обов'язкових полів
            if (string.IsNullOrWhiteSpace(loginTextBox.Text))
            {
                MessageBox.Show("Введіть логін!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!_isEditMode && string.IsNullOrWhiteSpace(passwordTextBox.Text))
            {
                MessageBox.Show("Введіть пароль!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Передаємо введені дані у властивості форми
            UserLogin = loginTextBox.Text;
            UserPassword = passwordTextBox.Text;
            UserRole = roleComboBox.SelectedItem?.ToString() ?? "Operator";
            UserIsActive = isActiveCheckBox.Checked;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}