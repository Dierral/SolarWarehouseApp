namespace SolarWarehouseApp.Forms
{
    // Форма для створення / редагування типів обладнання (довідник EquipmentTypes)
    public class EquipmentTypeForm : Form
    {
        public string ItemName { get; private set; } = "";
        public string Description { get; private set; } = "";

        private TextBox? nameBox;
        private TextBox? descriptionBox;

        public EquipmentTypeForm(string name = "", string description = "")
        {
            ItemName = name;
            Description = description;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            Text = string.IsNullOrEmpty(ItemName) ? "Нова категорія обладнання" : "Редагування категорії";
            Width = 500;
            Height = 250;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Створення елементів керування на формі
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

            Label descriptionLabel = new Label
            {
                Text = "Опис:",
                Location = new Point(20, 60),
                Width = 100,
                Font = new Font("Arial", 10)
            };
            Controls.Add(descriptionLabel);

            descriptionBox = new TextBox
            {
                Location = new Point(130, 60),
                Width = 340,
                Height = 80,
                Text = Description,
                Multiline = true,
                Font = new Font("Arial", 10)
            };
            Controls.Add(descriptionBox);

            Button okButton = new Button
            {
                Text = "ОК",
                Location = new Point(220, 170),
                Width = 100,
                Height = 30,
                Font = new Font("Arial", 10)
            };
            okButton.Click += (s, e) => SaveClick();
            Controls.Add(okButton);

            Button cancelButton = new Button
            {
                Text = "Скасувати",
                Location = new Point(330, 170),
                Width = 100,
                Height = 30,
                Font = new Font("Arial", 10)
            };
            cancelButton.Click += (s, e) => Close();
            Controls.Add(cancelButton);
        }

        // Валідація та збереження введених даних
        private void SaveClick()
        {
            if (string.IsNullOrWhiteSpace(nameBox?.Text))
            {
                MessageBox.Show("Заповніть назву!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ItemName = nameBox!.Text;
            Description = descriptionBox?.Text ?? "";
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}