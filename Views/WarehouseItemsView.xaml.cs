using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using SolarWarehouseApp.Data;
using SolarWarehouseApp.Services;

namespace SolarWarehouseApp.Views
{
    public partial class WarehouseItemsView : Page
    {
        private readonly DatabaseService _dbService;
        private readonly LogService? _logService;
        private bool _isLoading = false;

        public WarehouseItemsView(DatabaseService dbService, LogService? logService)
        {
            InitializeComponent();
            _dbService = dbService;
            _logService = logService;
            Loaded += WarehouseItemsView_Loaded;
        }

        private void WarehouseItemsView_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            LoadFilterData();
            _isLoading = false;
            LoadItems();
        }

        private void LoadFilterData()
        {
            EquipmentFilter.Items.Add(new FilterItem { Id = 0, Name = "(Всі типи)" });
            var eqTypes = _dbService.ExecuteQuery("SELECT Id, Name FROM EquipmentTypes WHERE IsActive = 1 ORDER BY Name");
            if (eqTypes != null)
                foreach (DataRow row in eqTypes.Rows)
                    EquipmentFilter.Items.Add(new FilterItem { Id = (int)row["Id"], Name = row["Name"].ToString() ?? "" });
            EquipmentFilter.SelectedIndex = 0;

            ManufacturerFilter.Items.Add(new FilterItem { Id = 0, Name = "(Всі виробники)" });
            var mfrs = _dbService.ExecuteQuery("SELECT Id, Name FROM Manufacturers WHERE IsActive = 1 ORDER BY Name");
            if (mfrs != null)
                foreach (DataRow row in mfrs.Rows)
                    ManufacturerFilter.Items.Add(new FilterItem { Id = (int)row["Id"], Name = row["Name"].ToString() ?? "" });
            ManufacturerFilter.SelectedIndex = 0;

            CountryFilter.Items.Add(new FilterItem { Id = 0, Name = "(Всі країни)" });
            var countries = _dbService.ExecuteQuery("SELECT DISTINCT Country FROM Manufacturers WHERE Country IS NOT NULL AND Country != '' ORDER BY Country");
            if (countries != null)
                foreach (DataRow row in countries.Rows)
                    CountryFilter.Items.Add(new FilterItem { Id = 0, Name = row["Country"].ToString() ?? "" });
            CountryFilter.SelectedIndex = 0;

            WarehouseFilter.Items.Add(new FilterItem { Id = 0, Name = "(Всі склади)" });
            var whs = _dbService.ExecuteQuery("SELECT DISTINCT WarehouseName FROM StorageLocations WHERE IsActive = 1 ORDER BY WarehouseName");
            if (whs != null)
                foreach (DataRow row in whs.Rows)
                    WarehouseFilter.Items.Add(new FilterItem { Id = 0, Name = row["WarehouseName"].ToString() ?? "" });
            WarehouseFilter.SelectedIndex = 0;

            RackFilter.Items.Add(new FilterItem { Id = 0, Name = "(Всі стелажі)" });
            var racks = _dbService.ExecuteQuery("SELECT DISTINCT Rack FROM StorageLocations WHERE IsActive = 1 ORDER BY Rack");
            if (racks != null)
                foreach (DataRow row in racks.Rows)
                    RackFilter.Items.Add(new FilterItem { Id = 0, Name = row["Rack"].ToString() ?? "" });
            RackFilter.SelectedIndex = 0;

            ShelfFilter.Items.Add(new FilterItem { Id = 0, Name = "(Всі полиці)" });
            var shelves = _dbService.ExecuteQuery("SELECT DISTINCT Shelf FROM StorageLocations WHERE IsActive = 1 ORDER BY Shelf");
            if (shelves != null)
                foreach (DataRow row in shelves.Rows)
                    ShelfFilter.Items.Add(new FilterItem { Id = 0, Name = row["Shelf"].ToString() ?? "" });
            ShelfFilter.SelectedIndex = 0;
        }

        private void LoadItems()
        {
            var conditions = new List<string>();
            var parameters = new List<SqlParameter>();

            string article = ArticleFilter.Text.Trim();
            if (!string.IsNullOrEmpty(article))
            {
                conditions.Add("wi.Article LIKE @article");
                parameters.Add(new SqlParameter("@article", "%" + article + "%"));
            }

            if (EquipmentFilter.SelectedItem is FilterItem eq && eq.Id > 0)
            {
                conditions.Add("wi.EquipmentTypeId = @eqId");
                parameters.Add(new SqlParameter("@eqId", eq.Id));
            }

            if (ManufacturerFilter.SelectedItem is FilterItem mfr && mfr.Id > 0)
            {
                conditions.Add("wi.ManufacturerId = @mfrId");
                parameters.Add(new SqlParameter("@mfrId", mfr.Id));
            }

            if (CountryFilter.SelectedItem is FilterItem country && country.Name != "(Всі країни)")
            {
                conditions.Add("m.Country = @country");
                parameters.Add(new SqlParameter("@country", country.Name));
            }

            if (WarehouseFilter.SelectedItem is FilterItem wh && wh.Name != "(Всі склади)")
            {
                conditions.Add("sl.WarehouseName = @wh");
                parameters.Add(new SqlParameter("@wh", wh.Name));
            }

            if (RackFilter.SelectedItem is FilterItem rack && rack.Name != "(Всі стелажі)")
            {
                conditions.Add("sl.Rack = @rack");
                parameters.Add(new SqlParameter("@rack", rack.Name));
            }

            if (ShelfFilter.SelectedItem is FilterItem shelf && shelf.Name != "(Всі полиці)")
            {
                conditions.Add("sl.Shelf = @shelf");
                parameters.Add(new SqlParameter("@shelf", shelf.Name));
            }

            string where = conditions.Count > 0 ? "AND " + string.Join(" AND ", conditions) : "";

            string query = $@"
                SELECT wi.Id, wi.Article,
                       et.Name as EquipmentType,
                       m.Name as Manufacturer,
                       m.Country,
                       wi.Power, wi.Voltage, wi.Quantity, wi.Unit,
                       sl.WarehouseName, sl.Rack, sl.Shelf
                FROM WarehouseItems wi
                LEFT JOIN EquipmentTypes et ON wi.EquipmentTypeId = et.Id
                LEFT JOIN Manufacturers m ON wi.ManufacturerId = m.Id
                LEFT JOIN StorageLocations sl ON wi.StorageLocationId = sl.Id
                WHERE 1=1 {where}
                ORDER BY wi.Article";

            var result = _dbService.ExecuteQueryWithParameters(query, parameters.ToArray());
            ItemsGrid.ItemsSource = result?.DefaultView;
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (!_isLoading) LoadItems();
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            ArticleFilter.Clear();
            EquipmentFilter.SelectedIndex = 0;
            ManufacturerFilter.SelectedIndex = 0;
            CountryFilter.SelectedIndex = 0;
            WarehouseFilter.SelectedIndex = 0;
            RackFilter.SelectedIndex = 0;
            ShelfFilter.SelectedIndex = 0;
            _isLoading = false;
            LoadItems();
        }
    }
}
