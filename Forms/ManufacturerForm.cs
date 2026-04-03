namespace SolarWarehouseApp.Forms
{
    // Форма для створення / редагування виробників (довідник Manufacturers)
    public class ManufacturerForm : Form
    {
        public string ItemName { get; private set; } = "";
        public string Country { get; private set; } = "";

        private TextBox? nameBox;
        private TextBox? countryBox;

        public ManufacturerForm(string name = "", string country = "")
        {
            ItemName = name;
            Country = country;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            Text = string.IsNullOrEmpty(ItemName) ? "Новий виробник" : "Редагування виробника";
            Width = 500;
            Height = 220;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Побудова елементів керування форми
            CreateControls();
        }

        private void CreateControls()
        {
            Label nameLabel = new Label
            {
                Text = "Назва:",
                Location = new Point(20, 20),
                Width = 100,
                Font = new Font("Arial", 10)
            };
            Controls.Add(nameLabel);

            nameBox = new TextBox
            {
                Location = new Point(130, 20),
                Width = 340,
                Text = ItemName,
                Font = new Font("Arial", 10)
            };
            Controls.Add(nameBox);

            Label countryLabel = new Label
            {
                Text = "Країна:",
                Location = new Point(20, 60),
                Width = 100,
                Font = new Font("Arial", 10)
            };
            Controls.Add(countryLabel);

            countryBox = new TextBox
            {
                Location = new Point(130, 60),
                Width = 340,
                Text = Country,
                Font = new Font("Arial", 10)
            };
            Controls.Add(countryBox);

            Button okButton = new Button
            {
                Text = "ОК",
                Location = new Point(220, 140),
                Width = 100,
                Height = 30,
                Font = new Font("Arial", 10)
            };
            okButton.Click += (s, e) => SaveClick();
            Controls.Add(okButton);

            Button cancelButton = new Button
            {
                Text = "Скасувати",
                Location = new Point(330, 140),
                Width = 100,
                Height = 30,
                Font = new Font("Arial", 10)
            };
            cancelButton.Click += (s, e) => Close();
            Controls.Add(cancelButton);
        }

        // Перевірка введених даних і передача їх у властивості форми
        private void SaveClick()
        {
            if (string.IsNullOrWhiteSpace(nameBox?.Text) || string.IsNullOrWhiteSpace(countryBox?.Text))
            {
                MessageBox.Show("Заповніть усі поля!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ItemName = nameBox!.Text;
            Country = countryBox!.Text;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}