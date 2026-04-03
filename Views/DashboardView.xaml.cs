using System.Windows.Controls;
using System.Windows.Threading;
using SolarWarehouseApp.Data;

namespace SolarWarehouseApp.Views
{
    public partial class DashboardView : Page
    {
        private readonly DatabaseService _dbService;
        private readonly string? _userLogin;
        private DispatcherTimer? _clockTimer;

        public DashboardView(DatabaseService dbService, string? userLogin)
        {
            InitializeComponent();
            _dbService = dbService;
            _userLogin = userLogin;
            Loaded += DashboardView_Loaded;
            Unloaded += DashboardView_Unloaded;
        }

        private void DashboardView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            UpdateGreeting();
            LoadStats();
            LoadRecentSupplies();

            // Update clock every second
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, ev) => UpdateGreeting();
            _clockTimer.Start();
        }

        private void DashboardView_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _clockTimer?.Stop();
        }

        private void UpdateGreeting()
        {
            int hour = DateTime.Now.Hour;
            string greeting = hour switch
            {
                >= 6 and < 12  => "Доброго ранку ☀️",
                >= 12 and < 18 => "Добрий день 🌤️",
                >= 18 and < 24 => "Добрий вечір 🌅",
                _              => "Доброї ночі 🌙"
            };

            GreetingText.Text = $"{greeting}, {_userLogin}!";
            DateTimeText.Text = DateTime.Now.ToString("dddd, d MMMM yyyy  •  HH:mm:ss",
                new System.Globalization.CultureInfo("uk-UA"));
        }

        private void LoadStats()
        {
            try
            {
                // Total warehouse items count
                var itemsResult = _dbService.ExecuteQuery("SELECT COUNT(*) as cnt FROM WarehouseItems WHERE Quantity > 0");
                if (itemsResult != null && itemsResult.Rows.Count > 0)
                    TotalItemsCount.Text = itemsResult.Rows[0]["cnt"].ToString() ?? "0";

                // Supplies in last 30 days
                var suppliesResult = _dbService.ExecuteQuery(
                    "SELECT COUNT(*) as cnt FROM Supplies WHERE SupplyDate >= DATEADD(day, -30, GETDATE())");
                if (suppliesResult != null && suppliesResult.Rows.Count > 0)
                    SuppliesCount.Text = suppliesResult.Rows[0]["cnt"].ToString() ?? "0";

                // Equipment types count
                var eqResult = _dbService.ExecuteQuery("SELECT COUNT(*) as cnt FROM EquipmentTypes WHERE IsActive = 1");
                if (eqResult != null && eqResult.Rows.Count > 0)
                    EquipmentTypesCount.Text = eqResult.Rows[0]["cnt"].ToString() ?? "0";

                // Manufacturers count
                var mfrResult = _dbService.ExecuteQuery("SELECT COUNT(*) as cnt FROM Manufacturers WHERE IsActive = 1");
                if (mfrResult != null && mfrResult.Rows.Count > 0)
                    ManufacturersCount.Text = mfrResult.Rows[0]["cnt"].ToString() ?? "0";
            }
            catch { /* Stats are best-effort */ }
        }

        private void LoadRecentSupplies()
        {
            try
            {
                string query = @"
                    SELECT TOP 10
                        s.SupplyDate,
                        wi.Article,
                        et.Name as EquipmentType,
                        m.Name as Manufacturer,
                        s.Quantity,
                        st.Description as SupplyType,
                        s.CreatedByUserLogin as CreatedBy
                    FROM Supplies s
                    INNER JOIN WarehouseItems wi ON s.WarehouseItemId = wi.Id
                    LEFT JOIN EquipmentTypes et ON wi.EquipmentTypeId = et.Id
                    LEFT JOIN Manufacturers m ON wi.ManufacturerId = m.Id
                    LEFT JOIN SupplyTypes st ON s.SupplyTypeId = st.Id
                    ORDER BY s.SupplyDate DESC, s.Id DESC";

                var result = _dbService.ExecuteQuery(query);
                RecentSuppliesGrid.ItemsSource = result?.DefaultView;
            }
            catch { /* Best-effort */ }
        }
    }
}
