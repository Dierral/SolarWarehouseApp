using SolarWarehouseApp.Data;
using SolarWarehouseApp.Helpers;

namespace SolarWarehouseApp.Main
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // Завантаження налаштувань підключення та створення сервісу доступу до БД
            var config = ConfigHelper.LoadConfig();
            string connectionString = ConfigHelper.BuildConnectionString(config);
            var dbService = new DatabaseService(connectionString);

            // Перевірка, чи існують користувачі в системі
            string checkUsersQuery = "SELECT COUNT(*) as cnt FROM AppUsers";
            var result = dbService.ExecuteQuery(checkUsersQuery);
            int userCount = result != null && result.Rows.Count > 0
                ? int.Parse(result.Rows[0]["cnt"].ToString() ?? "0")
                : 0;

            if (userCount == 0)
            {
                // Якщо користувачів ще немає – відкриваємо форму створення першого адміністратора
                Application.Run(new FirstAdminForm(dbService));
            }
            else
            {
                // Інакше показуємо форму логіну, а після успішного входу – головну форму програми
                LoginForm loginForm = new LoginForm();
                if (loginForm.ShowDialog() == DialogResult.OK)
                {
                    Application.Run(new MainForm(dbService, loginForm.LoggedInUserLogin, loginForm.LoggedInUserRole));
                }
            }
        }
    }
}