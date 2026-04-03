using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SolarWarehouseApp.Views
{
    /// <summary>
    /// Generic multi-field input dialog for CRUD operations.
    /// Supports up to 4 text fields, an optional checkbox, and an optional role combo.
    /// </summary>
    public partial class InputTextDialog : Window
    {
        public string Value1 { get; private set; } = "";
        public string Value2 { get; private set; } = "";
        public string Value3 { get; private set; } = "";
        public string Value4 { get; private set; } = "";
        public bool IsActive { get; private set; } = true;
        public string SelectedRole { get; private set; } = "Operator";

        private readonly string _label1;
        private readonly string _label2;
        private readonly string _label3;
        private readonly string _label4;
        private readonly bool _showIsActive;
        private readonly bool _showRole;
        private readonly bool _field1ReadOnly;

        public InputTextDialog(string title, string label1, string label2 = "",
            string default1 = "", string default2 = "")
        {
            InitializeComponent();
            _label1 = label1; _label2 = label2;
            _label3 = ""; _label4 = "";

            Setup(title, label1, label2, "", "", default1, default2, "", "");
        }

        public InputTextDialog(string title, string label1, string label2, string label3,
            string default1 = "", string default2 = "", string default3 = "")
        {
            InitializeComponent();
            Setup(title, label1, label2, label3, "", default1, default2, default3, "");
        }

        public InputTextDialog(string title, string label1, string label2, string label3, string label4,
            string default1 = "", string default2 = "", string default3 = "", string default4 = "",
            bool showIsActive = false, bool showRole = false, bool field1ReadOnly = false,
            bool isActiveDefault = true, string roleDefault = "Operator")
        {
            InitializeComponent();
            _showIsActive = showIsActive;
            _showRole = showRole;
            _field1ReadOnly = field1ReadOnly;
            Setup(title, label1, label2, label3, label4, default1, default2, default3, default4,
                showIsActive, showRole, field1ReadOnly, isActiveDefault, roleDefault);
        }

        private void Setup(string title, string l1, string l2, string l3, string l4,
            string d1, string d2, string d3, string d4,
            bool showIsActive = false, bool showRole = false, bool f1ReadOnly = false,
            bool isActiveDefault = true, string roleDefault = "Operator")
        {
            TitleText.Text = title;
            Title = title;

            SetField(Field1Box, l1, d1, f1ReadOnly);

            if (!string.IsNullOrEmpty(l2))
                SetField(Field2Box, l2, d2, false, true);

            if (!string.IsNullOrEmpty(l3))
                SetField(Field3Box, l3, d3, false, true);

            if (!string.IsNullOrEmpty(l4))
                SetField(Field4Box, l4, d4, false, true);

            if (showIsActive)
            {
                IsActiveCheck.Visibility = Visibility.Visible;
                IsActiveCheck.IsChecked = isActiveDefault;
            }

            if (showRole)
            {
                RoleCombo.Visibility = Visibility.Visible;
                RoleCombo.Items.Add("Admin");
                RoleCombo.Items.Add("Operator");
                RoleCombo.SelectedItem = roleDefault;
            }
        }

        private static void SetField(TextBox box, string hint, string defaultValue, bool readOnly, bool show = false)
        {
            if (show) box.Visibility = Visibility.Visible;
            MaterialDesignThemes.Wpf.HintAssist.SetHint(box, hint);
            box.Text = defaultValue;
            box.IsReadOnly = readOnly;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Field1Box.Text))
            {
                MessageBox.Show("Заповніть перше поле!", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Value1 = Field1Box.Text.Trim();
            Value2 = Field2Box.Text.Trim();
            Value3 = Field3Box.Text.Trim();
            Value4 = Field4Box.Text.Trim();
            IsActive = IsActiveCheck.IsChecked ?? true;
            SelectedRole = RoleCombo.SelectedItem?.ToString() ?? "Operator";
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Field_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox tb && !tb.AcceptsReturn)
                Ok_Click(sender, new RoutedEventArgs());
        }
    }
}
