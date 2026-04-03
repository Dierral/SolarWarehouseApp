using System.Data;
using System.Globalization;
using System.Windows;
using Microsoft.Data.SqlClient;
using SolarWarehouseApp.Data;

namespace SolarWarehouseApp.Views
{
    public partial class AddEditSupplyDialog : Window
    {
        private readonly DatabaseService _dbService;
        private readonly string? _userLogin;
        private readonly int? _supplyId;
        private readonly bool _isEdit;

        public AddEditSupplyDialog(DatabaseService dbService, string? userLogin, int? supplyId = null)
        {
            InitializeComponent();
            _dbService = dbService;
            _userLogin = userLogin;
            _supplyId = supplyId;
            _isEdit = supplyId.HasValue;

            Loaded += Dialog_Loaded;
        }

        private void Dialog_Loaded(object sender, RoutedEventArgs e)
        {
            DialogTitle.Text = _isEdit ? "Редагування поставки" : "Нова поставка";
            Title = _isEdit ? "Редагування поставки" : "Додавання нової поставки";

            SupplyDatePicker.SelectedDate = DateTime.Today;

            LoadComboBoxData();

            if (_isEdit && _supplyId.HasValue)
                LoadSupplyData(_supplyId.Value);
        }

        private void LoadComboBoxData()
        {
            // Equipment types
            var eqTypes = _dbService.ExecuteQuery("SELECT Id, Name FROM EquipmentTypes WHERE IsActive = 1 ORDER BY Name");
            if (eqTypes != null)
                foreach (DataRow row in eqTypes.Rows)
                    EquipmentTypeBox.Items.Add(new ComboItem { Id = (int)row["Id"], Name = row["Name"].ToString() ?? "" });

            // Manufacturers
            var mfrs = _dbService.ExecuteQuery("SELECT Id, Name FROM Manufacturers WHERE IsActive = 1 ORDER BY Name");
            if (mfrs != null)
                foreach (DataRow row in mfrs.Rows)
                    ManufacturerBox.Items.Add(new ComboItem { Id = (int)row["Id"], Name = row["Name"].ToString() ?? "" });

            // Storage locations
            var locs = _dbService.ExecuteQuery("SELECT Id, CONCAT(WarehouseName, '-', Rack, '-', Shelf) as Location FROM StorageLocations WHERE IsActive = 1 ORDER BY WarehouseName, Rack, Shelf");
            if (locs != null)
                foreach (DataRow row in locs.Rows)
                    StorageLocationBox.Items.Add(new ComboItem { Id = (int)row["Id"], Name = row["Location"].ToString() ?? "" });

            // Supply types
            var types = _dbService.ExecuteQuery("SELECT Id, Description FROM SupplyTypes WHERE IsActive = 1 ORDER BY Name");
            if (types != null)
                foreach (DataRow row in types.Rows)
                    SupplyTypeBox.Items.Add(new ComboItem { Id = (int)row["Id"], Name = row["Description"].ToString() ?? "" });
        }

        private void LoadSupplyData(int supplyId)
        {
            string query = $@"
                SELECT s.Id, wi.Article, wi.EquipmentTypeId, wi.ManufacturerId, wi.StorageLocationId,
                       wi.Power, wi.Voltage, wi.Unit, s.Quantity, s.SupplyTypeId, s.SupplyDate, s.SupplierName
                FROM Supplies s
                INNER JOIN WarehouseItems wi ON s.WarehouseItemId = wi.Id
                WHERE s.Id = {supplyId}";

            var result = _dbService.ExecuteQuery(query);
            if (result == null || result.Rows.Count == 0) return;

            var row = result.Rows[0];

            ArticleBox.Text = row["Article"].ToString() ?? "";

            if (row["EquipmentTypeId"] != DBNull.Value)
            {
                int eqId = (int)row["EquipmentTypeId"];
                EquipmentTypeBox.SelectedItem = EquipmentTypeBox.Items.OfType<ComboItem>().FirstOrDefault(x => x.Id == eqId);
            }

            if (row["ManufacturerId"] != DBNull.Value)
            {
                int mfrId = (int)row["ManufacturerId"];
                ManufacturerBox.SelectedItem = ManufacturerBox.Items.OfType<ComboItem>().FirstOrDefault(x => x.Id == mfrId);
            }

            QuantityBox.Text = Math.Abs((row["Quantity"] is int qi ? qi : (row["Quantity"] is decimal qd ? (int)qd : 0))).ToString();
            UnitBox.Text = row["Unit"].ToString() ?? "шт.";

            if (row["Power"] != DBNull.Value)
                PowerBox.Text = ((decimal)row["Power"]).ToString("0.##", CultureInfo.InvariantCulture);
            if (row["Voltage"] != DBNull.Value)
                VoltageBox.Text = ((decimal)row["Voltage"]).ToString("0.##", CultureInfo.InvariantCulture);

            if (row["StorageLocationId"] != DBNull.Value)
            {
                int locId = (int)row["StorageLocationId"];
                StorageLocationBox.SelectedItem = StorageLocationBox.Items.OfType<ComboItem>().FirstOrDefault(x => x.Id == locId);
            }

            if (row["SupplyTypeId"] != DBNull.Value)
            {
                int stId = (int)row["SupplyTypeId"];
                SupplyTypeBox.SelectedItem = SupplyTypeBox.Items.OfType<ComboItem>().FirstOrDefault(x => x.Id == stId);
            }

            if (row["SupplyDate"] != DBNull.Value && DateTime.TryParse(row["SupplyDate"].ToString(), out var sd))
                SupplyDatePicker.SelectedDate = sd;

            SupplierBox.Text = row["SupplierName"].ToString() ?? "";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string article = ArticleBox.Text.Trim();
            int equipmentId = (EquipmentTypeBox.SelectedItem as ComboItem)?.Id ?? 0;
            int manufacturerId = (ManufacturerBox.SelectedItem as ComboItem)?.Id ?? 0;
            int locationId = (StorageLocationBox.SelectedItem as ComboItem)?.Id ?? 0;
            int supplyTypeId = (SupplyTypeBox.SelectedItem as ComboItem)?.Id ?? 0;

            if (string.IsNullOrWhiteSpace(article) || equipmentId == 0 || manufacturerId == 0 ||
                locationId == 0 || supplyTypeId == 0)
            {
                MessageBox.Show("Заповніть усі обов'язкові поля (*)!", "Попередження",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(QuantityBox.Text, out int quantity) || quantity <= 0)
            {
                MessageBox.Show("Введіть коректну кількість!", "Попередження",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string unit = UnitBox.Text.Trim();
            if (string.IsNullOrEmpty(unit)) unit = "шт.";

            decimal.TryParse(PowerBox.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal power);
            decimal.TryParse(VoltageBox.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal voltage);

            DateTime supplyDate = SupplyDatePicker.SelectedDate ?? DateTime.Today;
            string supplier = SupplierBox.Text.Trim();

            try
            {
                // Check/create WarehouseItem
                string checkQuery = "SELECT Id FROM WarehouseItems WHERE Article = @article";
                SqlParameter[] checkParams = { new SqlParameter("@article", article) };
                var checkResult = _dbService.ExecuteQueryWithParameters(checkQuery, checkParams);

                int warehouseItemId = 0;
                if (checkResult != null && checkResult.Rows.Count > 0)
                {
                    warehouseItemId = (int)checkResult.Rows[0]["Id"];
                    // Update existing item info
                    string updateItem = @"UPDATE WarehouseItems SET EquipmentTypeId=@eq, ManufacturerId=@mfr, StorageLocationId=@loc, Power=@power, Voltage=@voltage, Unit=@unit WHERE Id=@id";
                    SqlParameter[] updParams =
                    {
                        new SqlParameter("@eq", equipmentId),
                        new SqlParameter("@mfr", manufacturerId),
                        new SqlParameter("@loc", locationId),
                        new SqlParameter("@power", power),
                        new SqlParameter("@voltage", voltage),
                        new SqlParameter("@unit", unit),
                        new SqlParameter("@id", warehouseItemId)
                    };
                    _dbService.ExecuteNonQueryWithParameters(updateItem, updParams);
                }
                else
                {
                    string insertItem = @"INSERT INTO WarehouseItems (Article, EquipmentTypeId, ManufacturerId, StorageLocationId, Power, Voltage, Quantity, Unit, Notes) VALUES (@article, @eq, @mfr, @loc, @power, @voltage, 0, @unit, '')";
                    SqlParameter[] insParams =
                    {
                        new SqlParameter("@article", article),
                        new SqlParameter("@eq", equipmentId),
                        new SqlParameter("@mfr", manufacturerId),
                        new SqlParameter("@loc", locationId),
                        new SqlParameter("@power", power),
                        new SqlParameter("@voltage", voltage),
                        new SqlParameter("@unit", unit)
                    };
                    _dbService.ExecuteNonQueryWithParameters(insertItem, insParams);

                    var newResult = _dbService.ExecuteQueryWithParameters(checkQuery, new[] { new SqlParameter("@article", article) });
                    if (newResult != null && newResult.Rows.Count > 0)
                        warehouseItemId = (int)newResult.Rows[0]["Id"];
                }

                // Determine sign from supply type
                string typeQuery = "SELECT Name FROM SupplyTypes WHERE Id = @id";
                SqlParameter[] typeParams = { new SqlParameter("@id", supplyTypeId) };
                var typeResult = _dbService.ExecuteQueryWithParameters(typeQuery, typeParams);
                int supplyQuantity = quantity;
                if (typeResult != null && typeResult.Rows.Count > 0)
                {
                    string typeName = typeResult.Rows[0]["Name"].ToString() ?? "INBOUND";
                    if (typeName != "INBOUND") supplyQuantity = -quantity;
                }

                if (_isEdit && _supplyId.HasValue)
                {
                    string updateQuery = @"UPDATE Supplies SET WarehouseItemId=@wid, SupplyTypeId=@st, Quantity=@qty, SupplyDate=@date, SupplierName=@supplier, UpdatedAt=GETDATE() WHERE Id=@sid";
                    SqlParameter[] updParams =
                    {
                        new SqlParameter("@wid", warehouseItemId),
                        new SqlParameter("@st", supplyTypeId),
                        new SqlParameter("@qty", supplyQuantity),
                        new SqlParameter("@date", supplyDate.Date),
                        new SqlParameter("@supplier", supplier),
                        new SqlParameter("@sid", _supplyId.Value)
                    };
                    _dbService.ExecuteNonQueryWithParameters(updateQuery, updParams);
                }
                else
                {
                    string insertQuery = @"INSERT INTO Supplies (WarehouseItemId, SupplyTypeId, Quantity, SupplyDate, SupplierName, CreatedByUserLogin, UpdatedAt) VALUES (@wid, @st, @qty, @date, @supplier, @login, GETDATE())";
                    SqlParameter[] insParams =
                    {
                        new SqlParameter("@wid", warehouseItemId),
                        new SqlParameter("@st", supplyTypeId),
                        new SqlParameter("@qty", supplyQuantity),
                        new SqlParameter("@date", supplyDate.Date),
                        new SqlParameter("@supplier", supplier),
                        new SqlParameter("@login", _userLogin ?? "")
                    };
                    _dbService.ExecuteNonQueryWithParameters(insertQuery, insParams);
                }

                // Recalculate warehouse quantity
                string updateQty = @"UPDATE WarehouseItems SET Quantity = COALESCE((SELECT SUM(Quantity) FROM Supplies WHERE WarehouseItemId = @wid), 0) WHERE Id = @wid";
                _dbService.ExecuteNonQueryWithParameters(updateQty, new[] { new SqlParameter("@wid", warehouseItemId) });

                MessageBox.Show("Поставку збережено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class ComboItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public override string ToString() => Name;
    }
}
