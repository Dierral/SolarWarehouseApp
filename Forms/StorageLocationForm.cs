namespace SolarWarehouseApp.Forms
{
    // Форма для опису конкретного місця зберігання на складі
    public class StorageLocationForm : Form
    {
        private TextBox? warehouseNameTextBox;
        private TextBox? rackTextBox;
        private TextBox? shelfTextBox;
        private TextBox? descriptionTextBox;
        private Button? okButton;
        private Button? cancelButton;

        public string Warehouse { get; set; } = "";
        public string Rack { get; set; } = "";
        public string Shelf { get; set; } = "";
        public string Description { get; set; } = "";

        public StorageLocationForm(string warehouse = "", string rack = "", string shelf = "", string description = "")
        {
            Warehouse = warehouse;
            Rack = rack;
            Shelf = shelf;
            Description = description;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Місце зберігання";
            Width = 400;
            Height = 350;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            Label warehouseLabel = new Label
            {
                Text = "Склад:",
                Location = new Point(10, 20),
                Width = 100
            };
            Controls.Add(warehouseLabel);

            warehouseNameTextBox = new TextBox
            {
                Location = new Point(110, 20),
                Width = 270,
                Text = Warehouse
            };
            Controls.Add(warehouseNameTextBox);

            Label rackLabel = new Label
            {
                Text = "Стелаж:",
                Location = new Point(10, 60),
                Width = 100
            };
            Controls.Add(rackLabel);

            rackTextBox = new TextBox
            {
                Location = new Point(110, 60),
                Width = 270,
                Text = Rack
            };
            Controls.Add(rackTextBox);

            Label shelfLabel = new Label
            {
                Text = "Полиця:",
                Location = new Point(10, 100),
                Width = 100
            };
            Controls.Add(shelfLabel);

            shelfTextBox = new TextBox
            {
                Location = new Point(110, 100),
                Width = 270,
                Text = Shelf
            };
            Controls.Add(shelfTextBox);

            Label descriptionLabel = new Label
            {
                Text = "Опис:",
                Location = new Point(10, 140),
                Width = 100
            };
            Controls.Add(descriptionLabel);

            descriptionTextBox = new TextBox
            {
                Location = new Point(110, 140),
                Width = 270,
                Height = 100,
                Multiline = true,
                Text = Description
            };
            Controls.Add(descriptionTextBox);

            okButton = new Button
            {
                Text = "OK",
                Location = new Point(200, 260),
                Width = 80
            };
            // Після натискання ОК передаємо введені значення у властивості форми
            okButton.Click += (s, e) => OkClick();
            Controls.Add(okButton);

            cancelButton = new Button
            {
                Text = "Скасувати",
                Location = new Point(300, 260),
                Width = 80
            };
            cancelButton.Click += (s, e) => DialogResult = DialogResult.Cancel;
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private void OkClick()
        {
            if (warehouseNameTextBox != null) Warehouse = warehouseNameTextBox.Text;
            if (rackTextBox != null) Rack = rackTextBox.Text;
            if (shelfTextBox != null) Shelf = shelfTextBox.Text;
            if (descriptionTextBox != null) Description = descriptionTextBox.Text;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}