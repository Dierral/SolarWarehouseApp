using System.Windows;

namespace SolarWarehouseApp.Views
{
    public partial class UserEditDialog : Window
    {
        public string UserLogin { get; private set; } = "";
        public string UserPassword { get; private set; } = "";
        public string UserRole { get; private set; } = "Operator";
        public new bool IsActive { get; private set; } = true;

        private readonly bool _isEditMode;

        public UserEditDialog(string title, string login = "", string role = "Operator",
            bool isActive = true, bool isEditMode = false)
        {
            InitializeComponent();
            _isEditMode = isEditMode;

            TitleText.Text = title;
            Title = title;

            RoleCombo.Items.Add("Admin");
            RoleCombo.Items.Add("Operator");
            RoleCombo.SelectedItem = role;

            if (isEditMode)
            {
                LoginBox.Text = login;
                LoginBox.IsReadOnly = true;
                MaterialDesignThemes.Wpf.HintAssist.SetHint(PasswordBox, "Пароль (залишити пустим щоб не змінювати)");
            }

            IsActiveCheck.IsChecked = isActive;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(LoginBox.Text))
            {
                MessageBox.Show("Введіть логін!", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!_isEditMode && string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                MessageBox.Show("Введіть пароль!", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            UserLogin = LoginBox.Text.Trim();
            UserPassword = PasswordBox.Password;
            UserRole = RoleCombo.SelectedItem?.ToString() ?? "Operator";
            IsActive = IsActiveCheck.IsChecked ?? true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
