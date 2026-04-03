namespace SolarWarehouseApp.Forms
{
    // Універсальне діалогове вікно для введення рядкового значення
    public class InputDialog : Form
    {
        public string InputValue { get; private set; } = "";

        public InputDialog(string title, string label, string defaultValue = "")
        {
            Text = title;
            Width = 500;
            Height = 220;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            Label labelControl = new Label
            {
                Text = label,
                Location = new Point(20, 20),
                Width = 460,
                AutoSize = true,
                Font = new Font("Arial", 10)
            };
            Controls.Add(labelControl);

            TextBox textBox = new TextBox
            {
                Location = new Point(20, 50),
                Width = 460,
                Font = new Font("Arial", 10),
                Text = defaultValue
            };
            Controls.Add(textBox);

            Button okButton = new Button
            {
                Text = "ОК",
                Location = new Point(230, 130),
                Width = 100,
                Height = 30,
                Font = new Font("Arial", 10)
            };
            // Повертаємо введене значення при натисканні ОК
            okButton.Click += (s, e) =>
            {
                InputValue = textBox.Text;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(okButton);

            Button cancelButton = new Button
            {
                Text = "Скасувати",
                Location = new Point(355, 130),
                Width = 100,
                Height = 30,
                Font = new Font("Arial", 10)
            };
            cancelButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            Controls.Add(cancelButton);
        }
    }
}