using System.Data;
using SolarWarehouseApp.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace SolarWarehouseApp.Forms
{
    // Форма для додавання та редагування записів про поставки (Supplies)
    public class AddEditSupplyForm : Form
    {
        private readonly DatabaseService _dbService;
        private readonly string _userLogin;
        private readonly int? _supplyId;
        private readonly bool _isEdit;

        public AddEditSupplyForm(DatabaseService dbService, string userLogin, int? supplyId = null)
        {
            _dbService = dbService;
            _userLogin = userLogin;
            _supplyId = supplyId;
            _isEdit = supplyId.HasValue;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            Text = _isEdit ? "Редагування поставки" : "Додавання нової поставки";
            Width = 550;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Створюємо всі елементи керування форми
            CreateControls();

            // Якщо працюємо в режимі редагування – завантажуємо наявні дані
            if (_isEdit && _supplyId.HasValue)
            {
                LoadSupplyData(_supplyId.Value);
            }
        }

        private void CreateControls()
        {
            int y = 20;

            Label articleLabel = new Label
            {
                Text = "Артикул:",
                Location = new Point(20, y),
                Width = 150
            };
            Controls.Add(articleLabel);

            TextBox articleBox = new TextBox
            {
                Name = "articleBox",
                Location = new Point(180, y),
                Width = 330
            };
            Controls.Add(articleBox);
            y += 40;

            Label equipmentLabel = new Label
            {
                Text = "Тип обладнання:",
                Location = new Point(20, y),
                Width = 150
            };
            Controls.Add(equipmentLabel);

            ComboBox equipmentBox = new ComboBox
            {
                Name = "equipmentBox",
                Location = new Point(180, y),
                Width = 330,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            // Заповнення списку типів обладнання
            LoadEquipmentTypes(equipmentBox);
            Controls.Add(equipmentBox);
            y += 40;

            Label manufacturerLabel = new Label
            {
                Text = "Виробник:",
                Location = new Point(20, y),
                Width = 150
            };
            Controls.Add(manufacturerLabel);

            ComboBox manufacturerBox = new ComboBox
            {
                Name = "manufacturerBox",
                Location = new Point(180, y),
                Width = 330,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            // Заповнення списку виробників
            LoadManufacturers(manufacturerBox);
            Controls.Add(manufacturerBox);
            y += 40;

            Label quantityLabel = new Label
            {
                Text = "Кількість:",
                Location = new Point(20, y),
                Width = 150
            };
            Controls.Add(quantityLabel);

            NumericUpDown quantityBox = new NumericUpDown
            {
                Name = "quantityBox",
                Location = new Point(180, y),
                Width = 330,
                Minimum = 1,
                Maximum = 100000,
                Value = 1
            };
            Controls.Add(quantityBox);
            y += 40;

            Label unitLabel = new Label
            {
                Text = "Одиниця виміру:",
                Location = new Point(20, y),
                Width = 150
            };
            Controls.Add(unitLabel);

            TextBox unitBox = new TextBox
            {
                Name = "unitBox",
                Location = new Point(180, y),
                Width = 330,
                Text = "шт."
            };
            Controls.Add(unitBox);
            y += 40;

            Label powerLabel = new Label
            {
                Text = "Потужність (W):",
                Location = new Point(20, y),
                Width = 150
            };
            Controls.Add(powerLabel);

            TextBox powerBox = new TextBox
            {
                Name = "powerBox",
                Location = new Point(180, y),
                Width = 330
            };
            Controls.Add(powerBox);
            y += 40;

            Label voltageLabel = new Label
            {
                Text = "Напруга (V):",
                Location = new Point(20, y),
                Width = 150
            };
            Controls.Add(voltageLabel);

            TextBox voltageBox = new TextBox
            {
                Name = "voltageBox",
                Location = new Point(180, y),
                Width = 330
            };
            Controls.Add(voltageBox);
            y += 40;

            Label locationLabel = new Label
            {
                Text = "Місце зберігання:",
                Location = new Point(20, y),
                Width = 150
            };
            Controls.Add(locationLabel);

            ComboBox locationBox = new ComboBox
            {
                Name = "locationBox",
                Location = new Point(180, y),
                Width = 330,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            // Заповнення списку місць зберігання
            LoadStorageLocations(locationBox);
            Controls.Add(locationBox);
            y += 40;

            Label supplyTypeLabel = new Label
            {
                Text = "Тип поставки:",
                Location = new Point(20, y),
                Width = 150
            };
            Controls.Add(supplyTypeLabel);

            ComboBox supplyTypeBox = new ComboBox
            {
                Name = "supplyTypeBox",
                Location = new Point(180, y),
                Width = 330,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            // Заповнення видів поставок (INBOUND / OUTBOUND тощо)
            LoadSupplyTypes(supplyTypeBox);
            Controls.Add(supplyTypeBox);
            y += 40;

            Label supplyDateLabel = new Label
            {
                Text = "Дата поставки:",
                Location = new Point(20, y),
                Width = 150
            };
            Controls.Add(supplyDateLabel);

            DateTimePicker supplyDatePicker = new DateTimePicker
            {
                Name = "supplyDatePicker",
                Location = new Point(180, y),
                Width = 330,
                Value = DateTime.Now
            };
            Controls.Add(supplyDatePicker);
            y += 40;

            Label supplierLabel = new Label
            {
                Text = "Постачальник:",
                Location = new Point(20, y),
                Width = 150
            };
            Controls.Add(supplierLabel);

            TextBox supplierBox = new TextBox
            {
                Name = "supplierBox",
                Location = new Point(180, y),
                Width = 330
            };
            Controls.Add(supplierBox);
            y += 40;

            Label notesLabel = new Label
            {
                Text = "Примітки:",
                Location = new Point(20, y),
                Width = 150
            };
            Controls.Add(notesLabel);

            TextBox notesBox = new TextBox
            {
                Name = "notesBox",
                Location = new Point(180, y),
                Width = 330,
                Height = 60,
                Multiline = true
            };
            Controls.Add(notesBox);
            y += 80;

            Button saveButton = new Button
            {
                Text = "Зберегти",
                Location = new Point(180, y),
                Width = 120,
                Height = 35
            };
            // Збереження поточної поставки
            saveButton.Click += (s, e) => SaveSupply();
            Controls.Add(saveButton);

            Button cancelButton = new Button
            {
                Text = "Скасувати",
                Location = new Point(310, y),
                Width = 120,
                Height = 35
            };
            cancelButton.Click += (s, e) => Close();
            Controls.Add(cancelButton);
        }

        private void LoadEquipmentTypes(ComboBox box)
        {
            var result = _dbService.ExecuteQuery("SELECT Id, Name FROM EquipmentTypes WHERE IsActive = 1 ORDER BY Name");
            if (result != null)
            {
                foreach (DataRow row in result.Rows)
                {
                    int id = (int)row["Id"];
                    string name = row["Name"].ToString() ?? "";
                    box.Items.Add(new ComboBoxItem { Id = id, Name = name });
                }
                box.DisplayMember = "Name";
                box.ValueMember = "Id";
            }
        }

        private void LoadManufacturers(ComboBox box)
        {
            var result = _dbService.ExecuteQuery("SELECT Id, Name FROM Manufacturers WHERE IsActive = 1 ORDER BY Name");
            if (result != null)
            {
                foreach (DataRow row in result.Rows)
                {
                    int id = (int)row["Id"];
                    string name = row["Name"].ToString() ?? "";
                    box.Items.Add(new ComboBoxItem { Id = id, Name = name });
                }
                box.DisplayMember = "Name";
                box.ValueMember = "Id";
            }
        }

        private void LoadStorageLocations(ComboBox box)
        {
            var result = _dbService.ExecuteQuery("SELECT Id, CONCAT(WarehouseName, '-', Rack, '-', Shelf) as Location FROM StorageLocations WHERE IsActive = 1 ORDER BY WarehouseName, Rack, Shelf");
            if (result != null)
            {
                foreach (DataRow row in result.Rows)
                {
                    int id = (int)row["Id"];
                    string location = row["Location"].ToString() ?? "";
                    box.Items.Add(new ComboBoxItem { Id = id, Name = location });
                }
                box.DisplayMember = "Name";
                box.ValueMember = "Id";
            }
        }

        private void LoadSupplyTypes(ComboBox box)
        {
            var result = _dbService.ExecuteQuery("SELECT Id, Name, Description FROM SupplyTypes WHERE IsActive = 1 ORDER BY Name");
            if (result != null)
            {
                foreach (DataRow row in result.Rows)
                {
                    int id = (int)row["Id"];
                    string description = row["Description"].ToString() ?? "";
                    box.Items.Add(new ComboBoxItem { Id = id, Name = description });
                }
                box.DisplayMember = "Name";
                box.ValueMember = "Id";
            }
        }

        // Завантаження існуючих даних про поставку при редагуванні
        private void LoadSupplyData(int supplyId)
        {
            string query = $@"
                SELECT s.Id, wi.Article, wi.EquipmentTypeId, wi.ManufacturerId, wi.StorageLocationId,
                       wi.Power, wi.Voltage, wi.Unit, s.Quantity, s.SupplyTypeId, s.SupplyDate, s.SupplierName
                FROM Supplies s
                INNER JOIN WarehouseItems wi ON s.WarehouseItemId = wi.Id
                WHERE s.Id = {supplyId}";

            var result = _dbService.ExecuteQuery(query);

            if (result != null && result.Rows.Count > 0)
            {
                DataRow row = result.Rows[0];

                if (Controls["articleBox"] is TextBox articleBox)
                    articleBox.Text = row["Article"].ToString() ?? "";

                if (Controls["equipmentBox"] is ComboBox equipmentBox && row["EquipmentTypeId"] != DBNull.Value)
                {
                    int equipId = (int)row["EquipmentTypeId"];
                    foreach (ComboBoxItem item in equipmentBox.Items)
                    {
                        if (item.Id == equipId)
                        {
                            equipmentBox.SelectedItem = item;
                            break;
                        }
                    }
                }

                if (Controls["manufacturerBox"] is ComboBox manufacturerBox && row["ManufacturerId"] != DBNull.Value)
                {
                    int mfrId = (int)row["ManufacturerId"];
                    foreach (ComboBoxItem item in manufacturerBox.Items)
                    {
                        if (item.Id == mfrId)
                        {
                            manufacturerBox.SelectedItem = item;
                            break;
                        }
                    }
                }

                if (Controls["quantityBox"] is NumericUpDown quantityBox && row["Quantity"] != DBNull.Value)
                    quantityBox.Value = Math.Abs((int)row["Quantity"]);

                if (Controls["unitBox"] is TextBox unitBox)
                    unitBox.Text = row["Unit"].ToString() ?? "шт.";

                if (Controls["powerBox"] is TextBox powerBox && row["Power"] != DBNull.Value)
                    powerBox.Text = ((decimal)row["Power"]).ToString("0.00", CultureInfo.InvariantCulture);

                if (Controls["voltageBox"] is TextBox voltageBox && row["Voltage"] != DBNull.Value)
                    voltageBox.Text = ((decimal)row["Voltage"]).ToString("0.00", CultureInfo.InvariantCulture);

                if (Controls["locationBox"] is ComboBox locationBox && row["StorageLocationId"] != DBNull.Value)
                {
                    int locId = (int)row["StorageLocationId"];
                    foreach (ComboBoxItem item in locationBox.Items)
                    {
                        if (item.Id == locId)
                        {
                            locationBox.SelectedItem = item;
                            break;
                        }
                    }
                }

                if (Controls["supplyTypeBox"] is ComboBox supplyTypeBox && row["SupplyTypeId"] != DBNull.Value)
                {
                    int supplyTypeId = (int)row["SupplyTypeId"];
                    foreach (ComboBoxItem item in supplyTypeBox.Items)
                    {
                        if (item.Id == supplyTypeId)
                        {
                            supplyTypeBox.SelectedItem = item;
                            break;
                        }
                    }
                }

                if (Controls["supplyDatePicker"] is DateTimePicker supplyDatePicker && row["SupplyDate"] != DBNull.Value)
                    supplyDatePicker.Value = DateTime.Parse(row["SupplyDate"].ToString() ?? DateTime.Now.ToString(CultureInfo.InvariantCulture));

                if (Controls["supplierBox"] is TextBox supplierBox)
                    supplierBox.Text = row["SupplierName"].ToString() ?? "";
            }
        }

        // Збереження інформації про поставку в базі даних
        private void SaveSupply()
        {
            string article = (Controls["articleBox"] as TextBox)?.Text ?? "";

            int equipmentId = (Controls["equipmentBox"] as ComboBox)?.SelectedItem is ComboBoxItem eq ? eq.Id : 0;
            int manufacturerId = (Controls["manufacturerBox"] as ComboBox)?.SelectedItem is ComboBoxItem mfg ? mfg.Id : 0;
            int quantity = (int)((Controls["quantityBox"] as NumericUpDown)?.Value ?? 1);

            string unit = (Controls["unitBox"] as TextBox)?.Text ?? "шт.";

            decimal power = 0;
            if (!string.IsNullOrWhiteSpace((Controls["powerBox"] as TextBox)?.Text))
            {
                decimal.TryParse((Controls["powerBox"] as TextBox)!.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out power);
            }

            decimal voltage = 0;
            if (!string.IsNullOrWhiteSpace((Controls["voltageBox"] as TextBox)?.Text))
            {
                decimal.TryParse((Controls["voltageBox"] as TextBox)!.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out voltage);
            }

            int locationId = (Controls["locationBox"] as ComboBox)?.SelectedItem is ComboBoxItem loc ? loc.Id : 0;
            int supplyTypeId = (Controls["supplyTypeBox"] as ComboBox)?.SelectedItem is ComboBoxItem st ? st.Id : 0;

            DateTime supplyDate = (Controls["supplyDatePicker"] as DateTimePicker)?.Value ?? DateTime.Now;

            string supplier = (Controls["supplierBox"] as TextBox)?.Text ?? "";

            // Перевірка обов'язкових полів
            if (string.IsNullOrWhiteSpace(article) || equipmentId == 0 || manufacturerId == 0 || locationId == 0 || supplyTypeId == 0)
            {
                MessageBox.Show("Заповніть усі обов'язкові поля!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Перевіряємо, чи існує товар з таким артикулом у WarehouseItems
                string checkQuery = "SELECT Id FROM WarehouseItems WHERE Article = @article";
                SqlParameter[] checkParams = { new SqlParameter("@article", article) };
                var checkResult = _dbService.ExecuteQueryWithParameters(checkQuery, checkParams);

                int warehouseItemId = 0;

                if (checkResult != null && checkResult.Rows.Count > 0)
                {
                    warehouseItemId = (int)checkResult.Rows[0]["Id"];
                }
                else
                {
                    // Якщо товару ще немає – створюємо новий запис у WarehouseItems з нульовою кількістю
                    string insertQuery = @"
                        INSERT INTO WarehouseItems (Article, EquipmentTypeId, ManufacturerId, StorageLocationId, Power, Voltage, Quantity, Unit, Notes)
                        VALUES (@article, @equipmentId, @manufacturerId, @locationId, @power, @voltage, 0, @unit, '')";

                    SqlParameter[] insertParams =
                    {
                        new SqlParameter("@article", article),
                        new SqlParameter("@equipmentId", equipmentId),
                        new SqlParameter("@manufacturerId", manufacturerId),
                        new SqlParameter("@locationId", locationId),
                        new SqlParameter("@power", power),
                        new SqlParameter("@voltage", voltage),
                        new SqlParameter("@unit", unit)
                    };

                    _dbService.ExecuteNonQueryWithParameters(insertQuery, insertParams);

                    // Повторна перевірка, щоб отримати Id щойно створеного товару
                    SqlParameter[] recheck = { new SqlParameter("@article", article) };
                    var newResult = _dbService.ExecuteQueryWithParameters(checkQuery, recheck);
                    if (newResult != null && newResult.Rows.Count > 0)
                    {
                        warehouseItemId = (int)newResult.Rows[0]["Id"];
                    }
                }

                int supplyQuantity = quantity;

                // Отримуємо тип поставки, щоб визначити знак кількості (INBOUND/OUTBOUND)
                string typeQuery = "SELECT Name FROM SupplyTypes WHERE Id = @id";
                SqlParameter[] typeParams = { new SqlParameter("@id", supplyTypeId) };
                var typeResult = _dbService.ExecuteQueryWithParameters(typeQuery, typeParams);

                if (typeResult != null && typeResult.Rows.Count > 0)
                {
                    string typeName = typeResult.Rows[0]["Name"].ToString() ?? "INBOUND";
                    if (typeName != "INBOUND")
                    {
                        // Для вихідних операцій кількість записується зі знаком мінус
                        supplyQuantity = -quantity;
                    }
                }

                if (_isEdit && _supplyId.HasValue)
                {
                    // Оновлення існуючого запису про поставку
                    string updateQuery = @"
                        UPDATE Supplies
                        SET WarehouseItemId = @warehouseItemId,
                            SupplyTypeId = @supplyTypeId,
                            Quantity = @quantity,
                            SupplyDate = @supplyDate,
                            SupplierName = @supplierName,
                            UpdatedAt = GETDATE()
                        WHERE Id = @supplyId";

                    SqlParameter[] updateParams =
                    {
                        new SqlParameter("@warehouseItemId", warehouseItemId),
                        new SqlParameter("@supplyTypeId", supplyTypeId),
                        new SqlParameter("@quantity", supplyQuantity),
                        new SqlParameter("@supplyDate", supplyDate.Date),
                        new SqlParameter("@supplierName", supplier),
                        new SqlParameter("@supplyId", _supplyId.Value)
                    };

                    _dbService.ExecuteNonQueryWithParameters(updateQuery, updateParams);
                }
                else
                {
                    // Створення нового запису у таблиці Supplies
                    string insertSupplyQuery = @"
                        INSERT INTO Supplies (WarehouseItemId, SupplyTypeId, Quantity, SupplyDate, SupplierName, CreatedByUserLogin, UpdatedAt)
                        VALUES (@warehouseItemId, @supplyTypeId, @quantity, @supplyDate, @supplierName, @userLogin, GETDATE())";

                    SqlParameter[] supplyParams =
                    {
                        new SqlParameter("@warehouseItemId", warehouseItemId),
                        new SqlParameter("@supplyTypeId", supplyTypeId),
                        new SqlParameter("@quantity", supplyQuantity),
                        new SqlParameter("@supplyDate", supplyDate.Date),
                        new SqlParameter("@supplierName", supplier),
                        new SqlParameter("@userLogin", _userLogin)
                    };

                    _dbService.ExecuteNonQueryWithParameters(insertSupplyQuery, supplyParams);
                }

                // Перерахунок загальної кількості товару на складі на основі всіх поставок
                string updateQtyQuery = @"
                    UPDATE WarehouseItems
                    SET Quantity = COALESCE((SELECT SUM(Quantity) FROM Supplies WHERE WarehouseItemId = @warehouseItemId), 0)
                    WHERE Id = @warehouseItemId";

                SqlParameter[] qtyParams = { new SqlParameter("@warehouseItemId", warehouseItemId) };
                _dbService.ExecuteNonQueryWithParameters(updateQtyQuery, qtyParams);

                MessageBox.Show("Поставка збережена!", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // Допоміжний клас для збереження пари Id‑Назва в елементах ComboBox
    public class ComboBoxItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";

        public override string ToString() => Name;
    }
}