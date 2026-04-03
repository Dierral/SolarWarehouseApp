using System.Windows;
using MaterialDesignThemes.Wpf;
using SolarWarehouseApp.Data;
using SolarWarehouseApp.Helpers;
using SolarWarehouseApp.Views;

namespace SolarWarehouseApp
{
    public partial class App : Application
    {
        private static bool _isDarkTheme = true;
        public static bool IsDarkTheme => _isDarkTheme;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load DB connection and create service
            var config = ConfigHelper.LoadConfig();
            string connectionString = ConfigHelper.BuildConnectionString(config);
            var dbService = new DatabaseService(connectionString);

            // Check if any users exist
            string checkUsersQuery = "SELECT COUNT(*) as cnt FROM AppUsers";
            var result = dbService.ExecuteQuery(checkUsersQuery);
            int userCount = result != null && result.Rows.Count > 0
                ? int.Parse(result.Rows[0]["cnt"].ToString() ?? "0")
                : 0;

            if (userCount == 0)
            {
                var firstAdminView = new FirstAdminView(dbService);
                firstAdminView.Show();
            }
            else
            {
                var loginView = new LoginView(dbService);
                loginView.Show();
            }
        }

        public static void ToggleTheme()
        {
            _isDarkTheme = !_isDarkTheme;
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(_isDarkTheme ? BaseTheme.Dark : BaseTheme.Light);
            paletteHelper.SetTheme(theme);
        }
    }
}
