using SolarWarehouseApp.Data;
using SolarWarehouseApp.Helpers;
using SolarWarehouseApp.Views;
using System.Windows;

namespace SolarWarehouseApp.Main
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
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

            var app = new App();

            if (userCount == 0)
            {
                // Якщо користувачів ще немає – відкриваємо форму створення першого адміністратора
                var firstAdminView = new FirstAdminView(dbService);
                app.Run(firstAdminView);
            }
            else
            {
                // Показуємо форму логіну
                var loginView = new LoginView(dbService);
                app.Run(loginView);
            }
        }
    }
}
