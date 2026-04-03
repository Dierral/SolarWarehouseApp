using System.Data;
using Microsoft.Data.SqlClient;

namespace SolarWarehouseApp.Data
{
    public class DatabaseService
    {
        // Рядок підключення до бази даних
        private readonly string _connectionString;

        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Виконання SQL‑запиту, що повертає результати у вигляді DataTable
        public DataTable? ExecuteQuery(string query)
        {
            try
            {
                using SqlConnection connection = new SqlConnection(_connectionString);
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable dataTable = new DataTable();
                adapter.Fill(dataTable);
                return dataTable;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при виконанні запиту:\n{ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        // Виконання параметризованого SQL‑запиту
        public DataTable? ExecuteQueryWithParameters(string query, SqlParameter[] parameters)
        {
            try
            {
                using SqlConnection connection = new SqlConnection(_connectionString);
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                adapter.SelectCommand.Parameters.AddRange(parameters);
                DataTable dataTable = new DataTable();
                adapter.Fill(dataTable);
                return dataTable;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при виконанні запиту:\n{ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        // Виконання SQL‑команди без повернення результатів (INSERT/UPDATE/DELETE)
        public bool ExecuteNonQuery(string query)
        {
            try
            {
                using SqlConnection connection = new SqlConnection(_connectionString);
                connection.Open();
                SqlCommand command = new SqlCommand(query, connection);
                command.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при виконанні команди:\n{ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // Параметризована команда без повернення результатів
        public bool ExecuteNonQueryWithParameters(string query, SqlParameter[] parameters)
        {
            try
            {
                using SqlConnection connection = new SqlConnection(_connectionString);
                connection.Open();
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddRange(parameters);
                command.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при виконанні команди:\n{ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // Отримання опису структури таблиць бази даних
        public DataTable? GetDatabaseStructure()
        {
            string query = @"
                SELECT 
                    TABLE_NAME as 'Таблиця',
                    COLUMN_NAME as 'Назва поля',
                    DATA_TYPE as 'Тип даних',
                    IS_NULLABLE as 'Nullable',
                    COLUMN_DEFAULT as 'Значення за замовчуванням',
                    ORDINAL_POSITION as 'Позиція'
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo'
                ORDER BY TABLE_NAME, ORDINAL_POSITION
            ";
            return ExecuteQuery(query);
        }

        // Отримання кількості рядків у кожній таблиці бази даних
        public DataTable? GetTableRowCounts()
        {
            string query = @"
                SELECT 
                    t.name as 'Таблиця',
                    SUM(p.rows) as 'Кількість рядків'
                FROM sys.tables t
                INNER JOIN sys.partitions p ON t.object_id = p.object_id
                WHERE p.index_id IN (0, 1) AND t.name NOT LIKE 'sysdiagrams%'
                GROUP BY t.name
                ORDER BY t.name
            ";
            return ExecuteQuery(query);
        }

        // Отримання інформації про первинні ключі
        public DataTable? GetPrimaryKeys()
        {
            string query = @"
                SELECT 
                    t.name as 'Таблиця',
                    c.name as 'Поле',
                    'PRIMARY KEY' as 'Тип'
                FROM sys.tables t
                INNER JOIN sys.indexes i ON t.object_id = i.object_id
                INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                INNER JOIN sys.columns c ON t.object_id = c.object_id AND ic.column_id = c.column_id
                WHERE i.is_primary_key = 1
                ORDER BY t.name, c.name
            ";
            return ExecuteQuery(query);
        }

        // Отримання опису зовнішніх ключів між таблицями
        public DataTable? GetForeignKeys()
        {
            string query = @"
                SELECT 
                    fk.name as 'Назва FK',
                    tp.name as 'Батьківська таблиця',
                    cp.name as 'Батьківське поле',
                    tr.name as 'Дочірня таблиця',
                    cr.name as 'Дочірнє поле'
                FROM sys.foreign_keys fk
                INNER JOIN sys.tables tp ON fk.referenced_object_id = tp.object_id
                INNER JOIN sys.tables tr ON fk.parent_object_id = tr.object_id
                INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                INNER JOIN sys.columns cp ON tp.object_id = cp.object_id AND fkc.referenced_column_id = cp.column_id
                INNER JOIN sys.columns cr ON tr.object_id = cr.object_id AND fkc.parent_column_id = cr.column_id
                ORDER BY tp.name, tr.name
            ";
            return ExecuteQuery(query);
        }

        // Отримання списку індексів для таблиць
        public DataTable? GetIndexes()
        {
            string query = @"
                SELECT 
                    t.name as 'Таблиця',
                    i.name as 'Індекс',
                    CASE WHEN i.is_unique = 1 THEN 'UNIQUE' ELSE 'NON-UNIQUE' END as 'Тип',
                    CASE WHEN i.is_primary_key = 1 THEN 'PRIMARY KEY' ELSE '' END as 'Основний ключ'
                FROM sys.indexes i
                INNER JOIN sys.tables t ON i.object_id = t.object_id
                WHERE i.name IS NOT NULL AND t.name NOT LIKE 'sysdiagrams%'
                ORDER BY t.name, i.name
            ";
            return ExecuteQuery(query);
        }

        // Обчислення розміру конкретної таблиці в кілобайтах
        public long GetTableSizeInKB(string tableName)
        {
            string query = $@"
                SELECT 
                    SUM(p.reserved_page_count) * 8 as SizeKB
                FROM sys.dm_db_partition_stats p
                INNER JOIN sys.tables t ON p.object_id = t.object_id
                WHERE t.name = '{tableName}'
            ";
            var result = ExecuteQuery(query);
            if (result != null && result.Rows.Count > 0)
            {
                if (long.TryParse(result.Rows[0][0]?.ToString() ?? "0", out long size))
                {
                    return size;
                }
            }
            return 0;
        }

        // Отримання загального розміру поточної бази даних
        public long GetDatabaseSizeInKB()
        {
            string query = @"
                SELECT SUM(size) * 8 as SizeKB
                FROM sys.master_files
                WHERE database_id = DB_ID()
            ";
            var result = ExecuteQuery(query);
            if (result != null && result.Rows.Count > 0)
            {
                if (long.TryParse(result.Rows[0][0]?.ToString() ?? "0", out long size))
                {
                    return size;
                }
            }
            return 0;
        }

        // Збереження файлу логу сесії до таблиці Logs
        public bool SaveLogToDatabase(int userId, string logFileName, byte[] fileContent)
        {
            try
            {
                using SqlConnection connection = new SqlConnection(_connectionString);
                connection.Open();

                string query = @"
                        INSERT INTO Logs (UserId, LogFileName, CreatedAt, ClosedAt, LogFileContent)
                        VALUES (@userId, @logFileName, @createdAt, @closedAt, @logFileContent)
                    ";

                using SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@logFileName", logFileName);
                // CreatedAt і ClosedAt ставляться на момент завершення сесії
                command.Parameters.AddWithValue("@createdAt", DateTime.Now.AddHours(-1));
                command.Parameters.AddWithValue("@closedAt", DateTime.Now);
                command.Parameters.AddWithValue("@logFileContent", fileContent);

                command.ExecuteNonQuery();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Отримання переліку логів з можливістю фільтрації за користувачем та датою
        public DataTable? GetLogs(int? userId = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            string query = @"
                SELECT 
                    l.LogId,
                    l.UserId,
                    u.Login as UserLogin,
                    l.LogFileName,
                    l.CreatedAt,
                    l.ClosedAt
                FROM Logs l
                INNER JOIN AppUsers u ON l.UserId = u.Id
                WHERE 1=1";

            if (userId.HasValue)
                query += $" AND l.UserId = {userId}";

            if (fromDate.HasValue)
                query += $" AND l.CreatedAt >= '{fromDate:yyyy-MM-dd 00:00:00}'";

            if (toDate.HasValue)
                query += $" AND l.CreatedAt <= '{toDate:yyyy-MM-dd 23:59:59}'";

            query += " ORDER BY l.CreatedAt DESC";

            return ExecuteQuery(query);
        }

        // Отримання вмісту файлу логу з таблиці Logs
        public byte[] GetLogFileContent(int logId)
        {
            string query = $"SELECT LogFileContent FROM Logs WHERE LogId = {logId}";
            var result = ExecuteQuery(query);

            if (result != null && result.Rows.Count > 0)
            {
                return result.Rows[0]["LogFileContent"] as byte[] ?? Array.Empty<byte>();
            }
            return Array.Empty<byte>();
        }

        // Видалення записів логів за масивом їх ідентифікаторів
        public bool DeleteLogs(int[] logIds)
        {
            if (logIds == null || logIds.Length == 0) return false;

            try
            {
                using SqlConnection connection = new SqlConnection(_connectionString);
                connection.Open();

                foreach (int logId in logIds)
                {
                    string query = $"DELETE FROM Logs WHERE LogId = {logId}";
                    using SqlCommand command = new SqlCommand(query, connection);
                    command.ExecuteNonQuery();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}