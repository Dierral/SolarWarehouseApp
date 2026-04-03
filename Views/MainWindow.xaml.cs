using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SolarWarehouseApp.Data;
using SolarWarehouseApp.Services;
using MaterialDesignThemes.Wpf;

namespace SolarWarehouseApp.Views
{
    public partial class MainWindow : Window
    {
        private readonly DatabaseService _dbService;
        private readonly string? _userLogin;
        private readonly string? _userRole;
        private LogService _logService = null!;

        private bool _sidebarExpanded = false;
        private const double CollapsedWidth = 64;
        private const double ExpandedWidth = 220;

        private Button? _activeButton;

        public MainWindow(DatabaseService dbService, string? userLogin, string? userRole)
        {
            InitializeComponent();
            _dbService = dbService;
            _userLogin = userLogin;
            _userRole = userRole;
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            InitializeWindow();
            Navigate_Dashboard(this, new RoutedEventArgs());
        }

        private void InitializeWindow()
        {
            // Set user info in top bar
            UserLoginLabel.Text = _userLogin ?? "Unknown";
            UserRoleLabel.Text = _userRole ?? "Operator";

            // Show admin-only menu items
            if (_userRole == "Admin")
            {
                AdminDivider.Visibility = Visibility.Visible;
                BtnUsers.Visibility = Visibility.Visible;
                BtnSql.Visibility = Visibility.Visible;
                BtnDbStructure.Visibility = Visibility.Visible;
                BtnBackup.Visibility = Visibility.Visible;
                BtnLogs.Visibility = Visibility.Visible;
            }

            // Initialize logging
            _logService = new LogService(_dbService);
            if (int.TryParse(_userLogin, out int userId))
            {
                _logService.StartSession(userId, _userLogin ?? "Unknown");
            }
            else
            {
                // Try to get user ID from DB
                var result = _dbService.ExecuteQuery($"SELECT Id FROM AppUsers WHERE Login = '{_userLogin}'");
                if (result != null && result.Rows.Count > 0)
                {
                    int id = (int)result.Rows[0]["Id"];
                    _logService.StartSession(id, _userLogin ?? "Unknown");
                }
            }

            _activeButton = BtnDashboard;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _logService?.EndSession();
            base.OnClosing(e);
        }

        #region Sidebar Animation

        private void Sidebar_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_sidebarExpanded) return;
            ExpandSidebar();
        }

        private void Sidebar_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_sidebarExpanded) return;
            CollapseSidebar();
        }

        private void ExpandSidebar()
        {
            _sidebarExpanded = true;

            // Animate sidebar width
            var widthAnim = new DoubleAnimation(ExpandedWidth, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Sidebar.BeginAnimation(WidthProperty, widthAnim);

            // Show text labels
            ShowSidebarLabels(true);

            // Dim the content
            var dimAnim = new DoubleAnimation(0.45, TimeSpan.FromMilliseconds(220));
            DimOverlay.BeginAnimation(OpacityProperty, dimAnim);
            DimOverlay.IsHitTestVisible = true;
        }

        private void CollapseSidebar()
        {
            _sidebarExpanded = false;

            // Hide text labels first
            ShowSidebarLabels(false);

            // Animate sidebar width
            var widthAnim = new DoubleAnimation(CollapsedWidth, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Sidebar.BeginAnimation(WidthProperty, widthAnim);

            // Remove dim
            var dimAnim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
            DimOverlay.BeginAnimation(OpacityProperty, dimAnim);
            DimOverlay.IsHitTestVisible = false;
        }

        private void ShowSidebarLabels(bool show)
        {
            var textBlocks = new[]
            {
                TxtDashboard, TxtSupplies, TxtWarehouse, TxtEquipment, TxtManufacturers,
                TxtStorage, TxtUsers, TxtSql, TxtDbStructure, TxtBackup, TxtLogs,
                TxtTheme, TxtLogout, LogoText
            };

            var otherElements = new FrameworkElement[] { AdminLabel };

            if (show)
            {
                foreach (var tb in textBlocks)
                {
                    tb.Visibility = Visibility.Visible;
                    var anim = new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { BeginTime = TimeSpan.FromMilliseconds(80) };
                    tb.BeginAnimation(OpacityProperty, anim);
                }
                AdminLabel.Visibility = Visibility.Visible;
                var adminAnim = new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { BeginTime = TimeSpan.FromMilliseconds(80) };
                AdminLabel.BeginAnimation(OpacityProperty, adminAnim);
            }
            else
            {
                foreach (var tb in textBlocks)
                {
                    var anim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(100));
                    anim.Completed += (s, e) => tb.Visibility = Visibility.Collapsed;
                    tb.BeginAnimation(OpacityProperty, anim);
                }
                var adminAnim2 = new DoubleAnimation(0, TimeSpan.FromMilliseconds(100));
                adminAnim2.Completed += (s, e) => AdminLabel.Visibility = Visibility.Collapsed;
                AdminLabel.BeginAnimation(OpacityProperty, adminAnim2);
            }
        }

        #endregion

        #region Navigation

        private void SetActiveSidebarButton(Button btn)
        {
            if (_activeButton != null)
                _activeButton.Style = (Style)Resources["SidebarButtonStyle"];

            btn.Style = (Style)Resources["SidebarButtonActiveStyle"];
            _activeButton = btn;
        }

        private void Navigate_Dashboard(object sender, RoutedEventArgs e)
        {
            SetActiveSidebarButton(BtnDashboard);
            PageTitle.Text = "Дашборд";
            ContentFrame.Navigate(new DashboardView(_dbService, _userLogin));
            _logService?.LogEvent("NAV", null, null, "Dashboard");
        }

        private void Navigate_Supplies(object sender, RoutedEventArgs e)
        {
            SetActiveSidebarButton(BtnSupplies);
            PageTitle.Text = "Поставки";
            ContentFrame.Navigate(new SuppliesView(_dbService, _userLogin, _logService));
            _logService?.LogEvent("NAV", null, null, "Supplies");
        }

        private void Navigate_Warehouse(object sender, RoutedEventArgs e)
        {
            SetActiveSidebarButton(BtnWarehouse);
            PageTitle.Text = "Товари на складі";
            ContentFrame.Navigate(new WarehouseItemsView(_dbService, _logService));
            _logService?.LogEvent("NAV", null, null, "WarehouseItems");
        }

        private void Navigate_Equipment(object sender, RoutedEventArgs e)
        {
            SetActiveSidebarButton(BtnEquipment);
            PageTitle.Text = "Типи обладнання";
            ContentFrame.Navigate(new EquipmentTypesView(_dbService, _logService));
            _logService?.LogEvent("NAV", null, null, "EquipmentTypes");
        }

        private void Navigate_Manufacturers(object sender, RoutedEventArgs e)
        {
            SetActiveSidebarButton(BtnManufacturers);
            PageTitle.Text = "Виробники";
            ContentFrame.Navigate(new ManufacturersView(_dbService, _logService));
            _logService?.LogEvent("NAV", null, null, "Manufacturers");
        }

        private void Navigate_Storage(object sender, RoutedEventArgs e)
        {
            SetActiveSidebarButton(BtnStorage);
            PageTitle.Text = "Місця зберігання";
            ContentFrame.Navigate(new StorageLocationsView(_dbService, _logService));
            _logService?.LogEvent("NAV", null, null, "StorageLocations");
        }

        private void Navigate_Users(object sender, RoutedEventArgs e)
        {
            if (_userRole != "Admin") return;
            SetActiveSidebarButton(BtnUsers);
            PageTitle.Text = "Користувачі";
            ContentFrame.Navigate(new UsersView(_dbService, _logService));
            _logService?.LogEvent("NAV", null, null, "Users");
        }

        private void Navigate_Sql(object sender, RoutedEventArgs e)
        {
            if (_userRole != "Admin") return;
            SetActiveSidebarButton(BtnSql);
            PageTitle.Text = "SQL-консоль";
            ContentFrame.Navigate(new SqlConsoleView(_dbService, _logService));
            _logService?.LogEvent("NAV", null, null, "SqlConsole");
        }

        private void Navigate_DbStructure(object sender, RoutedEventArgs e)
        {
            if (_userRole != "Admin") return;
            SetActiveSidebarButton(BtnDbStructure);
            PageTitle.Text = "Структура БД";
            ContentFrame.Navigate(new DbStructureView(_dbService, _logService));
            _logService?.LogEvent("NAV", null, null, "DbStructure");
        }

        private void Navigate_Backup(object sender, RoutedEventArgs e)
        {
            if (_userRole != "Admin") return;
            SetActiveSidebarButton(BtnBackup);
            PageTitle.Text = "Backup / Restore";
            ContentFrame.Navigate(new BackupRestoreView(_dbService, _logService));
            _logService?.LogEvent("NAV", null, null, "BackupRestore");
        }

        private void Navigate_Logs(object sender, RoutedEventArgs e)
        {
            if (_userRole != "Admin") return;
            SetActiveSidebarButton(BtnLogs);
            PageTitle.Text = "Логи";
            ContentFrame.Navigate(new LogsView(_dbService, _logService));
            _logService?.LogEvent("NAV", null, null, "Logs");
        }

        private void BtnTheme_Click(object sender, RoutedEventArgs e)
        {
            App.ToggleTheme();
            ThemeIcon.Kind = App.IsDarkTheme ? PackIconKind.WeatherNight : PackIconKind.WeatherSunny;
            TxtTheme.Text = App.IsDarkTheme ? "Темна тема" : "Світла тема";
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вийти з системи?", "Підтвердження",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _logService?.EndSession();
                var loginView = new LoginView(_dbService);
                loginView.Show();
                Close();
            }
        }

        #endregion
    }
}
