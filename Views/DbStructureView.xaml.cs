using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using SolarWarehouseApp.Data;
using SolarWarehouseApp.Services;

namespace SolarWarehouseApp.Views
{
    /// <summary>
    /// Represents a database column with original-value tracking for inline editing.
    /// </summary>
    public class ColumnModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Snapshots taken at load time — used to detect what changed
        public string OriginalName { get; set; } = "";
        public string OriginalDataType { get; set; } = "";
        public bool OriginalNullable { get; set; }
        public string OriginalDefault { get; set; } = "";

        /// <summary>True when this row was added by the "Додати поле" button and not yet persisted.</summary>
        public bool IsNew { get; set; }

        private string _columnName = "";
        public string ColumnName
        {
            get => _columnName;
            set { _columnName = value; OnPropertyChanged(); }
        }

        private string _dataType = "nvarchar(100)";
        public string DataType
        {
            get => _dataType;
            set { _dataType = value; OnPropertyChanged(); }
        }

        private bool _isNullable = true;
        public bool IsNullable
        {
            get => _isNullable;
            set { _isNullable = value; OnPropertyChanged(); }
        }

        private string _defaultValue = "";
        public string DefaultValue
        {
            get => _defaultValue;
            set { _defaultValue = value; OnPropertyChanged(); }
        }

        public int Position { get; set; }
    }

    public partial class DbStructureView : Page
    {
        private readonly DatabaseService _dbService;
        private readonly LogService? _logService;
        private string? _selectedTable;
        private ObservableCollection<ColumnModel> _columns = new();
        private bool _isApplyingChanges;

        /// <summary>SQL Server data types shown in the DataType ComboBox.</summary>
        public List<string> SqlDataTypes { get; } = new List<string>
        {
            "int", "bigint", "smallint", "tinyint", "bit",
            "decimal(18,2)", "decimal(10,4)", "numeric(18,2)", "float", "real",
            "money", "smallmoney",
            "nvarchar(50)", "nvarchar(100)", "nvarchar(255)", "nvarchar(500)", "nvarchar(MAX)",
            "varchar(50)", "varchar(100)", "varchar(255)", "varchar(500)", "varchar(MAX)",
            "char(10)", "nchar(10)", "text", "ntext",
            "datetime", "datetime2", "datetime2(7)", "date", "time", "smalldatetime",
            "uniqueidentifier", "varbinary(MAX)", "xml"
        };

        public DbStructureView(DatabaseService dbService, LogService? logService)
        {
            InitializeComponent();
            DataContext = this;
            _dbService = dbService;
            _logService = logService;
            Loaded += (s, e) => LoadTableTree();
        }

        // ────────────────────────────────────────────────────────────────
        // Validation
        // ────────────────────────────────────────────────────────────────

        private static bool IsValidIdentifier(string name)
            => !string.IsNullOrWhiteSpace(name) &&
               Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$");

        /// <summary>Shows the SQL command and asks the user to confirm before executing.</summary>
        private static bool ConfirmSql(string sql, string title = "Підтвердження операції")
        {
            var result = MessageBox.Show(
                $"Буде виконано SQL:\n\n{sql}\n\nПродовжити?",
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        // ────────────────────────────────────────────────────────────────
        // Table tree
        // ────────────────────────────────────────────────────────────────

        private void LoadTableTree()
        {
            TableTree.Items.Clear();

            string query = @"
                SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE, ORDINAL_POSITION
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo'
                ORDER BY TABLE_NAME, ORDINAL_POSITION";

            var result = _dbService.ExecuteQuery(query);
            if (result == null) return;

            foreach (var tableGroup in result.AsEnumerable().GroupBy(r => r["TABLE_NAME"].ToString() ?? ""))
            {
                var tableNode = new TreeViewItem
                {
                    Header = tableGroup.Key,
                    Tag = tableGroup.Key,
                    FontWeight = FontWeights.SemiBold
                };

                foreach (var col in tableGroup)
                {
                    string colName = col["COLUMN_NAME"].ToString() ?? "";
                    string dataType = col["DATA_TYPE"].ToString() ?? "";
                    string nullable = col["IS_NULLABLE"].ToString() ?? "";

                    var colNode = new TreeViewItem
                    {
                        Header = $"{colName} ({dataType}{(nullable == "YES" ? ", null" : "")})",
                        Tag = tableGroup.Key,
                        FontWeight = FontWeights.Normal,
                        Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(0x9E, 0x9E, 0x9E))
                    };
                    tableNode.Items.Add(colNode);
                }

                TableTree.Items.Add(tableNode);
            }
        }

        private void TableTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is not TreeViewItem item || item.Tag is not string tableName) return;

            _selectedTable = tableName;
            TableNameLabel.Text = tableName;
            _logService?.LogEvent("VIEW_DB_STRUCTURE", tableName, null, "");
            LoadTableDetails(tableName);
        }

        private void LoadTableDetails(string tableName)
        {
            LoadColumns(tableName);
            LoadPrimaryKeys(tableName);
            LoadForeignKeys(tableName);
            LoadIndexes(tableName);
        }

        // ────────────────────────────────────────────────────────────────
        // Load helpers
        // ────────────────────────────────────────────────────────────────

        private void LoadColumns(string tableName)
        {
            const string colQuery = @"
                SELECT
                    c.COLUMN_NAME,
                    c.DATA_TYPE,
                    c.IS_NULLABLE,
                    ISNULL(c.COLUMN_DEFAULT, '') AS COLUMN_DEFAULT,
                    c.ORDINAL_POSITION,
                    ISNULL(c.CHARACTER_MAXIMUM_LENGTH, 0) AS MAX_LENGTH,
                    ISNULL(c.NUMERIC_PRECISION, 0)        AS NUMERIC_PRECISION,
                    ISNULL(c.NUMERIC_SCALE, 0)            AS NUMERIC_SCALE
                FROM INFORMATION_SCHEMA.COLUMNS c
                WHERE c.TABLE_SCHEMA = 'dbo' AND c.TABLE_NAME = @tableName
                ORDER BY c.ORDINAL_POSITION";

            var dt = _dbService.ExecuteQueryWithParameters(
                colQuery, new[] { new SqlParameter("@tableName", tableName) });
            if (dt == null) return;

            _columns = new ObservableCollection<ColumnModel>();

            foreach (DataRow row in dt.Rows)
            {
                string baseType = row["DATA_TYPE"].ToString() ?? "nvarchar";
                int maxLen = Convert.ToInt32(row["MAX_LENGTH"]);
                int numPrec = Convert.ToInt32(row["NUMERIC_PRECISION"]);
                int numScale = Convert.ToInt32(row["NUMERIC_SCALE"]);
                string fullType = BuildFullType(baseType, maxLen, numPrec, numScale);

                // Strip extra parentheses that SQL Server wraps around defaults, e.g. ((0)) → 0
                string defaultVal = row["COLUMN_DEFAULT"].ToString() ?? "";
                while (defaultVal.Length >= 2 && defaultVal.StartsWith('(') && defaultVal.EndsWith(')'))
                    defaultVal = defaultVal[1..^1];

                var col = new ColumnModel
                {
                    ColumnName = row["COLUMN_NAME"].ToString() ?? "",
                    DataType = fullType,
                    IsNullable = row["IS_NULLABLE"].ToString() == "YES",
                    DefaultValue = defaultVal,
                    Position = Convert.ToInt32(row["ORDINAL_POSITION"])
                };
                col.OriginalName = col.ColumnName;
                col.OriginalDataType = col.DataType;
                col.OriginalNullable = col.IsNullable;
                col.OriginalDefault = col.DefaultValue;
                _columns.Add(col);
            }

            ColumnsGrid.ItemsSource = _columns;
        }

        private static string BuildFullType(string baseType, int maxLen, int numPrec, int numScale)
            => baseType switch
            {
                "nvarchar" or "varchar" or "char" or "nchar" or "varbinary" or "binary"
                    => maxLen == -1 ? $"{baseType}(MAX)" : maxLen > 0 ? $"{baseType}({maxLen})" : baseType,
                "decimal" or "numeric"
                    => numPrec > 0 ? $"{baseType}({numPrec},{numScale})" : baseType,
                "datetime2" or "time" or "datetimeoffset"
                    => numScale > 0 ? $"{baseType}({numScale})" : baseType,
                _ => baseType
            };

        private void LoadPrimaryKeys(string tableName)
        {
            const string query = @"
                SELECT
                    kc.CONSTRAINT_NAME AS 'Назва обмеження',
                    kc.COLUMN_NAME     AS 'Поле'
                FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kc
                INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    ON kc.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                    AND kc.TABLE_SCHEMA = 'dbo'
                    AND kc.TABLE_NAME   = @tableName
                ORDER BY kc.ORDINAL_POSITION";

            var dt = _dbService.ExecuteQueryWithParameters(
                query, new[] { new SqlParameter("@tableName", tableName) });
            PkGrid.ItemsSource = dt?.DefaultView;
        }

        private void LoadForeignKeys(string tableName)
        {
            const string query = @"
                SELECT
                    fk.name  AS 'Назва FK',
                    tp.name  AS 'Батьківська таблиця',
                    cp.name  AS 'Батьківське поле',
                    tr.name  AS 'Дочірня таблиця',
                    cr.name  AS 'Дочірнє поле'
                FROM sys.foreign_keys fk
                INNER JOIN sys.tables tp   ON fk.referenced_object_id = tp.object_id
                INNER JOIN sys.tables tr   ON fk.parent_object_id     = tr.object_id
                INNER JOIN sys.foreign_key_columns fkc
                    ON fk.object_id = fkc.constraint_object_id
                INNER JOIN sys.columns cp
                    ON tp.object_id = cp.object_id AND fkc.referenced_column_id = cp.column_id
                INNER JOIN sys.columns cr
                    ON tr.object_id = cr.object_id AND fkc.parent_column_id     = cr.column_id
                WHERE tp.name = @tableName OR tr.name = @tableName
                ORDER BY tp.name, tr.name";

            var dt = _dbService.ExecuteQueryWithParameters(
                query, new[] { new SqlParameter("@tableName", tableName) });
            FkGrid.ItemsSource = dt?.DefaultView;
        }

        private void LoadIndexes(string tableName)
        {
            const string query = @"
                SELECT
                    i.name   AS 'Індекс',
                    c.name   AS 'Поле',
                    CASE WHEN i.is_unique      = 1 THEN 'UNIQUE'       ELSE 'NON-UNIQUE'  END AS 'Тип',
                    CASE WHEN i.is_primary_key = 1 THEN 'Так'          ELSE 'Ні'          END AS 'PK'
                FROM sys.indexes i
                INNER JOIN sys.tables t       ON i.object_id = t.object_id
                INNER JOIN sys.index_columns ic
                    ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                INNER JOIN sys.columns c
                    ON t.object_id = c.object_id AND ic.column_id = c.column_id
                WHERE i.name IS NOT NULL AND t.name = @tableName
                ORDER BY i.name, c.name";

            var dt = _dbService.ExecuteQueryWithParameters(
                query, new[] { new SqlParameter("@tableName", tableName) });
            IndexesGrid.ItemsSource = dt?.DefaultView;
        }

        // ────────────────────────────────────────────────────────────────
        // Column inline editing
        // ────────────────────────────────────────────────────────────────

        private void AddColumn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTable == null)
            {
                MessageBox.Show("Оберіть таблицю зліва.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newCol = new ColumnModel
            {
                ColumnName = "NewColumn",
                DataType = "nvarchar(100)",
                IsNullable = true,
                DefaultValue = "",
                IsNew = true
            };
            _columns.Add(newCol);
            ColumnsGrid.SelectedItem = newCol;
            ColumnsGrid.ScrollIntoView(newCol);
            ColumnsGrid.CurrentCell = new DataGridCellInfo(newCol, ColumnsGrid.Columns[0]);
            ColumnsGrid.BeginEdit();
        }

        private void DeleteColumn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTable == null)
            {
                MessageBox.Show("Оберіть таблицю зліва.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ColumnsGrid.SelectedItem is not ColumnModel col)
            {
                MessageBox.Show("Оберіть поле для видалення.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (col.IsNew)
            {
                _columns.Remove(col);
                return;
            }

            if (!IsValidIdentifier(col.ColumnName))
            {
                MessageBox.Show("Некоректна назва поля.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string sql = $"ALTER TABLE [{_selectedTable}] DROP COLUMN [{col.ColumnName}]";
            if (!ConfirmSql(sql, "Видалення поля")) return;

            if (_dbService.ExecuteNonQuery(sql))
            {
                _logService?.LogEvent("DROP_COLUMN", _selectedTable, null, col.ColumnName);
                _columns.Remove(col);
                LoadTableTree();
            }
        }

        private void ColumnsGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (_isApplyingChanges) return;
            if (e.Row.Item is not ColumnModel col) return;

            if (e.EditAction == DataGridEditAction.Cancel)
            {
                if (col.IsNew) _columns.Remove(col);
                return;
            }

            // Defer until after the DataGrid has committed the row values
            var capturedCol = col;
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() =>
                {
                    if (_isApplyingChanges) return;
                    _isApplyingChanges = true;
                    try
                    {
                        if (capturedCol.IsNew)
                            HandleAddColumn(capturedCol);
                        else
                            HandleEditColumn(capturedCol);
                    }
                    finally
                    {
                        _isApplyingChanges = false;
                    }
                }));
        }

        private void HandleAddColumn(ColumnModel col)
        {
            if (_selectedTable == null) return;

            if (!IsValidIdentifier(col.ColumnName))
            {
                MessageBox.Show(
                    "Некоректна назва поля. Дозволені: літери, цифри, підкреслення.",
                    "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                _columns.Remove(col);
                return;
            }

            string nullPart = col.IsNullable ? "NULL" : "NOT NULL";
            string defaultPart = !string.IsNullOrWhiteSpace(col.DefaultValue)
                ? $" DEFAULT {col.DefaultValue}" : "";
            string sql =
                $"ALTER TABLE [{_selectedTable}] ADD [{col.ColumnName}] {col.DataType} {nullPart}{defaultPart}";

            if (!ConfirmSql(sql, "Додавання поля"))
            {
                _columns.Remove(col);
                return;
            }

            if (_dbService.ExecuteNonQuery(sql))
            {
                _logService?.LogEvent("ADD_COLUMN", _selectedTable, null, $"{col.ColumnName} {col.DataType}");
                col.IsNew = false;
                col.OriginalName = col.ColumnName;
                col.OriginalDataType = col.DataType;
                col.OriginalNullable = col.IsNullable;
                col.OriginalDefault = col.DefaultValue;
                LoadTableTree();
                LoadColumns(_selectedTable);
            }
            else
            {
                _columns.Remove(col);
            }
        }

        private void HandleEditColumn(ColumnModel col)
        {
            if (_selectedTable == null) return;

            bool nameChanged = col.ColumnName != col.OriginalName;
            bool typeChanged = col.DataType != col.OriginalDataType;
            bool nullableChanged = col.IsNullable != col.OriginalNullable;
            bool defaultChanged = col.DefaultValue != col.OriginalDefault;

            if (!nameChanged && !typeChanged && !nullableChanged && !defaultChanged) return;

            if (!IsValidIdentifier(col.ColumnName))
            {
                MessageBox.Show("Некоректна назва поля.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                col.ColumnName = col.OriginalName;
                return;
            }

            // 1. Rename
            if (nameChanged)
            {
                string renameSql =
                    $"EXEC sp_rename '[{_selectedTable}].[{col.OriginalName}]', '{col.ColumnName}', 'COLUMN'";
                if (!ConfirmSql(renameSql, "Перейменування поля"))
                {
                    col.ColumnName = col.OriginalName;
                    return;
                }
                if (!_dbService.ExecuteNonQuery(renameSql))
                {
                    col.ColumnName = col.OriginalName;
                    return;
                }
                _logService?.LogEvent("RENAME_COLUMN", _selectedTable, null,
                    $"{col.OriginalName} → {col.ColumnName}");
                col.OriginalName = col.ColumnName;
            }

            // 2. Alter type / nullable
            if (typeChanged || nullableChanged)
            {
                string nullPart = col.IsNullable ? "NULL" : "NOT NULL";
                string alterSql =
                    $"ALTER TABLE [{_selectedTable}] ALTER COLUMN [{col.ColumnName}] {col.DataType} {nullPart}";
                if (!ConfirmSql(alterSql, "Зміна типу / nullable"))
                {
                    col.DataType = col.OriginalDataType;
                    col.IsNullable = col.OriginalNullable;
                    return;
                }
                if (!_dbService.ExecuteNonQuery(alterSql))
                {
                    col.DataType = col.OriginalDataType;
                    col.IsNullable = col.OriginalNullable;
                    return;
                }
                _logService?.LogEvent("ALTER_COLUMN", _selectedTable, null,
                    $"{col.ColumnName} {col.DataType} {nullPart}");
                col.OriginalDataType = col.DataType;
                col.OriginalNullable = col.IsNullable;
            }

            // 3. Change default value
            if (defaultChanged)
            {
                ApplyDefaultChange(_selectedTable, col.ColumnName, col.DefaultValue);
                col.OriginalDefault = col.DefaultValue;
            }

            LoadTableTree();
        }

        private void ApplyDefaultChange(string tableName, string columnName, string newDefault)
        {
            // Find the existing default constraint name (if any).
            // tableName and columnName are pre-validated by IsValidIdentifier, so use parameterized query.
            const string findSql = @"
                SELECT dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c
                    ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                INNER JOIN sys.tables t ON c.object_id = t.object_id
                WHERE t.name = @tableName AND c.name = @columnName";

            var result = _dbService.ExecuteQueryWithParameters(findSql, new[]
            {
                new SqlParameter("@tableName", tableName),
                new SqlParameter("@columnName", columnName)
            });
            string? constraintName = result?.Rows.Count > 0 ? result.Rows[0][0]?.ToString() : null;

            var sqlParts = new List<string>();
            if (!string.IsNullOrEmpty(constraintName))
                sqlParts.Add($"ALTER TABLE [{tableName}] DROP CONSTRAINT [{constraintName}]");

            if (!string.IsNullOrWhiteSpace(newDefault))
            {
                string newConstraint = $"DF_{tableName}_{columnName}";
                sqlParts.Add(
                    $"ALTER TABLE [{tableName}] ADD CONSTRAINT [{newConstraint}] DEFAULT {newDefault} FOR [{columnName}]");
            }

            if (sqlParts.Count == 0) return;

            string fullSql = string.Join(";\n", sqlParts);
            if (!ConfirmSql(fullSql, "Зміна значення за замовчуванням")) return;

            foreach (string sql in sqlParts)
                if (!_dbService.ExecuteNonQuery(sql)) return;

            _logService?.LogEvent("ALTER_DEFAULT", tableName, null,
                $"{columnName} DEFAULT '{newDefault}'");
        }

        // ────────────────────────────────────────────────────────────────
        // Primary Key operations
        // ────────────────────────────────────────────────────────────────

        private void AddPk_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTable == null)
            {
                MessageBox.Show("Оберіть таблицю зліва.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new InputTextDialog(
                "Додати первинний ключ",
                "Назва обмеження (PK_...)",
                "Поле(я) через кому");
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() != true) return;

            string constraintName = dialog.Value1.Trim();
            string rawColumns = dialog.Value2.Trim();

            if (!IsValidIdentifier(constraintName))
            {
                MessageBox.Show("Некоректна назва обмеження.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Validate each column name
            string[] colArray = rawColumns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (colArray.Length == 0 || Array.Exists(colArray, c => !IsValidIdentifier(c)))
            {
                MessageBox.Show("Некоректні назви полів.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string cols = string.Join("], [", colArray);
            string sql =
                $"ALTER TABLE [{_selectedTable}] ADD CONSTRAINT [{constraintName}] PRIMARY KEY ([{cols}])";
            if (!ConfirmSql(sql, "Додавання первинного ключа")) return;

            if (_dbService.ExecuteNonQuery(sql))
            {
                _logService?.LogEvent("ADD_PRIMARY_KEY", _selectedTable, null, constraintName);
                LoadPrimaryKeys(_selectedTable);
                LoadIndexes(_selectedTable);
            }
        }

        private void DeletePk_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTable == null)
            {
                MessageBox.Show("Оберіть таблицю зліва.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (PkGrid.SelectedItem is not DataRowView pkRow)
            {
                MessageBox.Show("Оберіть рядок первинного ключа для видалення.",
                    "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string constraintName = pkRow["Назва обмеження"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(constraintName)) return;

            string sql = $"ALTER TABLE [{_selectedTable}] DROP CONSTRAINT [{constraintName}]";
            if (!ConfirmSql(sql, "Видалення первинного ключа")) return;

            if (_dbService.ExecuteNonQuery(sql))
            {
                _logService?.LogEvent("DROP_PRIMARY_KEY", _selectedTable, null, constraintName);
                LoadPrimaryKeys(_selectedTable);
                LoadIndexes(_selectedTable);
            }
        }

        // ────────────────────────────────────────────────────────────────
        // Foreign Key operations
        // ────────────────────────────────────────────────────────────────

        private void AddFk_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTable == null)
            {
                MessageBox.Show("Оберіть таблицю зліва.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new InputTextDialog(
                "Додати зовнішній ключ",
                "Назва FK (FK_...)",
                "Поле в цій таблиці",
                "Батьківська таблиця",
                label4: "Батьківське поле");
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() != true) return;

            string fkName = dialog.Value1.Trim();
            string childColumn = dialog.Value2.Trim();
            string parentTable = dialog.Value3.Trim();
            string parentColumn = dialog.Value4.Trim();

            if (!IsValidIdentifier(fkName) || !IsValidIdentifier(childColumn) ||
                !IsValidIdentifier(parentTable) || !IsValidIdentifier(parentColumn))
            {
                MessageBox.Show(
                    "Некоректні імена. Дозволені: літери, цифри, підкреслення.",
                    "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string sql =
                $"ALTER TABLE [{_selectedTable}] ADD CONSTRAINT [{fkName}] " +
                $"FOREIGN KEY ([{childColumn}]) REFERENCES [{parentTable}] ([{parentColumn}])";
            if (!ConfirmSql(sql, "Додавання зовнішнього ключа")) return;

            if (_dbService.ExecuteNonQuery(sql))
            {
                _logService?.LogEvent("ADD_FOREIGN_KEY", _selectedTable, null, fkName);
                LoadForeignKeys(_selectedTable);
            }
        }

        private void DeleteFk_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTable == null)
            {
                MessageBox.Show("Оберіть таблицю зліва.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (FkGrid.SelectedItem is not DataRowView fkRow)
            {
                MessageBox.Show("Оберіть рядок FK для видалення.",
                    "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string fkName = fkRow["Назва FK"]?.ToString() ?? "";
            string childTable = fkRow["Дочірня таблиця"]?.ToString() ?? _selectedTable;
            if (string.IsNullOrEmpty(fkName)) return;

            string sql = $"ALTER TABLE [{childTable}] DROP CONSTRAINT [{fkName}]";
            if (!ConfirmSql(sql, "Видалення зовнішнього ключа")) return;

            if (_dbService.ExecuteNonQuery(sql))
            {
                _logService?.LogEvent("DROP_FOREIGN_KEY", _selectedTable, null, fkName);
                LoadForeignKeys(_selectedTable);
            }
        }

        // ────────────────────────────────────────────────────────────────
        // Index operations
        // ────────────────────────────────────────────────────────────────

        private void AddIndex_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTable == null)
            {
                MessageBox.Show("Оберіть таблицю зліва.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new InputTextDialog(
                "Додати індекс",
                "Назва індексу",
                "Поле",
                label3: "Тип (UNIQUE або залиште пустим)");
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() != true) return;

            string indexName = dialog.Value1.Trim();
            string column = dialog.Value2.Trim();
            bool isUnique = dialog.Value3.Trim().Equals("UNIQUE", StringComparison.OrdinalIgnoreCase);

            if (!IsValidIdentifier(indexName) || !IsValidIdentifier(column))
            {
                MessageBox.Show("Некоректні імена.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string uniquePart = isUnique ? "UNIQUE " : "";
            string sql = $"CREATE {uniquePart}INDEX [{indexName}] ON [{_selectedTable}] ([{column}])";
            if (!ConfirmSql(sql, "Створення індексу")) return;

            if (_dbService.ExecuteNonQuery(sql))
            {
                _logService?.LogEvent("CREATE_INDEX", _selectedTable, null, indexName);
                LoadIndexes(_selectedTable);
            }
        }

        private void DeleteIndex_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTable == null)
            {
                MessageBox.Show("Оберіть таблицю зліва.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (IndexesGrid.SelectedItem is not DataRowView idxRow)
            {
                MessageBox.Show("Оберіть індекс для видалення.",
                    "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string indexName = idxRow["Індекс"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(indexName)) return;

            if (idxRow["PK"]?.ToString() == "Так")
            {
                MessageBox.Show(
                    "Індекс первинного ключа видаляється через вкладку 'Первинні ключі'.",
                    "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string sql = $"DROP INDEX [{indexName}] ON [{_selectedTable}]";
            if (!ConfirmSql(sql, "Видалення індексу")) return;

            if (_dbService.ExecuteNonQuery(sql))
            {
                _logService?.LogEvent("DROP_INDEX", _selectedTable, null, indexName);
                LoadIndexes(_selectedTable);
            }
        }

        // ────────────────────────────────────────────────────────────────
        // Table-level operations
        // ────────────────────────────────────────────────────────────────

        private void CreateTable_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputTextDialog("Створити таблицю", "Назва таблиці");
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() != true) return;

            string tableName = dialog.Value1.Trim();
            if (!IsValidIdentifier(tableName))
            {
                MessageBox.Show(
                    "Некоректна назва таблиці. Дозволені: літери, цифри, підкреслення.",
                    "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string sql =
                $"CREATE TABLE [{tableName}] " +
                $"(Id INT IDENTITY(1,1) NOT NULL, CONSTRAINT [PK_{tableName}] PRIMARY KEY (Id))";
            if (!ConfirmSql(sql, "Створення таблиці")) return;

            if (_dbService.ExecuteNonQuery(sql))
            {
                _logService?.LogEvent("CREATE_TABLE", tableName, null, "");
                LoadTableTree();
            }
        }

        private void DeleteTable_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTable == null)
            {
                MessageBox.Show("Оберіть таблицю зліва.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string sql = $"DROP TABLE [{_selectedTable}]";
            if (!ConfirmSql(sql, "Видалення таблиці — НЕЗВОРОТНА ДІЯ!")) return;

            if (_dbService.ExecuteNonQuery(sql))
            {
                _logService?.LogEvent("DROP_TABLE", _selectedTable, null, "");
                _selectedTable = null;
                TableNameLabel.Text = "Оберіть таблицю зліва";
                ColumnsGrid.ItemsSource = null;
                PkGrid.ItemsSource = null;
                FkGrid.ItemsSource = null;
                IndexesGrid.ItemsSource = null;
                LoadTableTree();
            }
        }
    }
}

