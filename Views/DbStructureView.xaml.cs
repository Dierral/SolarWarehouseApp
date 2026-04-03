using System.Data;
using System.Windows;
using System.Windows.Controls;
using SolarWarehouseApp.Data;
using SolarWarehouseApp.Services;

namespace SolarWarehouseApp.Views
{
    public partial class DbStructureView : Page
    {
        private readonly DatabaseService _dbService;
        private readonly LogService? _logService;

        public DbStructureView(DatabaseService dbService, LogService? logService)
        {
            InitializeComponent();
            _dbService = dbService;
            _logService = logService;
            Loaded += (s, e) => LoadTableTree();
        }

        private void LoadTableTree()
        {
            TableTree.Items.Clear();

            // Get all tables with their columns
            string query = @"
                SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE, ORDINAL_POSITION
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo'
                ORDER BY TABLE_NAME, ORDINAL_POSITION";

            var result = _dbService.ExecuteQuery(query);
            if (result == null) return;

            var tableGroups = result.AsEnumerable()
                .GroupBy(r => r["TABLE_NAME"].ToString() ?? "");

            foreach (var tableGroup in tableGroups)
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
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9E, 0x9E, 0x9E))
                    };
                    tableNode.Items.Add(colNode);
                }

                TableTree.Items.Add(tableNode);
            }
        }

        private void TableTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is not TreeViewItem item || item.Tag is not string tableName) return;

            // Validate tableName is a known table (from INFORMATION_SCHEMA, sanitize any injection attempt)
            // tableName is from our own tree built from DB metadata, but we still use parameterized query where possible
            TableNameLabel.Text = tableName;
            _logService?.LogEvent("VIEW_DB_STRUCTURE", tableName, null, "");

            // Load columns using parameterized query
            string colQuery = @"
                SELECT COLUMN_NAME as 'Поле', DATA_TYPE as 'Тип', IS_NULLABLE as 'Nullable',
                       COLUMN_DEFAULT as 'За замовчуванням', ORDINAL_POSITION as 'Позиція'
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @tableName
                ORDER BY ORDINAL_POSITION";
            var colParams = new[] { new Microsoft.Data.SqlClient.SqlParameter("@tableName", tableName) };
            ColumnsGrid.ItemsSource = _dbService.ExecuteQueryWithParameters(colQuery, colParams)?.DefaultView;

            // Load primary keys for this table
            var pk = _dbService.GetPrimaryKeys();
            if (pk != null)
            {
                var filtered = pk.AsEnumerable().Where(r => r["Таблиця"].ToString() == tableName).CopyToDataTable();
                PkGrid.ItemsSource = filtered.DefaultView;
            }

            // Load foreign keys for this table
            var fk = _dbService.GetForeignKeys();
            if (fk != null)
            {
                try
                {
                    var filtered = fk.AsEnumerable()
                        .Where(r => r["Батьківська таблиця"].ToString() == tableName || r["Дочірня таблиця"].ToString() == tableName)
                        .CopyToDataTable();
                    FkGrid.ItemsSource = filtered.DefaultView;
                }
                catch { FkGrid.ItemsSource = null; }
            }

            // Load indexes for this table
            var idx = _dbService.GetIndexes();
            if (idx != null)
            {
                try
                {
                    var filtered = idx.AsEnumerable()
                        .Where(r => r["Таблиця"].ToString() == tableName)
                        .CopyToDataTable();
                    IndexesGrid.ItemsSource = filtered.DefaultView;
                }
                catch { IndexesGrid.ItemsSource = null; }
            }
        }
    }
}
