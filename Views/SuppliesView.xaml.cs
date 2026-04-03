using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SolarWarehouseApp.Data;
using SolarWarehouseApp.Services;

namespace SolarWarehouseApp.Views
{
    public partial class SuppliesView : Page
    {
        private readonly DatabaseService _dbService;
        private readonly string? _userLogin;
        private readonly LogService? _logService;
        private bool _isLoading = false;

        public SuppliesView(DatabaseService dbService, string? userLogin, LogService? logService)
        {
            InitializeComponent();
            _dbService = dbService;
            _userLogin = userLogin;
            _logService = logService;
            Loaded += SuppliesView_Loaded;
        }

        private void SuppliesView_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            LoadFilterData();
            _isLoading = false;
            LoadSupplies();
        }

        private void LoadFilterData()
        {
            // Supply types filter
            TypeFilter.Items.Add(new FilterItem { Id = 0, Name = "(Всі типи)" });
            var types = _dbService.ExecuteQuery("SELECT Id, Description FROM SupplyTypes WHERE IsActive = 1 ORDER BY Name");
            if (types != null)
                foreach (DataRow row in types.Rows)
                    TypeFilter.Items.Add(new FilterItem { Id = (int)row["Id"], Name = row["Description"].ToString() ?? "" });
            TypeFilter.SelectedIndex = 0;

            // Equipment types filter
            EquipmentTypeFilter.Items.Add(new FilterItem { Id = 0, Name = "(Всі типи)" });
            var eqTypes = _dbService.ExecuteQuery("SELECT Id, Name FROM EquipmentTypes WHERE IsActive = 1 ORDER BY Name");
            if (eqTypes != null)
                foreach (DataRow row in eqTypes.Rows)
                    EquipmentTypeFilter.Items.Add(new FilterItem { Id = (int)row["Id"], Name = row["Name"].ToString() ?? "" });
            EquipmentTypeFilter.SelectedIndex = 0;

            // Manufacturers filter
            ManufacturerFilter.Items.Add(new FilterItem { Id = 0, Name = "(Всі виробники)" });
            var mfrs = _dbService.ExecuteQuery("SELECT Id, Name FROM Manufacturers WHERE IsActive = 1 ORDER BY Name");
            if (mfrs != null)
                foreach (DataRow row in mfrs.Rows)
                    ManufacturerFilter.Items.Add(new FilterItem { Id = (int)row["Id"], Name = row["Name"].ToString() ?? "" });
            ManufacturerFilter.SelectedIndex = 0;

            // Countries filter
            CountryFilter.Items.Add(new FilterItem { Id = 0, Name = "(Всі країни)" });
            var countries = _dbService.ExecuteQuery("SELECT DISTINCT Country FROM Manufacturers WHERE Country IS NOT NULL AND Country != '' ORDER BY Country");
            if (countries != null)
                foreach (DataRow row in countries.Rows)
                    CountryFilter.Items.Add(new FilterItem { Id = 0, Name = row["Country"].ToString() ?? "" });
            CountryFilter.SelectedIndex = 0;

            // Storage locations filter
            LocationFilter.Items.Add(new FilterItem { Id = 0, Name = "(Всі місця)" });
            var locs = _dbService.ExecuteQuery("SELECT Id, CONCAT(WarehouseName, '-', Rack, '-', Shelf) as Loc FROM StorageLocations WHERE IsActive = 1 ORDER BY WarehouseName");
            if (locs != null)
                foreach (DataRow row in locs.Rows)
                    LocationFilter.Items.Add(new FilterItem { Id = (int)row["Id"], Name = row["Loc"].ToString() ?? "" });
            LocationFilter.SelectedIndex = 0;

            // Added by filter
            AddedByFilter.Items.Add(new FilterItem { Id = 0, Name = "(Всі)" });
            var users = _dbService.ExecuteQuery("SELECT DISTINCT CreatedByUserLogin FROM Supplies WHERE CreatedByUserLogin IS NOT NULL ORDER BY CreatedByUserLogin");
            if (users != null)
                foreach (DataRow row in users.Rows)
                    AddedByFilter.Items.Add(new FilterItem { Id = 0, Name = row["CreatedByUserLogin"].ToString() ?? "" });
            AddedByFilter.SelectedIndex = 0;
        }

        private void LoadSupplies()
        {
            string query = BuildQuery();
            var result = _dbService.ExecuteQuery(query);
            SuppliesGrid.ItemsSource = result?.DefaultView;

            // Tag rows with IsOutbound for styling
            if (result != null && !result.Columns.Contains("IsOutbound"))
            {
                result.Columns.Add("IsOutbound", typeof(bool));
                foreach (DataRow row in result.Rows)
                    row["IsOutbound"] = (row["Quantity"] is int q && q < 0) ||
                                        (row["Quantity"] is decimal d && d < 0);
            }
        }

        private string BuildQuery()
        {
            var conditions = new List<string>();

            string article = ArticleFilter.Text.Trim();
            if (!string.IsNullOrEmpty(article))
                conditions.Add($"wi.Article LIKE '%{article}%'");

            if (TypeFilter.SelectedItem is FilterItem type && type.Id > 0)
                conditions.Add($"s.SupplyTypeId = {type.Id}");

            if (EquipmentTypeFilter.SelectedItem is FilterItem eq && eq.Id > 0)
                conditions.Add($"wi.EquipmentTypeId = {eq.Id}");

            if (ManufacturerFilter.SelectedItem is FilterItem mfr && mfr.Id > 0)
                conditions.Add($"wi.ManufacturerId = {mfr.Id}");

            if (CountryFilter.SelectedItem is FilterItem country && country.Id == 0 && country.Name != "(Всі країни)")
                conditions.Add($"m.Country = '{country.Name}'");

            if (LocationFilter.SelectedItem is FilterItem loc && loc.Id > 0)
                conditions.Add($"wi.StorageLocationId = {loc.Id}");

            if (AddedByFilter.SelectedItem is FilterItem addedBy && addedBy.Id == 0 && addedBy.Name != "(Всі)")
                conditions.Add($"s.CreatedByUserLogin = '{addedBy.Name}'");

            if (DateFrom.SelectedDate.HasValue)
                conditions.Add($"s.SupplyDate >= '{DateFrom.SelectedDate.Value:yyyy-MM-dd}'");

            if (DateTo.SelectedDate.HasValue)
                conditions.Add($"s.SupplyDate <= '{DateTo.SelectedDate.Value:yyyy-MM-dd}'");

            string where = conditions.Count > 0 ? "AND " + string.Join(" AND ", conditions) : "";

            return $@"
                SELECT 
                    s.Id,
                    s.SupplyDate,
                    wi.Article,
                    et.Name as EquipmentType,
                    m.Name as Manufacturer,
                    m.Country,
                    s.Quantity,
                    wi.Unit,
                    st.Description as SupplyType,
                    s.SupplierName,
                    CONCAT(sl.WarehouseName, '-', sl.Rack, '-', sl.Shelf) as StorageLocation,
                    s.CreatedByUserLogin as CreatedBy
                FROM Supplies s
                INNER JOIN WarehouseItems wi ON s.WarehouseItemId = wi.Id
                LEFT JOIN EquipmentTypes et ON wi.EquipmentTypeId = et.Id
                LEFT JOIN Manufacturers m ON wi.ManufacturerId = m.Id
                LEFT JOIN StorageLocations sl ON wi.StorageLocationId = sl.Id
                LEFT JOIN SupplyTypes st ON s.SupplyTypeId = st.Id
                WHERE 1=1 {where}
                ORDER BY s.SupplyDate DESC, s.Id DESC";
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (!_isLoading) LoadSupplies();
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            ArticleFilter.Clear();
            TypeFilter.SelectedIndex = 0;
            EquipmentTypeFilter.SelectedIndex = 0;
            ManufacturerFilter.SelectedIndex = 0;
            CountryFilter.SelectedIndex = 0;
            LocationFilter.SelectedIndex = 0;
            AddedByFilter.SelectedIndex = 0;
            DateFrom.SelectedDate = null;
            DateTo.SelectedDate = null;
            _isLoading = false;
            LoadSupplies();
        }

        private void AddSupply_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddEditSupplyDialog(_dbService, _userLogin);
            if (dialog.ShowDialog() == true)
            {
                _logService?.LogEvent("CREATE", "Supplies", null, "New supply added");
                LoadSupplies();
            }
        }

        private void EditSupply_Click(object sender, RoutedEventArgs e)
        {
            EditSelectedSupply();
        }

        private void SuppliesGrid_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            EditSelectedSupply();
        }

        private void EditSelectedSupply()
        {
            if (SuppliesGrid.SelectedItem == null)
            {
                MessageBox.Show("Виберіть запис для редагування!", "Попередження",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var row = (DataRowView)SuppliesGrid.SelectedItem;
            int supplyId = (int)row["Id"];

            var dialog = new AddEditSupplyDialog(_dbService, _userLogin, supplyId);
            if (dialog.ShowDialog() == true)
            {
                _logService?.LogEvent("UPDATE", "Supplies", supplyId, $"Supply ID={supplyId} updated");
                LoadSupplies();
            }
        }

        private void DeleteSupply_Click(object sender, RoutedEventArgs e)
        {
            if (SuppliesGrid.SelectedItem == null)
            {
                MessageBox.Show("Виберіть запис для видалення!", "Попередження",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var row = (DataRowView)SuppliesGrid.SelectedItem;
            int supplyId = (int)row["Id"];

            var result = MessageBox.Show("Видалити цю поставку?", "Підтвердження",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                bool ok = _dbService.ExecuteNonQuery($"DELETE FROM Supplies WHERE Id = {supplyId}");
                if (ok)
                {
                    _logService?.LogEvent("DELETE", "Supplies", supplyId, $"Supply ID={supplyId} deleted");
                    MessageBox.Show("Поставку видалено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadSupplies();
                }
            }
        }
    }

    public class FilterItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public override string ToString() => Name;
    }
}
