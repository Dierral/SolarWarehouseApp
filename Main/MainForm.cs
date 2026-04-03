using SolarWarehouseApp.Services;
using SolarWarehouseApp.Data;
using SolarWarehouseApp.Forms;
using Microsoft.Data.SqlClient;
using System.Data;

namespace SolarWarehouseApp.Main
{
    // Головна форма додатку – містить всі вкладки, меню та бізнес‑логіку роботи зі складом
    public class MainForm : Form
    {
        private readonly DatabaseService _dbService;
        private readonly string? _userId;
        private readonly string? _userRole;
        private readonly TabControl tabControl = new TabControl();
        private LogService _logService = null!;
        private System.Threading.Timer? backupTimer;

        public MainForm(DatabaseService dbService, string? userId, string? userRole)
        {
            _dbService = dbService;
            _userId = userId;
            _userRole = userRole;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            Text = $"Solar Warehouse - Ласкаво просимо, {_userId}!";
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterScreen;

            // Ініціалізація підсистеми логування дій користувача
            InitializeLogging();
            // Створення головного меню та вкладок
            CreateMenuAndControls();
            // За замовчуванням відкривається вкладка «Поставки»
            ShowSuppliesTab();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // При закритті форми коректно завершуємо сесію логування
            FinalizeLogging();
            base.OnFormClosing(e);
        }

        private void CreateMenuAndControls()
        {
            MenuStrip menu = new MenuStrip();

            ToolStripMenuItem fileMenu = new ToolStripMenuItem("Файл");
            fileMenu.DropDownItems.Add("Вихід", null, (s, e) =>
            {
                LogUserAction("APP_EXIT");
                Close();
            });
            menu.Items.Add(fileMenu);

            ToolStripMenuItem warehouseMenu = new ToolStripMenuItem("Склад");
            warehouseMenu.DropDownItems.Add("Товари", null, (s, e) =>
            {
                LogUserAction("TAB_SWITCH", "Товари");
                ShowWarehouseItemsTab();
            });
            warehouseMenu.DropDownItems.Add("Поставки", null, (s, e) =>
            {
                LogUserAction("TAB_SWITCH", "Поставки");
                ShowSuppliesTab();
            });
            menu.Items.Add(warehouseMenu);

            ToolStripMenuItem referenceMenu = new ToolStripMenuItem("Довідники");
            referenceMenu.DropDownItems.Add("Типи обладнання", null, (s, e) =>
            {
                LogUserAction("TAB_SWITCH", "Типи обладнання");
                ShowEquipmentTypesTab();
            });
            referenceMenu.DropDownItems.Add("Виробники", null, (s, e) =>
            {
                LogUserAction("TAB_SWITCH", "Виробники");
                ShowManufacturersTab();
            });
            referenceMenu.DropDownItems.Add("Місця зберігання", null, (s, e) =>
            {
                LogUserAction("TAB_SWITCH", "Місця зберігання");
                ShowStorageLocationsTab();
            });
            menu.Items.Add(referenceMenu);

            // Адміністративне меню доступне лише для користувачів з роллю Admin
            if (_userRole == "Admin")
            {
                ToolStripMenuItem adminMenu = new ToolStripMenuItem("Адміністрація");
                adminMenu.DropDownItems.Add("Керування користувачами", null, (s, e) =>
                {
                    LogUserAction("TAB_SWITCH", "Керування користувачами");
                    ShowAppUsersTab();
                });
                adminMenu.DropDownItems.Add("SQL-консоль", null, (s, e) =>
                {
                    LogUserAction("TAB_SWITCH", "SQL-консоль");
                    ShowSQLConsoleTab();
                });
                adminMenu.DropDownItems.Add("Структура БД", null, (s, e) =>
                {
                    LogUserAction("TAB_SWITCH", "Структура БД");
                    ShowDatabaseStructureTab();
                });
                adminMenu.DropDownItems.Add("Backup/Restore", null, (s, e) =>
                {
                    LogUserAction("TAB_SWITCH", "Backup/Restore");
                    ShowBackupRestoreTab();
                });
                adminMenu.DropDownItems.Add("Логи", null, (s, e) =>
                {
                    LogUserAction("TAB_SWITCH", "Логи");
                    ShowLogsTab();
                });
                menu.Items.Add(adminMenu);
            }

            MainMenuStrip = menu;
            Controls.Add(menu);

            tabControl.Dock = DockStyle.Fill;
            Controls.Add(tabControl);
        }

        private DataGridView CreateGrid(string name)
        {
            // Уніфіковане створення таблиці з типовими налаштуваннями
            DataGridView grid = new DataGridView
            {
                Name = name,
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                AllowUserToResizeColumns = true,
                ScrollBars = ScrollBars.Both
            };
            return grid;
        }

        private void SetupDataGridView(DataGridView grid, DataTable? data, Dictionary<string, string> columnHeaders)
        {
            // Прив'язка даних до DataGridView та встановлення «людяних» заголовків колонок
            if (data == null) return;

            grid.DataSource = null;
            grid.Columns.Clear();
            grid.DataSource = data;

            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (columnHeaders.ContainsKey(column.Name))
                {
                    column.HeaderText = columnHeaders[column.Name];
                }
            }
        }

        // Допоміжний метод для запису дій користувача в лог
        private void LogUserAction(string action, string details = "")
        {
            if (_logService == null) return;

            string logMessage = action;
            if (!string.IsNullOrEmpty(details))
            {
                logMessage += $" - {details}";
            }
            _logService.LogEvent("USER_ACTION", null, null, logMessage);
        }

        #region Equipment Types

        // Вкладка «Типи обладнання» (довідник EquipmentTypes)
        private void ShowEquipmentTypesTab()
        {
            const string tabName = "equipment_types_tab";
            TabPage? existingTab = tabControl.TabPages.Cast<TabPage>().FirstOrDefault(t => t.Name == tabName);
            if (existingTab != null)
            {
                tabControl.SelectedTab = existingTab;
                return;
            }

            TabPage tab = new TabPage("Типи обладнання")
            {
                Name = tabName,
                Padding = new Padding(0)
            };

            DataGridView grid = CreateGrid("equipmentTypesGrid");
            tab.Controls.Add(grid);

            // Панель з кнопками CRUD‑операцій для довідника
            Panel buttonPanel = new Panel
            {
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = Color.LightGray
            };

            Button addButton = new Button
            {
                Text = "➕ Додати",
                Location = new Point(10, 10),
                Width = 100,
                Height = 30
            };
            addButton.Click += (s, e) => AddEquipmentType(tab);
            buttonPanel.Controls.Add(addButton);

            Button editButton = new Button
            {
                Text = "✏️ Редагувати",
                Location = new Point(120, 10),
                Width = 100,
                Height = 30
            };
            editButton.Click += (s, e) => EditEquipmentType(tab);
            buttonPanel.Controls.Add(editButton);

            Button deleteButton = new Button
            {
                Text = "🗑️ Видалити",
                Location = new Point(230, 10),
                Width = 100,
                Height = 30
            };
            deleteButton.Click += (s, e) => DeleteEquipmentType(tab);
            buttonPanel.Controls.Add(deleteButton);

            Button refreshButton = new Button
            {
                Text = "🔄 Оновити",
                Location = new Point(340, 10),
                Width = 100,
                Height = 30
            };
            refreshButton.Click += (s, e) => LoadEquipmentTypes(tab);
            buttonPanel.Controls.Add(refreshButton);

            tab.Controls.Add(buttonPanel);

            tabControl.TabPages.Add(tab);
            tabControl.SelectedTab = tab;

            LoadEquipmentTypes(tab);
        }

        private void LoadEquipmentTypes(TabPage tab)
        {
            DataGridView? grid = tab.Controls["equipmentTypesGrid"] as DataGridView;
            if (grid == null) return;

            var result = _dbService.ExecuteQuery("SELECT Id, Name, Description, IsActive FROM EquipmentTypes ORDER BY Name");

            var columnHeaders = new Dictionary<string, string>
            {
                { "Id", "№" },
                { "Name", "Назва" },
                { "Description", "Опис" },
                { "IsActive", "Активний" }
            };

            SetupDataGridView(grid, result, columnHeaders);
        }

        private void AddEquipmentType(TabPage tab)
        {
            EquipmentTypeForm form = new EquipmentTypeForm();
            if (form.ShowDialog() == DialogResult.OK)
            {
                string name = form.ItemName;
                string description = form.Description;

                try
                {
                    string query = "INSERT INTO EquipmentTypes (Name, Description, IsActive) VALUES (@name, @description, 1)";
                    SqlParameter[] parameters =
                    {
                        new SqlParameter("@name", name),
                        new SqlParameter("@description", description)
                    };

                    if (_dbService.ExecuteNonQueryWithParameters(query, parameters))
                    {
                        LogUserAction("CREATE", $"EquipmentType: {name}");
                        MessageBox.Show("Запис додан!", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadEquipmentTypes(tab);
                    }
                    else
                    {
                        LogUserAction("CREATE_FAILED", $"EquipmentType: {name}");
                        MessageBox.Show("Помилка при додаванні!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogUserAction("CREATE_ERROR", $"EquipmentType: {name} - {ex.Message}");
                    MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void EditEquipmentType(TabPage tab)
        {
            DataGridView? grid = tab.Controls["equipmentTypesGrid"] as DataGridView;
            if (grid == null || grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Виберіть запис!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int id = int.Parse(grid.SelectedRows[0].Cells["Id"].Value?.ToString() ?? "0");
            string name = grid.SelectedRows[0].Cells["Name"].Value?.ToString() ?? "";
            string description = grid.SelectedRows[0].Cells["Description"].Value?.ToString() ?? "";

            EquipmentTypeForm form = new EquipmentTypeForm(name, description);
            if (form.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string query = "UPDATE EquipmentTypes SET Name = @name, Description = @description WHERE Id = @id";
                    SqlParameter[] parameters =
                    {
                        new SqlParameter("@id", id),
                        new SqlParameter("@name", form.ItemName),
                        new SqlParameter("@description", form.Description)
                    };

                    if (_dbService.ExecuteNonQueryWithParameters(query, parameters))
                    {
                        LogUserAction("UPDATE", $"EquipmentType ID={id}: {form.ItemName}");
                        MessageBox.Show("Запис оновлений!", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadEquipmentTypes(tab);
                    }
                    else
                    {
                        LogUserAction("UPDATE_FAILED", $"EquipmentType ID={id}");
                        MessageBox.Show("Помилка при оновленні!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogUserAction("UPDATE_ERROR", $"EquipmentType ID={id} - {ex.Message}");
                    MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DeleteEquipmentType(TabPage tab)
        {
            DataGridView? grid = tab.Controls["equipmentTypesGrid"] as DataGridView;
            if (grid == null || grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Виберіть запис!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("Видалити?", "Підтвердження", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                int id = int.Parse(grid.SelectedRows[0].Cells["Id"].Value?.ToString() ?? "0");
                string name = grid.SelectedRows[0].Cells["Name"].Value?.ToString() ?? "";
                try
                {
                    string query = "DELETE FROM EquipmentTypes WHERE Id = @id";
                    SqlParameter[] parameters = { new SqlParameter("@id", id) };

                    if (_dbService.ExecuteNonQueryWithParameters(query, parameters))
                    {
                        LogUserAction("DELETE", $"EquipmentType ID={id}: {name}");
                        MessageBox.Show("Видалено!", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadEquipmentTypes(tab);
                    }
                    else
                    {
                        LogUserAction("DELETE_FAILED", $"EquipmentType ID={id}");
                        MessageBox.Show("Помилка при видаленні!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogUserAction("DELETE_ERROR", $"EquipmentType ID={id} - {ex.Message}");
                    MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

        #region Manufacturers

        // Вкладка «Виробники» (довідник Manufacturers)
        private void ShowManufacturersTab()
        {
            const string tabName = "manufacturers_tab";
            TabPage? existingTab = tabControl.TabPages.Cast<TabPage>().FirstOrDefault(t => t.Name == tabName);
            if (existingTab != null)
            {
                tabControl.SelectedTab = existingTab;
                return;
            }

            TabPage tab = new TabPage("Виробники")
            {
                Name = tabName,
                Padding = new Padding(0)
            };

            DataGridView grid = CreateGrid("manufacturersGrid");
            tab.Controls.Add(grid);

            Panel buttonPanel = new Panel
            {
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = Color.LightGray
            };

            Button addButton = new Button
            {
                Text = "➕ Додати",
                Location = new Point(10, 10),
                Width = 100,
                Height = 30
            };
            addButton.Click += (s, e) => AddManufacturer(tab);
            buttonPanel.Controls.Add(addButton);

            Button editButton = new Button
            {
                Text = "✏️ Редагувати",
                Location = new Point(120, 10),
                Width = 100,
                Height = 30
            };
            editButton.Click += (s, e) => EditManufacturer(tab);
            buttonPanel.Controls.Add(editButton);

            Button deleteButton = new Button
            {
                Text = "🗑️ Видалити",
                Location = new Point(230, 10),
                Width = 100,
                Height = 30
            };
            deleteButton.Click += (s, e) => DeleteManufacturer(tab);
            buttonPanel.Controls.Add(deleteButton);

            Button refreshButton = new Button
            {
                Text = "🔄 Оновити",
                Location = new Point(340, 10),
                Width = 100,
                Height = 30
            };
            refreshButton.Click += (s, e) => LoadManufacturers(tab);
            buttonPanel.Controls.Add(refreshButton);

            tab.Controls.Add(buttonPanel);

            tabControl.TabPages.Add(tab);
            tabControl.SelectedTab = tab;

            LoadManufacturers(tab);
        }

        private void LoadManufacturers(TabPage tab)
        {
            DataGridView? grid = tab.Controls["manufacturersGrid"] as DataGridView;
            if (grid == null) return;

            var result = _dbService.ExecuteQuery("SELECT Id, Name, Country, IsActive FROM Manufacturers ORDER BY Name");

            var columnHeaders = new Dictionary<string, string>
            {
                { "Id", "№" },
                { "Name", "Назва" },
                { "Country", "Країна" },
                { "IsActive", "Активний" }
            };

            SetupDataGridView(grid, result, columnHeaders);
        }

        private void AddManufacturer(TabPage tab)
        {
            ManufacturerForm form = new ManufacturerForm();
            if (form.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string query = "INSERT INTO Manufacturers (Name, Country, IsActive) VALUES (@name, @country, 1)";
                    SqlParameter[] parameters =
                    {
                        new SqlParameter("@name", form.ItemName ?? ""),
                        new SqlParameter("@country", form.Country ?? "")
                    };

                    if (_dbService.ExecuteNonQueryWithParameters(query, parameters))
                    {
                        LogUserAction("CREATE", $"Manufacturer: {form.ItemName}");
                        MessageBox.Show("Запис додан!", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadManufacturers(tab);
                    }
                    else
                    {
                        LogUserAction("CREATE_FAILED", $"Manufacturer: {form.ItemName}");
                        MessageBox.Show("Помилка при додаванні!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogUserAction("CREATE_ERROR", $"Manufacturer - {ex.Message}");
                    MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void EditManufacturer(TabPage tab)
        {
            DataGridView? grid = tab.Controls["manufacturersGrid"] as DataGridView;
            if (grid == null || grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Виберіть запис!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int id = int.Parse(grid.SelectedRows[0].Cells["Id"].Value?.ToString() ?? "0");
            string name = grid.SelectedRows[0].Cells["Name"].Value?.ToString() ?? "";
            string country = grid.SelectedRows[0].Cells["Country"].Value?.ToString() ?? "";

            ManufacturerForm form = new ManufacturerForm(name, country);
            if (form.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string query = "UPDATE Manufacturers SET Name = @name, Country = @country WHERE Id = @id";
                    SqlParameter[] parameters =
                    {
                        new SqlParameter("@id", id),
                        new SqlParameter("@name", form.ItemName ?? ""),
                        new SqlParameter("@country", form.Country ?? "")
                    };

                    if (_dbService.ExecuteNonQueryWithParameters(query, parameters))
                    {
                        LogUserAction("UPDATE", $"Manufacturer ID={id}: {form.ItemName}");
                        MessageBox.Show("Запис оновлений!", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadManufacturers(tab);
                    }
                    else
                    {
                        LogUserAction("UPDATE_FAILED", $"Manufacturer ID={id}");
                        MessageBox.Show("Помилка при оновленні!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogUserAction("UPDATE_ERROR", $"Manufacturer ID={id} - {ex.Message}");
                    MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DeleteManufacturer(TabPage tab)
        {
            DataGridView? grid = tab.Controls["manufacturersGrid"] as DataGridView;
            if (grid == null || grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Виберіть запис!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("Видалити?", "Підтвердження", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                int id = int.Parse(grid.SelectedRows[0].Cells["Id"].Value?.ToString() ?? "0");
                string name = grid.SelectedRows[0].Cells["Name"].Value?.ToString() ?? "";
                try
                {
                    string query = "DELETE FROM Manufacturers WHERE Id = @id";
                    SqlParameter[] parameters = { new SqlParameter("@id", id) };

                    if (_dbService.ExecuteNonQueryWithParameters(query, parameters))
                    {
                        LogUserAction("DELETE", $"Manufacturer ID={id}: {name}");
                        MessageBox.Show("Видалено!", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadManufacturers(tab);
                    }
                    else
                    {
                        LogUserAction("DELETE_FAILED", $"Manufacturer ID={id}");
                        MessageBox.Show("Помилка при видаленні!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogUserAction("DELETE_ERROR", $"Manufacturer ID={id} - {ex.Message}");
                    MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

        #region Storage Locations

        // Вкладка «Місця зберігання» (довідник StorageLocations)
        private void ShowStorageLocationsTab()
        {
            const string tabName = "storage_locations_tab";
            TabPage? existingTab = tabControl.TabPages.Cast<TabPage>().FirstOrDefault(t => t.Name == tabName);
            if (existingTab != null)
            {
                tabControl.SelectedTab = existingTab;
                return;
            }

            TabPage tab = new TabPage("Місця зберігання")
            {
                Name = tabName,
                Padding = new Padding(0)
            };

            DataGridView grid = CreateGrid("storageLocationsGrid");
            tab.Controls.Add(grid);

            Panel buttonPanel = new Panel
            {
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = Color.LightGray
            };

            Button addButton = new Button
            {
                Text = "➕ Додати",
                Location = new Point(10, 10),
                Width = 100,
                Height = 30
            };
            addButton.Click += (s, e) => AddStorageLocation(tab);
            buttonPanel.Controls.Add(addButton);

            Button editButton = new Button
            {
                Text = "✏️ Редагувати",
                Location = new Point(120, 10),
                Width = 100,
                Height = 30
            };
            editButton.Click += (s, e) => EditStorageLocation(tab);
            buttonPanel.Controls.Add(editButton);

            Button deleteButton = new Button
            {
                Text = "🗑️ Видалити",
                Location = new Point(230, 10),
                Width = 100,
                Height = 30
            };
            deleteButton.Click += (s, e) => DeleteStorageLocation(tab);
            buttonPanel.Controls.Add(deleteButton);

            Button refreshButton = new Button
            {
                Text = "🔄 Оновити",
                Location = new Point(340, 10),
                Width = 100,
                Height = 30
            };
            refreshButton.Click += (s, e) => LoadStorageLocations(tab);
            buttonPanel.Controls.Add(refreshButton);

            tab.Controls.Add(buttonPanel);

            tabControl.TabPages.Add(tab);
            tabControl.SelectedTab = tab;

            LoadStorageLocations(tab);
        }

        private void LoadStorageLocations(TabPage tab)
        {
            DataGridView? grid = tab.Controls["storageLocationsGrid"] as DataGridView;
            if (grid == null) return;

            var result = _dbService.ExecuteQuery("SELECT Id, WarehouseName, Rack, Shelf, Description, IsActive FROM StorageLocations ORDER BY Id DESC");

            var columnHeaders = new Dictionary<string, string>
            {
                { "Id", "№" },
                { "WarehouseName", "Склад" },
                { "Rack", "Стелаж" },
                { "Shelf", "Полиця" },
                { "Description", "Опис" },
                { "IsActive", "Активний" }
            };

            SetupDataGridView(grid, result, columnHeaders);
        }

        private void AddStorageLocation(TabPage tab)
        {
            StorageLocationForm form = new StorageLocationForm();
            if (form.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string query = "INSERT INTO StorageLocations (WarehouseName, Rack, Shelf, Description, IsActive) VALUES (@warehouse, @rack, @shelf, @description, 1)";
                    SqlParameter[] parameters =
                    {
                        new SqlParameter("@warehouse", form.Warehouse ?? ""),
                        new SqlParameter("@rack", form.Rack ?? ""),
                        new SqlParameter("@shelf", form.Shelf ?? ""),
                        new SqlParameter("@description", form.Description ?? "")
                    };

                    if (_dbService.ExecuteNonQueryWithParameters(query, parameters))
                    {
                        LogUserAction("CREATE", $"StorageLocation: {form.Warehouse}-{form.Rack}-{form.Shelf}");
                        MessageBox.Show("Запис додан!", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadStorageLocations(tab);
                    }
                    else
                    {
                        LogUserAction("CREATE_FAILED", "StorageLocation");
                        MessageBox.Show("Помилка при додаванні!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogUserAction("CREATE_ERROR", $"StorageLocation - {ex.Message}");
                    MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void EditStorageLocation(TabPage tab)
        {
            DataGridView? grid = tab.Controls["storageLocationsGrid"] as DataGridView;
            if (grid == null || grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Виберіть запис!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int id = int.Parse(grid.SelectedRows[0].Cells["Id"].Value?.ToString() ?? "0");
            string warehouse = grid.SelectedRows[0].Cells["WarehouseName"].Value?.ToString() ?? "";
            string rack = grid.SelectedRows[0].Cells["Rack"].Value?.ToString() ?? "";
            string shelf = grid.SelectedRows[0].Cells["Shelf"].Value?.ToString() ?? "";
            string description = grid.SelectedRows[0].Cells["Description"].Value?.ToString() ?? "";

            StorageLocationForm form = new StorageLocationForm(warehouse, rack, shelf, description);
            if (form.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string query = "UPDATE StorageLocations SET WarehouseName = @warehouse, Rack = @rack, Shelf = @shelf, Description = @description WHERE Id = @id";
                    SqlParameter[] parameters =
                    {
                        new SqlParameter("@id", id),
                        new SqlParameter("@warehouse", form.Warehouse ?? ""),
                        new SqlParameter("@rack", form.Rack ?? ""),
                        new SqlParameter("@shelf", form.Shelf ?? ""),
                        new SqlParameter("@description", form.Description ?? "")
                    };

                    if (_dbService.ExecuteNonQueryWithParameters(query, parameters))
                    {
                        LogUserAction("UPDATE", $"StorageLocation ID={id}: {form.Warehouse}-{form.Rack}-{form.Shelf}");
                        MessageBox.Show("Запис оновлений!", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadStorageLocations(tab);
                    }
                    else
                    {
                        LogUserAction("UPDATE_FAILED", $"StorageLocation ID={id}");
                        MessageBox.Show("Помилка при оновленні!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogUserAction("UPDATE_ERROR", $"StorageLocation ID={id} - {ex.Message}");
                    MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DeleteStorageLocation(TabPage tab)
        {
            DataGridView? grid = tab.Controls["storageLocationsGrid"] as DataGridView;
            if (grid == null || grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Виберіть запис!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("Видалити?", "Підтвердження", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                int id = int.Parse(grid.SelectedRows[0].Cells["Id"].Value?.ToString() ?? "0");
                string location = grid.SelectedRows[0].Cells["WarehouseName"].Value?.ToString() ?? "";
                try
                {
                    string query = "DELETE FROM StorageLocations WHERE Id = @id";
                    SqlParameter[] parameters = { new SqlParameter("@id", id) };

                    if (_dbService.ExecuteNonQueryWithParameters(query, parameters))
                    {
                        LogUserAction("DELETE", $"StorageLocation ID={id}: {location}");
                        MessageBox.Show("Видалено!", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadStorageLocations(tab);
                    }
                    else
                    {
                        LogUserAction("DELETE_FAILED", $"StorageLocation ID={id}");
                        MessageBox.Show("Помилка при видаленні!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogUserAction("DELETE_ERROR", $"StorageLocation ID={id} - {ex.Message}");
                    MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

        #region Warehouse Items

        // Вкладка «Поточні залишки (Товари)» – аналітика складу за товарними позиціями
        private void ShowWarehouseItemsTab()
        {
            const string tabName = "warehouseItemsTab";
            TabPage? existingTab = tabControl.TabPages.Cast<TabPage>().FirstOrDefault(t => t.Name == tabName);
            if (existingTab != null)
            {
                tabControl.SelectedTab = existingTab;
                return;
            }

            TabPage warehouseTab = new TabPage("Поточні залишки (Товари)")
            {
                Name = tabName,
                Padding = new Padding(0)
            };

            DataGridView grid = CreateGrid("warehouseItemsGrid");
            warehouseTab.Controls.Add(grid);

            // Панель фільтрів для пошуку товарів за різними параметрами
            Panel buttonPanel = new Panel
            {
                Height = 110,
                Dock = DockStyle.Top,
                BackColor = Color.LightGray
            };

            Label articleLabel = new Label
            {
                Text = "Артикул:",
                Location = new Point(10, 10),
                Width = 80,
                Height = 20
            };
            buttonPanel.Controls.Add(articleLabel);

            TextBox articleBox = new TextBox
            {
                Name = "articleFilter",
                Location = new Point(95, 10),
                Width = 180,
                Height = 20
            };
            buttonPanel.Controls.Add(articleBox);

            Label equipmentLabel = new Label
            {
                Text = "Тип обл.:",
                Location = new Point(290, 10),
                Width = 80,
                Height = 20
            };
            buttonPanel.Controls.Add(equipmentLabel);

            ComboBox equipmentFilter = new ComboBox
            {
                Name = "equipmentFilter",
                Location = new Point(375, 10),
                Width = 180,
                Height = 20
            };
            LoadEquipmentTypesForFilter(equipmentFilter);
            buttonPanel.Controls.Add(equipmentFilter);

            Label manufacturerLabel = new Label
            {
                Text = "Виробник:",
                Location = new Point(570, 10),
                Width = 80,
                Height = 20
            };
            buttonPanel.Controls.Add(manufacturerLabel);

            ComboBox manufacturerFilter = new ComboBox
            {
                Name = "manufacturerFilter",
                Location = new Point(655, 10),
                Width = 180,
                Height = 20
            };
            LoadManufacturersForFilter(manufacturerFilter);
            buttonPanel.Controls.Add(manufacturerFilter);

            Label countryLabel = new Label
            {
                Text = "Країна:",
                Location = new Point(10, 40),
                Width = 80,
                Height = 20
            };
            buttonPanel.Controls.Add(countryLabel);

            ComboBox countryFilter = new ComboBox
            {
                Name = "countryFilter",
                Location = new Point(95, 40),
                Width = 180,
                Height = 20
            };
            LoadCountriesForFilter(countryFilter);
            buttonPanel.Controls.Add(countryFilter);

            Label locationLabel = new Label
            {
                Text = "Місце зб.:",
                Location = new Point(290, 40),
                Width = 80,
                Height = 20
            };
            buttonPanel.Controls.Add(locationLabel);

            ComboBox warehouseFilter = new ComboBox
            {
                Name = "warehouseFilter",
                Location = new Point(375, 40),
                Width = 58,
                Height = 20
            };
            LoadWarehousesForFilter(warehouseFilter);
            buttonPanel.Controls.Add(warehouseFilter);

            ComboBox rackFilter = new ComboBox
            {
                Name = "rackFilter",
                Location = new Point(436, 40),
                Width = 58,
                Height = 20
            };
            LoadRacksForFilter(rackFilter);
            buttonPanel.Controls.Add(rackFilter);

            ComboBox shelfFilter = new ComboBox
            {
                Name = "shelfFilter",
                Location = new Point(497, 40),
                Width = 58,
                Height = 20
            };
            LoadShelvesForFilter(shelfFilter);
            buttonPanel.Controls.Add(shelfFilter);

            Button filterButton = new Button
            {
                Text = "🔍 Фільтрувати",
                Location = new Point(10, 70),
                Width = 130,
                Height = 30,
                BackColor = Color.LightBlue
            };
            // Застосування фільтрів до набору товарів
            filterButton.Click += (s, e) => ApplyWarehouseFilter(warehouseTab, articleBox, equipmentFilter, manufacturerFilter, countryFilter, warehouseFilter, rackFilter, shelfFilter);
            buttonPanel.Controls.Add(filterButton);

            Button resetButton = new Button
            {
                Text = "✖️ Скинути",
                Location = new Point(150, 70),
                Width = 130,
                Height = 30,
                BackColor = Color.LightCoral
            };
            resetButton.Click += (s, e) => ResetWarehouseFilter(warehouseTab, articleBox, equipmentFilter, manufacturerFilter, countryFilter, warehouseFilter, rackFilter, shelfFilter);
            buttonPanel.Controls.Add(resetButton);

            Button refreshButton = new Button
            {
                Text = "🔄 Оновити",
                Location = new Point(290, 70),
                Width = 130,
                Height = 30,
                BackColor = Color.LightGreen
            };
            refreshButton.Click += (s, e) => LoadWarehouseItems(warehouseTab);
            buttonPanel.Controls.Add(refreshButton);

            warehouseTab.Controls.Add(buttonPanel);

            tabControl.TabPages.Add(warehouseTab);
            tabControl.SelectedTab = warehouseTab;

            LoadWarehouseItems(warehouseTab);
        }

        private void LoadEquipmentTypesForFilter(ComboBox box)
        {
            var result = _dbService.ExecuteQuery("SELECT Id, Name FROM EquipmentTypes WHERE IsActive = 1 ORDER BY Name");
            box.Items.Add(new FilterItem { Id = 0, Name = "(Усі)" });
            if (result != null)
            {
                foreach (DataRow row in result.Rows)
                {
                    int id = (int)row["Id"];
                    string name = row["Name"]?.ToString() ?? "";
                    box.Items.Add(new FilterItem { Id = id, Name = name });
                }
            }
            box.DisplayMember = "Name";
            box.ValueMember = "Id";
            box.SelectedIndex = 0;
        }

        private void LoadManufacturersForFilter(ComboBox box)
        {
            var result = _dbService.ExecuteQuery("SELECT Id, Name FROM Manufacturers WHERE IsActive = 1 ORDER BY Name");
            box.Items.Add(new FilterItem { Id = 0, Name = "(Усі)" });
            if (result != null)
            {
                foreach (DataRow row in result.Rows)
                {
                    int id = (int)row["Id"];
                    string name = row["Name"]?.ToString() ?? "";
                    box.Items.Add(new FilterItem { Id = id, Name = name });
                }
            }
            box.DisplayMember = "Name";
            box.ValueMember = "Id";
            box.SelectedIndex = 0;
        }

        private void LoadCountriesForFilter(ComboBox box)
        {
            var result = _dbService.ExecuteQuery("SELECT DISTINCT Country FROM Manufacturers WHERE IsActive = 1 AND Country IS NOT NULL ORDER BY Country");
            box.Items.Add("(Усі)");
            if (result != null)
            {
                foreach (DataRow row in result.Rows)
                {
                    string country = row["Country"]?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(country))
                    {
                        box.Items.Add(country);
                    }
                }
            }
            box.SelectedIndex = 0;
        }

        private void LoadLocationsForFilter(ComboBox box)
        {
            var result = _dbService.ExecuteQuery("SELECT Id, CONCAT(WarehouseName, '-', Rack, '-', Shelf) as Location FROM StorageLocations WHERE IsActive = 1 ORDER BY WarehouseName, Rack, Shelf");
            box.Items.Add(new FilterItem { Id = 0, Name = "(Усі)" });
            if (result != null)
            {
                foreach (DataRow row in result.Rows)
                {
                    int id = (int)row["Id"];
                    string location = row["Location"]?.ToString() ?? "";
                    box.Items.Add(new FilterItem { Id = id, Name = location });
                }
            }
            box.DisplayMember = "Name";
            box.ValueMember = "Id";
            box.SelectedIndex = 0;
        }

        // Формування параметризованого SQL‑запиту для фільтрації товарів на складі
        private void ApplyWarehouseFilter(TabPage tab, TextBox articleBox, ComboBox equipmentFilter, ComboBox manufacturerFilter, ComboBox countryFilter, ComboBox warehouseFilter, ComboBox rackFilter, ComboBox shelfFilter)
        {
            string article = articleBox.Text.Trim();
            int equipmentId = equipmentFilter.SelectedItem is FilterItem eq ? eq.Id : 0;
            int manufacturerId = manufacturerFilter.SelectedItem is FilterItem mfg ? mfg.Id : 0;
            string country = countryFilter.SelectedItem?.ToString() ?? "";
            string warehouse = warehouseFilter.SelectedItem?.ToString() ?? "";
            string rack = rackFilter.SelectedItem?.ToString() ?? "";
            string shelf = shelfFilter.SelectedItem?.ToString() ?? "";

            LogUserAction("FILTER", $"Warehouse: article={article}, equipment={equipmentId}, manufacturer={manufacturerId}, country={country}, warehouse={warehouse}, rack={rack}, shelf={shelf}");

            string query = @"
                SELECT 
                    wi.Id,
                    wi.Article,
                    et.Name as EquipmentType,
                    m.Name as ManufacturerName,
                    m.Country,
                    wi.Quantity,
                    wi.Unit,
                    wi.Power,
                    wi.Voltage,
                    CONCAT(sl.WarehouseName, '-', sl.Rack, '-', sl.Shelf) as Location,
                    wi.Notes
                FROM WarehouseItems wi
                LEFT JOIN EquipmentTypes et ON wi.EquipmentTypeId = et.Id
                LEFT JOIN Manufacturers m ON wi.ManufacturerId = m.Id
                LEFT JOIN StorageLocations sl ON wi.StorageLocationId = sl.Id
                WHERE 1=1";

            List<SqlParameter> parameters = new List<SqlParameter>();

            if (!string.IsNullOrWhiteSpace(article))
            {
                query += " AND wi.Article = @article";
                parameters.Add(new SqlParameter("@article", article));
            }

            if (equipmentId > 0)
            {
                query += " AND wi.EquipmentTypeId = @equipmentId";
                parameters.Add(new SqlParameter("@equipmentId", equipmentId));
            }

            if (manufacturerId > 0)
            {
                query += " AND wi.ManufacturerId = @manufacturerId";
                parameters.Add(new SqlParameter("@manufacturerId", manufacturerId));
            }

            if (!string.IsNullOrWhiteSpace(country) && country != "(Усі)")
            {
                query += " AND m.Country = @country";
                parameters.Add(new SqlParameter("@country", country));
            }

            if (!string.IsNullOrWhiteSpace(warehouse) && warehouse != "(Усі)")
            {
                query += " AND sl.WarehouseName = @warehouse";
                parameters.Add(new SqlParameter("@warehouse", warehouse));
            }

            if (!string.IsNullOrWhiteSpace(rack) && rack != "(Усі)")
            {
                query += " AND sl.Rack = @rack";
                parameters.Add(new SqlParameter("@rack", rack));
            }

            if (!string.IsNullOrWhiteSpace(shelf) && shelf != "(Усі)")
            {
                query += " AND sl.Shelf = @shelf";
                parameters.Add(new SqlParameter("@shelf", shelf));
            }

            query += " ORDER BY wi.Id DESC";

            DataTable? result = parameters.Count > 0
                ? _dbService.ExecuteQueryWithParameters(query, parameters.ToArray())
                : _dbService.ExecuteQuery(query);

            DataGridView? grid = tab.Controls["warehouseItemsGrid"] as DataGridView;
            if (grid != null)
            {
                var columnHeaders = new Dictionary<string, string>
                {
                    { "Id", "№" },
                    { "Article", "Артикул" },
                    { "EquipmentType", "Тип обладнання" },
                    { "ManufacturerName", "Виробник" },
                    { "Country", "Країна" },
                    { "Quantity", "Кількість" },
                    { "Unit", "Одиниця" },
                    { "Power", "Потужність" },
                    { "Voltage", "Напруга" },
                    { "Location", "Місце зберігання" },
                    { "Notes", "Примітки" }
                };

                SetupDataGridView(grid, result, columnHeaders);
            }
        }

        private void ResetWarehouseFilter(TabPage tab, TextBox articleBox, ComboBox equipmentFilter, ComboBox manufacturerFilter, ComboBox countryFilter, ComboBox warehouseFilter, ComboBox rackFilter, ComboBox shelfFilter)
        {
            articleBox.Clear();
            equipmentFilter.SelectedIndex = 0;
            manufacturerFilter.SelectedIndex = 0;
            countryFilter.SelectedIndex = 0;
            warehouseFilter.SelectedIndex = 0;
            rackFilter.SelectedIndex = 0;
            shelfFilter.SelectedIndex = 0;

            LoadWarehouseItems(tab);
        }

        private void ResetWarehouseFilter(TabPage tab, TextBox articleBox, ComboBox equipmentFilter, ComboBox manufacturerFilter, ComboBox countryFilter, ComboBox locationFilter)
        {
            articleBox.Clear();
            equipmentFilter.SelectedIndex = 0;
            manufacturerFilter.SelectedIndex = 0;
            countryFilter.SelectedIndex = 0;
            locationFilter.SelectedIndex = 0;

            LoadWarehouseItems(tab);
        }

        private void LoadWarehouseItems(TabPage tab)
        {
            DataGridView? grid = tab.Controls["warehouseItemsGrid"] as DataGridView;
            if (grid == null) return;

            string query = @"
                SELECT 
                    wi.Id,
                    wi.Article,
                    et.Name as EquipmentType,
                    m.Name as ManufacturerName,
                    m.Country,
                    wi.Quantity,
                    wi.Unit,
                    wi.Power,
                    wi.Voltage,
                    CONCAT(sl.WarehouseName, '-', sl.Rack, '-', sl.Shelf) as Location,
                    wi.Notes
                FROM WarehouseItems wi
                LEFT JOIN EquipmentTypes et ON wi.EquipmentTypeId = et.Id
                LEFT JOIN Manufacturers m ON wi.ManufacturerId = m.Id
                LEFT JOIN StorageLocations sl ON wi.StorageLocationId = sl.Id
                ORDER BY wi.Id DESC";

            var result = _dbService.ExecuteQuery(query);

            var columnHeaders = new Dictionary<string, string>
            {
                { "Id", "№" },
                { "Article", "Артикул" },
                { "EquipmentType", "Тип обладнання" },
                { "ManufacturerName", "Виробник" },
                { "Country", "Країна" },
                { "Quantity", "Кількість" },
                { "Unit", "Одиниця" },
                { "Power", "Потужність" },
                { "Voltage", "Напруга" },
                { "Location", "Місце зберігання" },
                { "Notes", "Примітки" }
            };

            SetupDataGridView(grid, result, columnHeaders);
        }

        #endregion

        #region Supplies

        // Вкладка «Поставки» – історія всіх рухів товару (надходження/списання)
        private void ShowSuppliesTab()
        {
            const string tabName = "supplies_tab";
            TabPage? existingTab = tabControl.TabPages.Cast<TabPage>().FirstOrDefault(t => t.Name == tabName);
            if (existingTab != null)
            {
                tabControl.SelectedTab = existingTab;
                return;
            }

            TabPage suppliesTab = new TabPage("Поставки")
            {
                Name = tabName,
                Padding = new Padding(0)
            };

            DataGridView grid = CreateGrid("suppliesGrid");
            suppliesTab.Controls.Add(grid);

            // Розширена панель фільтрів для аналітики по поставках
            Panel buttonPanel = new Panel
            {
                Height = 140,
                Dock = DockStyle.Top,
                BackColor = Color.LightGray
            };

            Label supplyTypeLabel = new Label
            {
                Text = "Тип пост.:",
                Location = new Point(10, 10),
                Width = 80,
                Height = 20
            };
            buttonPanel.Controls.Add(supplyTypeLabel);

            ComboBox supplyTypeFilter = new ComboBox
            {
                Name = "supplyTypeFilter",
                Location = new Point(90, 10),
                Width = 160,
                Height = 20
            };
            LoadSupplyTypesForFilter(supplyTypeFilter);
            buttonPanel.Controls.Add(supplyTypeFilter);

            Label createdByLabel = new Label
            {
                Text = "Хто додав:",
                Location = new Point(260, 10),
                Width = 85,
                Height = 20
            };
            buttonPanel.Controls.Add(createdByLabel);

            ComboBox createdByFilter = new ComboBox
            {
                Name = "createdByFilter",
                Location = new Point(350, 10),
                Width = 160,
                Height = 20
            };
            LoadCreatedByForFilter(createdByFilter);
            buttonPanel.Controls.Add(createdByFilter);

            Label dateFromLabel = new Label
            {
                Text = "Від:",
                Location = new Point(520, 10),
                Width = 75,
                Height = 20
            };
            buttonPanel.Controls.Add(dateFromLabel);

            DateTimePicker dateFromPicker = new DateTimePicker
            {
                Name = "dateFromPicker",
                Location = new Point(610, 10),
                Width = 160,
                Height = 20,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now.AddMonths(-1)
            };
            buttonPanel.Controls.Add(dateFromPicker);

            Label dateToLabel = new Label
            {
                Text = "До:",
                Location = new Point(780, 10),
                Width = 40,
                Height = 20
            };
            buttonPanel.Controls.Add(dateToLabel);

            DateTimePicker dateToPicker = new DateTimePicker
            {
                Name = "dateToPicker",
                Location = new Point(860, 10),
                Width = 160,
                Height = 20,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now
            };
            buttonPanel.Controls.Add(dateToPicker);

            Label articleLabel = new Label
            {
                Text = "Артикул:",
                Location = new Point(10, 40),
                Width = 75,
                Height = 20
            };
            buttonPanel.Controls.Add(articleLabel);

            TextBox articleBox = new TextBox
            {
                Name = "articleFilter",
                Location = new Point(90, 40),
                Width = 160,
                Height = 20
            };
            buttonPanel.Controls.Add(articleBox);

            Label equipmentLabel = new Label
            {
                Text = "Тип обл.:",
                Location = new Point(260, 40),
                Width = 75,
                Height = 20
            };
            buttonPanel.Controls.Add(equipmentLabel);

            ComboBox equipmentFilter = new ComboBox
            {
                Name = "equipmentFilter",
                Location = new Point(350, 40),
                Width = 160,
                Height = 20
            };
            LoadEquipmentTypesForFilter(equipmentFilter);
            buttonPanel.Controls.Add(equipmentFilter);

            Label manufacturerLabel = new Label
            {
                Text = "Виробник:",
                Location = new Point(520, 40),
                Width = 85,
                Height = 20
            };
            buttonPanel.Controls.Add(manufacturerLabel);

            ComboBox manufacturerFilter = new ComboBox
            {
                Name = "manufacturerFilter",
                Location = new Point(610, 40),
                Width = 160,
                Height = 20
            };
            LoadManufacturersForFilter(manufacturerFilter);
            buttonPanel.Controls.Add(manufacturerFilter);

            Label countryLabel = new Label
            {
                Text = "Країна:",
                Location = new Point(780, 40),
                Width = 75,
                Height = 20
            };
            buttonPanel.Controls.Add(countryLabel);

            ComboBox countryFilter = new ComboBox
            {
                Name = "countryFilter",
                Location = new Point(860, 40),
                Width = 160,
                Height = 20
            };
            LoadCountriesForFilter(countryFilter);
            buttonPanel.Controls.Add(countryFilter);

            Label locationLabel = new Label
            {
                Text = "Місце зб.:",
                Location = new Point(1030, 40),
                Width = 80,
                Height = 20
            };
            buttonPanel.Controls.Add(locationLabel);

            ComboBox warehouseFilter = new ComboBox
            {
                Name = "warehouseFilter",
                Location = new Point(1115, 40),
                Width = 52,
                Height = 20
            };
            LoadWarehousesForFilter(warehouseFilter);
            buttonPanel.Controls.Add(warehouseFilter);

            ComboBox rackFilter = new ComboBox
            {
                Name = "rackFilter",
                Location = new Point(1169, 40),
                Width = 52,
                Height = 20
            };
            LoadRacksForFilter(rackFilter);
            buttonPanel.Controls.Add(rackFilter);

            ComboBox shelfFilter = new ComboBox
            {
                Name = "shelfFilter",
                Location = new Point(1223, 40),
                Width = 52,
                Height = 20
            };
            LoadShelvesForFilter(shelfFilter);
            buttonPanel.Controls.Add(shelfFilter);

            TextBox supplierBox = new TextBox
            {
                Name = "supplierFilter",
                Visible = false
            };
            buttonPanel.Controls.Add(supplierBox);

            Button filterButton = new Button
            {
                Text = "🔍 Фільтрувати",
                Location = new Point(10, 70),
                Width = 130,
                Height = 30,
                BackColor = Color.LightBlue
            };
            // Застосування комплексних фільтрів до історії поставок
            filterButton.Click += (s, e) => ApplySuppliesFilter(suppliesTab, dateFromPicker, dateToPicker, articleBox, equipmentFilter, manufacturerFilter, countryFilter, warehouseFilter, rackFilter, shelfFilter, supplyTypeFilter, supplierBox, createdByFilter);
            buttonPanel.Controls.Add(filterButton);

            Button resetButton = new Button
            {
                Text = "✖️ Скинути",
                Location = new Point(150, 70),
                Width = 130,
                Height = 30,
                BackColor = Color.LightCoral
            };
            resetButton.Click += (s, e) => ResetSuppliesFilter(suppliesTab, dateFromPicker, dateToPicker, articleBox, equipmentFilter, manufacturerFilter, countryFilter, warehouseFilter, rackFilter, shelfFilter, supplyTypeFilter, supplierBox, createdByFilter);
            buttonPanel.Controls.Add(resetButton);

            Button refreshButton = new Button
            {
                Text = "🔄 Оновити",
                Location = new Point(290, 70),
                Width = 130,
                Height = 30,
                BackColor = Color.LightGreen
            };
            refreshButton.Click += (s, e) => LoadSupplies(suppliesTab);
            buttonPanel.Controls.Add(refreshButton);

            Button addButton = new Button
            {
                Text = "➕ Додати",
                Location = new Point(10, 105),
                Width = 130,
                Height = 30,
                BackColor = Color.LightGray
            };
            addButton.Click += (s, e) => AddSupply(suppliesTab, _userId ?? "");
            buttonPanel.Controls.Add(addButton);

            Button editButton = new Button
            {
                Text = "✏️ Редагувати",
                Location = new Point(150, 105),
                Width = 130,
                Height = 30,
                BackColor = Color.LightGray
            };
            editButton.Click += (s, e) => EditSupply(suppliesTab, _userId ?? "");
            buttonPanel.Controls.Add(editButton);

            Button deleteButton = new Button
            {
                Text = "🗑️ Видалити",
                Location = new Point(290, 105),
                Width = 130,
                Height = 30,
                BackColor = Color.LightGray
            };
            deleteButton.Click += (s, e) => DeleteSupply(suppliesTab);
            buttonPanel.Controls.Add(deleteButton);

            suppliesTab.Controls.Add(buttonPanel);

            tabControl.TabPages.Add(suppliesTab);
            tabControl.SelectedTab = suppliesTab;

            LoadSupplies(suppliesTab);
        }

        private void LoadWarehousesForFilter(ComboBox box)
        {
            var result = _dbService.ExecuteQuery("SELECT DISTINCT WarehouseName FROM StorageLocations WHERE IsActive = 1 ORDER BY WarehouseName");
            box.Items.Clear();
            box.Items.Add("(Усі)");
            if (result != null)
            {
                foreach (DataRow row in result.Rows)
                {
                    string warehouse = row["WarehouseName"]?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(warehouse))
                    {
                        box.Items.Add(warehouse);
                    }
                }
            }
            box.SelectedIndex = 0;
        }

        private void LoadRacksForFilter(ComboBox box)
        {
            var result = _dbService.ExecuteQuery("SELECT DISTINCT Rack FROM StorageLocations WHERE IsActive = 1 ORDER BY Rack");
            box.Items.Clear();
            box.Items.Add("(Усі)");
            if (result != null)
            {
                foreach (DataRow row in result.Rows)
                {
                    string rack = row["Rack"]?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(rack))
                    {
                        box.Items.Add(rack);
                    }
                }
            }
            box.SelectedIndex = 0;
        }

        private void LoadShelvesForFilter(ComboBox box)
        {
            var result = _dbService.ExecuteQuery("SELECT DISTINCT Shelf FROM StorageLocations WHERE IsActive = 1 ORDER BY Shelf");
            box.Items.Clear();
            box.Items.Add("(Усі)");
            if (result != null)
            {
                foreach (DataRow row in result.Rows)
                {
                    string shelf = row["Shelf"]?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(shelf))
                    {
                        box.Items.Add(shelf);
                    }
                }
            }
            box.SelectedIndex = 0;
        }

        private void LoadSupplyTypesForFilter(ComboBox box)
        {
            var result = _dbService.ExecuteQuery("SELECT Id, Description FROM SupplyTypes WHERE IsActive = 1 ORDER BY Description");
            box.Items.Add(new FilterItem { Id = 0, Name = "(Усі)" });
            if (result != null)
            {
                foreach (DataRow row in result.Rows)
                {
                    int id = (int)row["Id"];
                    string desc = row["Description"]?.ToString() ?? "";
                    box.Items.Add(new FilterItem { Id = id, Name = desc });
                }
            }
            box.DisplayMember = "Name";
            box.ValueMember = "Id";
            box.SelectedIndex = 0;
        }

        private void LoadCreatedByForFilter(ComboBox box)
        {
            var result = _dbService.ExecuteQuery("SELECT DISTINCT CreatedByUserLogin FROM Supplies WHERE CreatedByUserLogin IS NOT NULL ORDER BY CreatedByUserLogin");
            box.Items.Add("(Усі)");
            if (result != null)
            {
                foreach (DataRow row in result.Rows)
                {
                    string login = row["CreatedByUserLogin"]?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(login))
                    {
                        box.Items.Add(login);
                    }
                }
            }
            box.SelectedIndex = 0;
        }

        // Побудова SQL‑запиту для фільтрації історії поставок за багатьма параметрами
        private void ApplySuppliesFilter(TabPage tab, DateTimePicker dateFromPicker, DateTimePicker dateToPicker, TextBox articleBox, ComboBox equipmentFilter, ComboBox manufacturerFilter, ComboBox countryFilter, ComboBox warehouseFilter, ComboBox rackFilter, ComboBox shelfFilter, ComboBox supplyTypeFilter, TextBox supplierBox, ComboBox createdByFilter)
        {
            string article = articleBox.Text.Trim();
            int equipmentId = equipmentFilter.SelectedItem is FilterItem eq ? eq.Id : 0;
            int manufacturerId = manufacturerFilter.SelectedItem is FilterItem mfg ? mfg.Id : 0;
            string country = countryFilter.SelectedItem?.ToString() ?? "";
            string warehouse = warehouseFilter.SelectedItem?.ToString() ?? "";
            string rack = rackFilter.SelectedItem?.ToString() ?? "";
            string shelf = shelfFilter.SelectedItem?.ToString() ?? "";
            int supplyTypeId = supplyTypeFilter.SelectedItem is FilterItem st ? st.Id : 0;
            string createdBy = createdByFilter.SelectedItem?.ToString() ?? "";

            DateTime dateFrom = dateFromPicker.Value.Date;
            DateTime dateTo = dateToPicker.Value.Date;

            string query = @"
                SELECT 
                    s.Id,
                    wi.Article,
                    et.Name as EquipmentType,
                    m.Name as Manufacturer,
                    m.Country,
                    CONCAT(sl.WarehouseName, '-', sl.Rack, '-', sl.Shelf) as Location,
                    wi.Power,
                    wi.Voltage,
                    wi.Unit,
                    s.Quantity,
                    st.Description as SupplyType,
                    s.SupplyDate,
                    s.SupplierName,
                    s.CreatedByUserLogin,
                    s.UpdatedAt
                FROM Supplies s
                INNER JOIN WarehouseItems wi ON s.WarehouseItemId = wi.Id
                LEFT JOIN EquipmentTypes et ON wi.EquipmentTypeId = et.Id
                LEFT JOIN Manufacturers m ON wi.ManufacturerId = m.Id
                LEFT JOIN StorageLocations sl ON wi.StorageLocationId = sl.Id
                LEFT JOIN SupplyTypes st ON s.SupplyTypeId = st.Id
                WHERE 1=1";

            List<SqlParameter> parameters = new List<SqlParameter>();

            if (!string.IsNullOrWhiteSpace(article))
            {
                query += " AND wi.Article = @article";
                parameters.Add(new SqlParameter("@article", article));
            }

            if (equipmentId > 0)
            {
                query += " AND wi.EquipmentTypeId = @equipmentId";
                parameters.Add(new SqlParameter("@equipmentId", equipmentId));
            }

            if (manufacturerId > 0)
            {
                query += " AND wi.ManufacturerId = @manufacturerId";
                parameters.Add(new SqlParameter("@manufacturerId", manufacturerId));
            }

            if (!string.IsNullOrWhiteSpace(country) && country != "(Усі)")
            {
                query += " AND m.Country = @country";
                parameters.Add(new SqlParameter("@country", country));
            }

            if (!string.IsNullOrWhiteSpace(warehouse) && warehouse != "(Усі)")
            {
                query += " AND sl.WarehouseName = @warehouse";
                parameters.Add(new SqlParameter("@warehouse", warehouse));
            }

            if (!string.IsNullOrWhiteSpace(rack) && rack != "(Усі)")
            {
                query += " AND sl.Rack = @rack";
                parameters.Add(new SqlParameter("@rack", rack));
            }

            if (!string.IsNullOrWhiteSpace(shelf) && shelf != "(Усі)")
            {
                query += " AND sl.Shelf = @shelf";
                parameters.Add(new SqlParameter("@shelf", shelf));
            }

            if (supplyTypeId > 0)
            {
                query += " AND s.SupplyTypeId = @supplyTypeId";
                parameters.Add(new SqlParameter("@supplyTypeId", supplyTypeId));
            }

            if (!string.IsNullOrWhiteSpace(createdBy) && createdBy != "(Усі)")
            {
                query += " AND s.CreatedByUserLogin = @createdBy";
                parameters.Add(new SqlParameter("@createdBy", createdBy));
            }

            // Фільтрація по діапазону дат поставки
            query += " AND CAST(s.SupplyDate AS DATE) >= @dateFrom AND CAST(s.SupplyDate AS DATE) <= @dateTo";
            parameters.Add(new SqlParameter("@dateFrom", dateFrom));
            parameters.Add(new SqlParameter("@dateTo", dateTo));

            query += " ORDER BY s.SupplyDate DESC, s.Id DESC";

            DataTable? result = parameters.Count > 0
                ? _dbService.ExecuteQueryWithParameters(query, parameters.ToArray())
                : _dbService.ExecuteQuery(query);

            DataGridView? grid = tab.Controls["suppliesGrid"] as DataGridView;
            if (grid != null)
            {
                var columnHeaders = new Dictionary<string, string>
                {
                    { "Id", "№" },
                    { "Article", "Артикул" },
                    { "EquipmentType", "Тип обладнання" },
                    { "Manufacturer", "Виробник" },
                    { "Country", "Країна" },
                    { "Location", "Місце зберігання" },
                    { "Power", "Потужність" },
                    { "Voltage", "Напруга" },
                    { "Unit", "Одиниця" },
                    { "Quantity", "Кількість" },
                    { "SupplyType", "Тип поставки" },
                    { "SupplyDate", "Дата" },
                    { "SupplierName", "Постачальник" },
                    { "CreatedByUserLogin", "Хто додав" },
                    { "UpdatedAt", "Останнє оновлення" }
                };

                SetupDataGridView(grid, result, columnHeaders);
            }
        }

        private void ResetSuppliesFilter(TabPage tab, DateTimePicker dateFromPicker, DateTimePicker dateToPicker, TextBox articleBox, ComboBox equipmentFilter, ComboBox manufacturerFilter, ComboBox countryFilter, ComboBox warehouseFilter, ComboBox rackFilter, ComboBox shelfFilter, ComboBox supplyTypeFilter, TextBox supplierBox, ComboBox createdByFilter)
        {
            articleBox.Clear();
            equipmentFilter.SelectedIndex = 0;
            manufacturerFilter.SelectedIndex = 0;
            countryFilter.SelectedIndex = 0;
            warehouseFilter.SelectedIndex = 0;
            rackFilter.SelectedIndex = 0;
            shelfFilter.SelectedIndex = 0;
            supplyTypeFilter.SelectedIndex = 0;
            createdByFilter.SelectedIndex = 0;
            dateFromPicker.Value = DateTime.Now.AddMonths(-1);
            dateToPicker.Value = DateTime.Now;

            LoadSupplies(tab);
        }

        private void LoadSupplies(TabPage tab)
        {
            DataGridView? grid = tab.Controls["suppliesGrid"] as DataGridView;
            if (grid == null) return;

            string query = @"
                SELECT 
                    s.Id,
                    wi.Article,
                    et.Name as EquipmentType,
                    m.Name as Manufacturer,
                    m.Country,
                    CONCAT(sl.WarehouseName, '-', sl.Rack, '-', sl.Shelf) as Location,
                    wi.Power,
                    wi.Voltage,
                    wi.Unit,
                    s.Quantity,
                    st.Description as SupplyType,
                    s.SupplyDate,
                    s.SupplierName,
                    s.CreatedByUserLogin,
                    s.UpdatedAt
                FROM Supplies s
                INNER JOIN WarehouseItems wi ON s.WarehouseItemId = wi.Id
                LEFT JOIN EquipmentTypes et ON wi.EquipmentTypeId = et.Id
                LEFT JOIN Manufacturers m ON wi.ManufacturerId = m.Id
                LEFT JOIN StorageLocations sl ON wi.StorageLocationId = sl.Id
                LEFT JOIN SupplyTypes st ON s.SupplyTypeId = st.Id
                ORDER BY s.SupplyDate DESC, s.Id DESC";

            var result = _dbService.ExecuteQuery(query);

            var columnHeaders = new Dictionary<string, string>
            {
                { "Id", "№" },
                { "Article", "Артикул" },
                { "EquipmentType", "Тип обладнання" },
                { "Manufacturer", "Виробник" },
                { "Country", "Країна" },
                { "Location", "Місце зберігання" },
                { "Power", "Потужність" },
                { "Voltage", "Напруга" },
                { "Unit", "Одиниця" },
                { "Quantity", "Кількість" },
                { "SupplyType", "Тип поставки" },
                { "SupplyDate", "Дата" },
                { "SupplierName", "Постачальник" },
                { "CreatedByUserLogin", "Хто додав" },
                { "UpdatedAt", "Останнє оновлення" }
            };

            SetupDataGridView(grid, result, columnHeaders);
        }

        private void AddSupply(TabPage tab, string userLogin)
        {
            LogUserAction("CREATE_FORM_OPEN", "Supply");
            AddEditSupplyForm form = new AddEditSupplyForm(_dbService, userLogin);
            if (form.ShowDialog() == DialogResult.OK)
            {
                LogUserAction("CREATE", $"Supply by {userLogin}");
                LoadSupplies(tab);
            }
        }

        private void EditSupply(TabPage tab, string userLogin)
        {
            DataGridView? grid = tab.Controls["suppliesGrid"] as DataGridView;
            if (grid == null || grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Виберіть поставку для редагування!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            object cellValue = grid.SelectedRows[0].Cells["Id"].Value;
            int supplyId = int.Parse(cellValue?.ToString() ?? "0");

            LogUserAction("UPDATE_FORM_OPEN", $"Supply ID={supplyId}");
            AddEditSupplyForm form = new AddEditSupplyForm(_dbService, userLogin, supplyId);
            if (form.ShowDialog() == DialogResult.OK)
            {
                LogUserAction("UPDATE", $"Supply ID={supplyId} by {userLogin}");
                LoadSupplies(tab);
            }
        }

        private void DeleteSupply(TabPage tab)
        {
            DataGridView? grid = tab.Controls["suppliesGrid"] as DataGridView;
            if (grid == null || grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Виберіть поставку для видалення!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("Видалити цю поставку?", "Підтвердження", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                object cellValue = grid.SelectedRows[0].Cells["Id"].Value;
                int supplyId = int.Parse(cellValue?.ToString() ?? "0");

                try
                {
                    // Знаходимо пов'язаний запис WarehouseItems, щоб оновити його кількість після видалення поставки
                    string getItemQuery = "SELECT WarehouseItemId FROM Supplies WHERE Id = @supplyId";
                    SqlParameter[] getParams = { new SqlParameter("@supplyId", supplyId) };
                    var itemResult = _dbService.ExecuteQueryWithParameters(getItemQuery, getParams);
                    int warehouseItemId = 0;

                    if (itemResult != null && itemResult.Rows.Count > 0)
                    {
                        object itemId = itemResult.Rows[0]["WarehouseItemId"];
                        if (itemId != null && int.TryParse(itemId.ToString(), out int id))
                        {
                            warehouseItemId = id;
                        }
                    }

                    string deleteQuery = "DELETE FROM Supplies WHERE Id = @supplyId";
                    SqlParameter[] deleteParams = { new SqlParameter("@supplyId", supplyId) };
                    _dbService.ExecuteNonQueryWithParameters(deleteQuery, deleteParams);

                    // Після видалення перераховуємо кількість товару на складі
                    string updateQuantityQuery = @"
                        UPDATE WarehouseItems
                        SET Quantity = COALESCE((SELECT SUM(Quantity) FROM Supplies WHERE WarehouseItemId = @warehouseItemId), 0)
                        WHERE Id = @warehouseItemId";

                    SqlParameter[] updateParams = { new SqlParameter("@warehouseItemId", warehouseItemId) };
                    _dbService.ExecuteNonQueryWithParameters(updateQuantityQuery, updateParams);

                    LogUserAction("DELETE", $"Supply ID={supplyId}");
                    MessageBox.Show("Поставка видалена!", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadSupplies(tab);
                }
                catch (Exception ex)
                {
                    LogUserAction("DELETE_ERROR", $"Supply ID={supplyId} - {ex.Message}");
                    MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

        #region SQL Console

        // Вкладка «SQL‑консоль» – інтерактивне середовище виконання SQL‑запитів
        private void ShowSQLConsoleTab()
        {
            const string tabName = "sqlConsole_tab";
            TabPage? existingTab = tabControl.TabPages.Cast<TabPage>().FirstOrDefault(t => t.Name == tabName);
            if (existingTab != null)
            {
                tabControl.SelectedTab = existingTab;
                return;
            }

            TabPage sqlTab = new TabPage("SQL-консоль")
            {
                Name = tabName,
                Padding = new Padding(0),
                BackColor = Color.Black
            };

            RichTextBox richTextBox = new RichTextBox
            {
                Name = "richTextBoxConsole",
                Dock = DockStyle.Fill,
                Font = new Font("Courier New", 11),
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                AcceptsTab = false,
                WordWrap = true,
                BorderStyle = BorderStyle.None
            };

            // Обробка Enter / Shift+Enter для виконання запиту або переходу на новий рядок
            richTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Return && !e.Shift && !e.Control)
                {
                    ExecuteSQLCommand(sqlTab);
                    e.Handled = true;
                }
                else if (e.Shift && e.KeyCode == Keys.Return)
                {
                    richTextBox.SelectedText = "\n";
                    e.Handled = true;
                }
            };

            // Захист від видалення запрошення консольного вводу ("> ")
            richTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete)
                {
                    int promptIndex = richTextBox.Text.LastIndexOf("> ");
                    int promptEnd = promptIndex + 2;

                    if (richTextBox.SelectionStart <= promptEnd)
                    {
                        e.Handled = true;
                    }
                }
            };

            // Заборона введення тексту перед останнім запрошенням
            richTextBox.KeyPress += (s, e) =>
            {
                int promptIndex = richTextBox.Text.LastIndexOf("> ");
                int promptEnd = promptIndex + 2;

                if (richTextBox.SelectionStart < promptEnd && !char.IsControl(e.KeyChar))
                {
                    richTextBox.SelectionStart = richTextBox.Text.Length;
                }
            };

            // Початковий текст-підказка в консолі
            richTextBox.Text = @"
╔════════════════════════════════════════════════════════════════════╗
║           SQL-КОНСОЛЬ - Solar Warehouse v1.0           ║
║                                                        ║
║ Команди:                                               ║
║ • Enter         - Виконати SQL запит                   ║
║ • Shift+Enter   - Нова строка                          ║
║ • /clear         - Очистити консоль                    ║
║                                                        ║
║ ВНИМАНИЕ: Будьте обережні з командами DELETE і DROP!   ║
╚════════════════════════════════════════════════════════════════════╝



> ";

            sqlTab.Controls.Add(richTextBox);
            tabControl.TabPages.Add(sqlTab);
            tabControl.SelectedTab = sqlTab;
        }

        private void ExecuteSQLCommand(TabPage sqlTab)
        {
            RichTextBox? richTextBox = sqlTab.Controls.OfType<RichTextBox>().FirstOrDefault(r => r.Name == "richTextBoxConsole");
            if (richTextBox == null) return;

            string fullText = richTextBox.Text;

            int lastPromptIndex = fullText.LastIndexOf("> ");
            if (lastPromptIndex == -1) return;

            string commandText = fullText[(lastPromptIndex + 2)..].Trim();

            if (string.IsNullOrWhiteSpace(commandText))
            {
                richTextBox.AppendText("\n> ");
                richTextBox.SelectionStart = richTextBox.Text.Length;
                richTextBox.ScrollToCaret();
                return;
            }

            // Спеціальна команда очищення консолі
            if (commandText.ToLower() == "/clear")
            {
                LogUserAction("SQL_CLEAR", "SQL Console cleared");
                richTextBox.Clear();
                richTextBox.Text = @"
╔════════════════════════════════════════════════════════════════════╗
║           SQL-КОНСОЛЬ - Solar Warehouse v1.0           ║
║                                                        ║
║ Команди:                                               ║
║ • Enter         - Виконати SQL запит                   ║
║ • Shift+Enter   - Нова строка                          ║
║ • /clear         - Очистити консоль                    ║
║                                                        ║
║   УВАГА: Будьте обережні з командами DELETE та DROP!   ║
╚════════════════════════════════════════════════════════════════════╝



> ";
                richTextBox.SelectionStart = richTextBox.Text.Length;
                richTextBox.ScrollToCaret();
                return;
            }

            try
            {
                LogUserAction("SQL_EXECUTE", commandText[..Math.Min(100, commandText.Length)]);
                var result = _dbService.ExecuteQuery(commandText);

                richTextBox.AppendText("\n");

                if (result == null)
                {
                    richTextBox.AppendText("ERROR: Command_returned_no_result\n");
                }
                else if (result.Rows.Count == 0)
                {
                    richTextBox.AppendText("OK: Command_executed_(0_rows)\n");
                }
                else
                {
                    richTextBox.AppendText($"OK: {result.Rows.Count}_rows\n");
                    richTextBox.AppendText("─────────────────────────────────────\n");

                    foreach (DataColumn column in result.Columns)
                    {
                        richTextBox.AppendText($"{column.ColumnName,-25}");
                    }
                    richTextBox.AppendText("\n");
                    richTextBox.AppendText("─────────────────────────────────────\n");

                    foreach (DataRow row in result.Rows)
                    {
                        foreach (var item in row.ItemArray)
                        {
                            string value = item?.ToString() ?? "NULL";
                            richTextBox.AppendText($"{value,-25}");
                        }
                        richTextBox.AppendText("\n");
                    }
                }
            }
            catch (Exception ex)
            {
                LogUserAction("SQL_ERROR", ex.Message);
                richTextBox.AppendText($"ERROR: {ex.Message}\n");
            }

            richTextBox.AppendText("\n> ");
            richTextBox.SelectionStart = richTextBox.Text.Length;
            richTextBox.ScrollToCaret();
        }

        #endregion

        #region Backup/Restore

        // Вкладка «Резервні копії бази даних» – створення, перегляд, відновлення бекапів
        private void ShowBackupRestoreTab()
        {
            const string tabName = "backupRestore_tab";
            TabPage? existingTab = tabControl.TabPages.Cast<TabPage>().FirstOrDefault(t => t.Name == tabName);
            if (existingTab != null)
            {
                tabControl.SelectedTab = existingTab;
                return;
            }

            TabPage brTab = new TabPage("Резервні копії бази даних")
            {
                Name = tabName,
                Padding = new Padding(0),
                BackColor = SystemColors.Control
            };

            Panel controlPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(brTab.Width, 105),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.WhiteSmoke,
                Padding = new Padding(10, 10, 10, 8),
                BorderStyle = BorderStyle.FixedSingle
            };

            int btnWidth = 170;
            int btnHeight = 36;
            int spacing = 6;
            int startX = 10;
            int topY = 10;
            int bottomY = 56;

            Button backupBtn = new Button
            {
                Text = "💾 Резервна копія",
                Width = btnWidth,
                Height = btnHeight,
                Location = new Point(startX, topY),
                BackColor = Color.LightGreen,
                FlatStyle = FlatStyle.Standard,
                Font = new Font("Arial", 9),
                TextAlign = ContentAlignment.MiddleCenter
            };
            backupBtn.Click += (s, e) => CreateBackupNow(brTab);
            controlPanel.Controls.Add(backupBtn);

            Button restoreBtn = new Button
            {
                Text = "♻️ Відновити",
                Width = btnWidth,
                Height = btnHeight,
                Location = new Point(startX + btnWidth + spacing, topY),
                BackColor = Color.LightBlue,
                FlatStyle = FlatStyle.Standard,
                Font = new Font("Arial", 9),
                TextAlign = ContentAlignment.MiddleCenter
            };
            restoreBtn.Click += (s, e) =>
            {
                DataGridView? gridRestore = brTab.Controls["backupGrid"] as DataGridView;
                if (gridRestore == null || gridRestore.SelectedRows.Count == 0)
                {
                    MessageBox.Show("Виберіть файл бекапу!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                DataGridViewRow selectedRow = gridRestore.SelectedRows[0];
                object? typeObj = selectedRow.Cells["Тип"].Value;

                if (typeObj?.ToString() != "file")
                {
                    MessageBox.Show("Виберіть файл, а не папку!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                object? pathObj = selectedRow.Cells["Шлях"].Value;
                string? filePath = pathObj?.ToString();

                if (!string.IsNullOrEmpty(filePath))
                {
                    DialogResult result = MessageBox.Show(
                        $"Відновити з цього бекапу?\n{Path.GetFileName(filePath)}",
                        "Підтвердження",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );

                    if (result == DialogResult.Yes)
                    {
                        RestoreBackupFromFile(filePath);
                    }
                }
            };
            controlPanel.Controls.Add(restoreBtn);

            Button folderBtn = new Button
            {
                Text = "📁 Папка",
                Width = btnWidth,
                Height = btnHeight,
                Location = new Point(startX + (btnWidth + spacing) * 2, topY),
                BackColor = Color.LightYellow,
                FlatStyle = FlatStyle.Standard,
                Font = new Font("Arial", 9),
                TextAlign = ContentAlignment.MiddleCenter
            };
            folderBtn.Click += (s, e) => OpenBackupFolder();
            controlPanel.Controls.Add(folderBtn);

            Button refreshBtn = new Button
            {
                Text = "🔄 Оновити",
                Width = btnWidth,
                Height = btnHeight,
                Location = new Point(startX + (btnWidth + spacing) * 3, topY),
                BackColor = Color.LightCyan,
                FlatStyle = FlatStyle.Standard,
                Font = new Font("Arial", 9),
                TextAlign = ContentAlignment.MiddleCenter
            };
            refreshBtn.Click += (s, e) => LoadBackupList(brTab);
            controlPanel.Controls.Add(refreshBtn);

            Label autoLabel = new Label
            {
                Text = "Автоматичні бекапи (хвилини):",
                Width = 210,
                Height = 25,
                Location = new Point(startX, bottomY),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Arial", 10)
            };
            controlPanel.Controls.Add(autoLabel);

            TextBox intervalBox = new TextBox
            {
                Name = "autoBackupIntervalTextBox",
                Text = LoadAutoBackupInterval().ToString(),
                Width = 50,
                Height = 26,
                Location = new Point(startX + 220, bottomY + 1),
                TextAlign = HorizontalAlignment.Center,
                Font = new Font("Arial", 10)
            };
            controlPanel.Controls.Add(intervalBox);

            Button saveBtn = new Button
            {
                Text = "💾 Зберегти",
                Width = 115,
                Height = 26,
                Location = new Point(startX + 280, bottomY + 1),
                BackColor = Color.LightGreen,
                FlatStyle = FlatStyle.Standard,
                Font = new Font("Arial", 9),
                TextAlign = ContentAlignment.MiddleCenter
            };
            saveBtn.Click += (s, e) =>
            {
                if (int.TryParse(intervalBox.Text, out int interval) && interval > 0)
                {
                    SaveAutoBackupInterval(interval);
                    RestartAutoBackupTimer(interval);
                    MessageBox.Show($"Інтервал змінено на {interval} хвилин!", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Введіть число > 0!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            controlPanel.Controls.Add(saveBtn);

            brTab.Controls.Add(controlPanel);

            DataGridView grid = new DataGridView
            {
                Name = "backupGrid",
                Location = new Point(5, 110),
                Size = new Size(brTab.Width - 10, brTab.Height - 115),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true,
                BackgroundColor = Color.White,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BorderStyle = BorderStyle.FixedSingle,
                ColumnHeadersHeight = 30,
                RowTemplate = { Height = 28 }
            };
            brTab.Controls.Add(grid);

            tabControl.TabPages.Add(brTab);
            tabControl.SelectedTab = brTab;

            LoadBackupList(brTab);

            DataGridView? gridCtrl = brTab.Controls["backupGrid"] as DataGridView;
            if (gridCtrl != null)
            {
                gridCtrl.CellDoubleClick += (s, e) =>
                {
                    if (e.RowIndex < 0 || e.RowIndex >= gridCtrl.Rows.Count) return;

                    object? typeObj = gridCtrl.Rows[e.RowIndex].Cells["Тип"].Value;
                    string? fileType = typeObj?.ToString();

                    if (fileType == "folder")
                    {
                        string? cellText = gridCtrl.Rows[e.RowIndex].Cells[0].Value?.ToString();

                        if (cellText != null)
                        {
                            int currentRow = e.RowIndex;
                            bool isOpen = cellText.StartsWith("📂");

                            for (int i = currentRow + 1; i < gridCtrl.Rows.Count; i++)
                            {
                                object? nextType = gridCtrl.Rows[i].Cells["Тип"].Value;
                                string? nextTypeStr = nextType?.ToString();

                                if (nextTypeStr == "folder")
                                {
                                    break;
                                }

                                if (nextTypeStr == "file")
                                {
                                    gridCtrl.Rows[i].Visible = !isOpen;
                                }
                            }

                            string folderName = cellText.Replace("📁", "").Replace("📂", "").Trim();
                            string newIcon = isOpen ? "📁" : "📂";
                            gridCtrl.Rows[currentRow].Cells[0].Value = $"{newIcon} {folderName}";
                        }
                    }
                    else if (fileType == "file")
                    {
                        object? pathObj = gridCtrl.Rows[e.RowIndex].Cells["Шлях"].Value;
                        string? filePath = pathObj?.ToString();

                        if (!string.IsNullOrEmpty(filePath))
                        {
                            DialogResult result = MessageBox.Show(
                                $"Відновити з цього бекапу?\n{Path.GetFileName(filePath)}",
                                "Підтвердження",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question
                            );

                            if (result == DialogResult.Yes)
                            {
                                RestoreBackupFromFile(filePath);
                            }
                        }
                    }
                };
            }
        }

        private void LoadBackupList(TabPage tab)
        {
            DataGridView? grid = tab.Controls["backupGrid"] as DataGridView;
            if (grid == null) return;

            string backupDir = GetBackupBaseDirectory();

            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
                return;
            }

            grid.Columns.Clear();
            grid.Rows.Clear();

            grid.Columns.Add("Папка/Файл", "Папка/Файл");
            grid.Columns.Add("Розмір", "Розмір");
            grid.Columns.Add("Час", "Час");
            grid.Columns.Add("Шлях", "Шлях");
            grid.Columns.Add("Тип", "Тип");
            grid.Columns["Шлях"].Visible = false;
            grid.Columns["Тип"].Visible = false;

            try
            {
                DirectoryInfo backupDirInfo = new DirectoryInfo(backupDir);
                DirectoryInfo[] dateFolders = backupDirInfo.GetDirectories().OrderByDescending(d => d.Name).ToArray();

                foreach (DirectoryInfo dateFolder in dateFolders)
                {
                    int folderRowIndex = grid.Rows.Add(
                        $"📁 {dateFolder.Name}",
                        "",
                        "",
                        dateFolder.FullName,
                        "folder"
                    );

                    grid.Rows[folderRowIndex].DefaultCellStyle.BackColor = Color.LightGray;
                    grid.Rows[folderRowIndex].DefaultCellStyle.Font = new Font("Arial", 10, FontStyle.Bold);

                    FileInfo[] backupFiles = dateFolder.GetFiles("*.bak").OrderByDescending(f => f.CreationTime).ToArray();

                    foreach (FileInfo file in backupFiles)
                    {
                        long sizeBytes = file.Length;
                        string sizeStr = sizeBytes > 1024 * 1024
                            ? $"{sizeBytes / (1024 * 1024)} МБ"
                            : $"{sizeBytes / 1024} КБ";

                        int fileRowIndex = grid.Rows.Add(
                            $"    📄 {file.Name}",
                            sizeStr,
                            file.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            file.FullName,
                            "file"
                        );

                        grid.Rows[fileRowIndex].DefaultCellStyle.BackColor = Color.White;
                        grid.Rows[fileRowIndex].DefaultCellStyle.Font = new Font("Arial", 9);
                        grid.Rows[fileRowIndex].Visible = false;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка:\n{ex.Message}", "Помилка");
            }
        }

        private void CreateBackupNow(TabPage brTab)
        {
            try
            {
                string backupPath = GetBackupFilePath();
                string? backupDir = Path.GetDirectoryName(backupPath);

                if (backupDir != null && !Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                string backupQuery = $"BACKUP DATABASE [SolarWarehouseDB] TO DISK = '{backupPath}'";

                if (_dbService.ExecuteNonQuery(backupQuery))
                {
                    LogUserAction("BACKUP_CREATE", backupPath);
                    MessageBox.Show($"Резервна копія успішно створена!\n\n{backupPath}", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadBackupList(brTab);
                }
                else
                {
                    LogUserAction("BACKUP_CREATE_FAILED", backupPath);
                    MessageBox.Show("Помилка при створенні резервної копії!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                LogUserAction("BACKUP_CREATE_ERROR", ex.Message);
                MessageBox.Show($"Помилка:\n{ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CreateBackupAuto(TabPage? brTab)
        {
            try
            {
                string backupPath = GetBackupFilePath();
                string? backupDir = Path.GetDirectoryName(backupPath);

                if (backupDir != null && !Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                string backupQuery = $"BACKUP DATABASE [SolarWarehouseDB] TO DISK = '{backupPath}'";
                _dbService.ExecuteNonQuery(backupQuery);

                if (brTab != null)
                {
                    LoadBackupList(brTab);
                }
            }
            catch
            {
            }
        }

        private void RestoreBackupFromFile(string backupFilePath)
        {
            try
            {
                if (!File.Exists(backupFilePath))
                {
                    LogUserAction("BACKUP_RESTORE_FAILED", $"File not found: {backupFilePath}");
                    MessageBox.Show($"Файл не знайдено:\n{backupFilePath}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string restoreQuery = @"
                    USE master
                    ALTER DATABASE [SolarWarehouseDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE
                    RESTORE DATABASE [SolarWarehouseDB] FROM DISK = @backupPath WITH REPLACE
                    ALTER DATABASE [SolarWarehouseDB] SET MULTI_USER
                ";

                SqlParameter[] parameters = { new SqlParameter("@backupPath", backupFilePath) };

                if (_dbService.ExecuteNonQueryWithParameters(restoreQuery, parameters))
                {
                    LogUserAction("BACKUP_RESTORE", backupFilePath);
                    MessageBox.Show("База даних успішно відновлена!", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    LogUserAction("BACKUP_RESTORE_FAILED", backupFilePath);
                    MessageBox.Show("Помилка при відновленні бази даних!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                LogUserAction("BACKUP_RESTORE_ERROR", ex.Message);
                MessageBox.Show($"Помилка:\n{ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetBackupFilePath()
        {
            string baseDir = GetBackupBaseDirectory();
            string dateFolder = DateTime.Now.ToString("dd-MM-yyyy");
            string dateFolderPath = Path.Combine(baseDir, dateFolder);

            if (!Directory.Exists(dateFolderPath))
            {
                Directory.CreateDirectory(dateFolderPath);
            }

            string fileName = $"SolarWarehouse_backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.bak";
            return Path.Combine(dateFolderPath, fileName);
        }

        // Базова тека для збереження всіх резервних копій – поруч із .exe
        private string GetBackupBaseDirectory()
        {
            string exePath = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(exePath, "Backups");
        }

        // Файл конфігурації інтервалу авто‑бекапу – лежить прямо біля .exe, з розширенням .cfg
        private string GetBackupConfigPath()
        {
            string exePath = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(exePath, "backup_config.cfg");
        }

        private void OpenBackupFolder()
        {
            string backupDir = GetBackupBaseDirectory();
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = backupDir,
                UseShellExecute = true
            });
        }

        private int LoadAutoBackupInterval()
        {
            try
            {
                string configPath = GetBackupConfigPath();

                if (File.Exists(configPath))
                {
                    string content = File.ReadAllText(configPath).Trim();
                    if (int.TryParse(content, out int interval) && interval > 0)
                    {
                        return interval;
                    }
                }
            }
            catch
            {
            }

            // Значення за замовчуванням – 10 хвилин
            return 10;
        }

        private void SaveAutoBackupInterval(int minutes)
        {
            try
            {
                string configPath = GetBackupConfigPath();

                File.WriteAllText(configPath, minutes.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при збереженні налаштувань:\n{ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestartAutoBackupTimer(int intervalMinutes)
        {
            backupTimer?.Dispose();
            backupTimer = null;

            backupTimer = new System.Threading.Timer(
                callback: _ => CreateBackupAuto(null),
                state: null,
                dueTime: TimeSpan.FromMinutes(intervalMinutes),
                period: TimeSpan.FromMinutes(intervalMinutes)
            );
        }

        // Базова тека для файлових логів (використовується LogService) – також поруч із .exe
        private string GetLogsBaseDirectory()
        {
            string exePath = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(exePath, "logs");
        }

        #endregion

        #region Database Structure

        // Вкладка «Структура БД» – візуалізація структури бази даних та моніторинг
        private void ShowDatabaseStructureTab()
        {
            const string tabName = "dbStructure_tab";
            TabPage? existingTab = tabControl.TabPages.Cast<TabPage>().FirstOrDefault(t => t.Name == tabName);
            if (existingTab != null)
            {
                tabControl.SelectedTab = existingTab;
                return;
            }

            TabPage dbTab = new TabPage("Структура БД")
            {
                Name = tabName,
                Padding = new Padding(0),
                BackColor = Color.White
            };

            // Ліва панель з деревом таблиць та полів
            Panel leftPanel = new Panel
            {
                Name = "leftPanel",
                Location = new Point(0, 0),
                Size = new Size(300, dbTab.Height),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };

            TreeView treeView = new TreeView
            {
                Name = "dbStructureTree",
                Dock = DockStyle.Fill,
                Font = new Font("Arial", 10)
            };
            treeView.NodeMouseClick += (s, e) => LoadTableDetails(dbTab, e.Node);
            leftPanel.Controls.Add(treeView);
            dbTab.Controls.Add(leftPanel);

            Panel splitter = new Panel
            {
                Location = new Point(303, 0),
                Size = new Size(3, dbTab.Height),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom,
                BackColor = Color.DarkGray
            };
            dbTab.Controls.Add(splitter);

            // Права панель з детальною інформацією по вибраній таблиці
            Panel rightPanel = new Panel
            {
                Name = "rightPanel",
                Location = new Point(306, 0),
                Size = new Size(dbTab.Width - 306, dbTab.Height),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right,
                BackColor = Color.White,
                Padding = new Padding(0)
            };

            Label tableNameLabel = new Label
            {
                Name = "tableNameLabel",
                Text = "Виберіть таблицю зліва",
                Font = new Font("Arial", 13, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(60, 120, 180),
                Location = new Point(0, 0),
                Size = new Size(rightPanel.Width, 40),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };
            rightPanel.Controls.Add(tableNameLabel);

            TabControl detailsTabControl = new TabControl
            {
                Name = "detailsTabControl",
                Location = new Point(0, 40),
                Size = new Size(rightPanel.Width, rightPanel.Height - 40),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right,
                Font = new Font("Arial", 10)
            };

            // Вкладка з переліком полів таблиці
            TabPage columnsTab = new TabPage("📄 Поля");
            DataGridView columnsGrid = new DataGridView
            {
                Name = "columnsGrid",
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                ReadOnly = true,
                AllowUserToAddRows = false,
                BackgroundColor = Color.White
            };
            columnsTab.Controls.Add(columnsGrid);
            detailsTabControl.TabPages.Add(columnsTab);

            // Вкладка з індексами
            TabPage indexesTab = new TabPage("🔍 Індекси");
            DataGridView indexesGrid = new DataGridView
            {
                Name = "indexesGrid",
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                ReadOnly = true,
                AllowUserToAddRows = false,
                BackgroundColor = Color.White
            };
            indexesTab.Controls.Add(indexesGrid);
            detailsTabControl.TabPages.Add(indexesTab);

            // Вкладка зі зв'язками (FOREIGN KEY)
            TabPage relationsTab = new TabPage("🔗 Зв'язки");
            DataGridView relationsGrid = new DataGridView
            {
                Name = "relationsGrid",
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                ReadOnly = true,
                AllowUserToAddRows = false,
                BackgroundColor = Color.White
            };
            relationsTab.Controls.Add(relationsGrid);
            detailsTabControl.TabPages.Add(relationsTab);

            // Вкладка з тригерами
            TabPage triggersTab = new TabPage("⚡ Тригери");
            DataGridView triggersGrid = new DataGridView
            {
                Name = "triggersGrid",
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                ReadOnly = true,
                AllowUserToAddRows = false,
                BackgroundColor = Color.White
            };
            triggersTab.Controls.Add(triggersGrid);
            detailsTabControl.TabPages.Add(triggersTab);

            // Вкладка з check‑обмеженнями
            TabPage constraintsTab = new TabPage("🔐 Обмеження");
            DataGridView constraintsGrid = new DataGridView
            {
                Name = "constraintsGrid",
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                ReadOnly = true,
                AllowUserToAddRows = false,
                BackgroundColor = Color.White
            };
            constraintsTab.Controls.Add(constraintsGrid);
            detailsTabControl.TabPages.Add(constraintsTab);

            // Вкладка «Моніторинг» – узагальнена статистика по таблиці
            TabPage statsTab = new TabPage("📊 Моніторинг");
            RichTextBox statsBox = new RichTextBox
            {
                Name = "statsBox",
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(245, 245, 245),
                Font = new Font("Courier New", 11)
            };
            statsTab.Controls.Add(statsBox);
            detailsTabControl.TabPages.Add(statsTab);

            rightPanel.Controls.Add(detailsTabControl);
            dbTab.Controls.Add(rightPanel);

            tabControl.TabPages.Add(dbTab);
            tabControl.SelectedTab = dbTab;

            LoadDatabaseStructure(dbTab);
        }

        // Завантаження структури БД у дерево: таблиця -> поля
        private void LoadDatabaseStructure(TabPage dbTab)
        {
            Panel? leftPanel = dbTab.Controls.OfType<Panel>().FirstOrDefault(p => p.Name == "leftPanel");
            if (leftPanel == null) return;

            TreeView? treeView = leftPanel.Controls.OfType<TreeView>().FirstOrDefault(t => t.Name == "dbStructureTree");
            if (treeView == null) return;

            treeView.Nodes.Clear();

            try
            {
                var structure = _dbService.GetDatabaseStructure();
                var rowCounts = _dbService.GetTableRowCounts();
                var primaryKeys = _dbService.GetPrimaryKeys();

                if (structure == null) return;

                // Підрахунок кількості рядків по кожній таблиці
                Dictionary<string, long> tableCounts = new Dictionary<string, long>();
                if (rowCounts != null)
                {
                    foreach (DataRow row in rowCounts.Rows)
                    {
                        string tableName = row["Таблиця"]?.ToString() ?? "";
                        if (long.TryParse(row["Кількість рядків"]?.ToString() ?? "0", out long count))
                        {
                            tableCounts[tableName] = count;
                        }
                    }
                }

                var tableGroups = structure.AsEnumerable()
                    .GroupBy(r => r["Таблиця"].ToString())
                    .OrderBy(g => g.Key);

                foreach (var tableGroup in tableGroups)
                {
                    string tableName = tableGroup.Key ?? "Unknown";
                    long rowCount = tableCounts.ContainsKey(tableName) ? tableCounts[tableName] : 0;

                    TreeNode tableNode = new TreeNode($"📊 {tableName} ({rowCount})")
                    {
                        Tag = tableName
                    };

                    foreach (var column in tableGroup)
                    {
                        string columnName = column["Назва поля"]?.ToString() ?? "";
                        string dataType = column["Тип даних"]?.ToString() ?? "";
                        string nullable = column["Nullable"]?.ToString() ?? "";

                        bool isPK = primaryKeys?.AsEnumerable().Any(pk =>
                            pk["Таблиця"]?.ToString() == tableName &&
                            pk["Поле"]?.ToString() == columnName) ?? false;

                        string icon = isPK ? "🔑" : "📄";
                        string nodeText = $"{icon} {columnName} ({dataType}) {(nullable == "YES" ? "NULL" : "NOT NULL")}";

                        TreeNode columnNode = new TreeNode(nodeText)
                        {
                            Tag = $"{tableName}. {columnName}"
                        };
                        tableNode.Nodes.Add(columnNode);
                    }

                    treeView.Nodes.Add(tableNode);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}", "Помилка");
            }
        }

        // Завантаження детальної інформації про вибрану таблицю (поля, індекси, зв'язки, статистика)
        private void LoadTableDetails(TabPage dbTab, TreeNode? selectedNode)
        {
            if (selectedNode == null || selectedNode.Tag == null) return;

            string tableName = selectedNode.Tag.ToString() ?? "";

            if (tableName.Contains(". "))
            {
                tableName = tableName.Split('.')[0];
            }

            Panel? rightPanel = dbTab.Controls.OfType<Panel>().FirstOrDefault(p => p.Name == "rightPanel");
            if (rightPanel == null) return;

            Label? tableNameLabel = rightPanel.Controls.OfType<Label>().FirstOrDefault(l => l.Name == "tableNameLabel");
            if (tableNameLabel != null)
            {
                tableNameLabel.Text = $"Таблиця: {tableName}";
            }

            TabControl? detailsTabControl = rightPanel.Controls.OfType<TabControl>().FirstOrDefault(t => t.Name == "detailsTabControl");
            if (detailsTabControl == null) return;

            var structure = _dbService.GetDatabaseStructure();
            var indexes = _dbService.GetIndexes();
            var primaryKeys = _dbService.GetPrimaryKeys();
            var foreignKeys = _dbService.GetForeignKeys();

            DataGridView? columnsGrid = detailsTabControl.TabPages[0].Controls.OfType<DataGridView>().FirstOrDefault(g => g.Name == "columnsGrid");
            if (columnsGrid != null && structure != null)
            {
                var tableColumns = structure.AsEnumerable()
                    .Where(r => r["Таблиця"].ToString() == tableName)
                    .Select(r => new
                    {
                        Поле = r["Назва поля"],
                        ТипДаних = r["Тип даних"],
                        Nullable = r["Nullable"],
                        Значення = r["Значення за замовчуванням"]
                    }).ToList();

                columnsGrid.DataSource = tableColumns;
            }

            DataGridView? indexesGrid = detailsTabControl.TabPages[1].Controls.OfType<DataGridView>().FirstOrDefault(g => g.Name == "indexesGrid");
            if (indexesGrid != null && indexes != null)
            {
                var tableIndexes = indexes.AsEnumerable()
                    .Where(r => r["Таблиця"].ToString() == tableName)
                    .Select(r => new
                    {
                        Індекс = r["Індекс"],
                        Тип = r["Тип"],
                        ОсновнийКлюч = r["Основний ключ"]
                    }).ToList();

                indexesGrid.DataSource = tableIndexes;
            }

            DataGridView? relationsGrid = detailsTabControl.TabPages[2].Controls.OfType<DataGridView>().FirstOrDefault(g => g.Name == "relationsGrid");
            if (relationsGrid != null && foreignKeys != null)
            {
                var tableRelations = foreignKeys.AsEnumerable()
                    .Where(r => r["Батьківська таблиця"].ToString() == tableName ||
                                r["Дочірня таблиця"].ToString() == tableName)
                    .Select(r => new
                    {
                        НазваFK = r["Назва FK"],
                        БатьківськаТаблиця = r["Батьківська таблиця"],
                        БатьківськеПоле = r["Батьківське поле"],
                        ДочірняТаблиця = r["Дочірня таблиця"],
                        ДочірнєПоле = r["Дочірнє поле"]
                    }).ToList();

                relationsGrid.DataSource = tableRelations;
            }

            DataGridView? triggersGrid = detailsTabControl.TabPages[3].Controls.OfType<DataGridView>().FirstOrDefault(g => g.Name == "triggersGrid");
            if (triggersGrid != null)
            {
                try
                {
                    var triggersData = _dbService.ExecuteQuery($@"
                        SELECT 
                            name AS 'Назва',
                            type_desc AS 'Тип',
                            is_disabled AS 'Вимкнено',
                            create_date AS 'Дата створення'
                        FROM sys.triggers 
                        WHERE parent_id = OBJECT_ID('{tableName}')
                        ORDER BY name
                    ");
                    triggersGrid.DataSource = triggersData;
                }
                catch
                {
                    triggersGrid.DataSource = null;
                }
            }

            DataGridView? constraintsGrid = detailsTabControl.TabPages[4].Controls.OfType<DataGridView>().FirstOrDefault(g => g.Name == "constraintsGrid");
            if (constraintsGrid != null)
            {
                try
                {
                    var constraintsData = _dbService.ExecuteQuery($@"
                        SELECT 
                            name AS 'Назва',
                            type AS 'Тип',
                            definition AS 'Визначення'
                        FROM sys.check_constraints 
                        WHERE parent_object_id = OBJECT_ID('{tableName}')
                        ORDER BY name
                    ");
                    constraintsGrid.DataSource = constraintsData;
                }
                catch
                {
                    constraintsGrid.DataSource = null;
                }
            }

            // Формування текстового звіту про моніторинг вибраної таблиці
            RichTextBox? statsBox = detailsTabControl.TabPages[5].Controls.OfType<RichTextBox>().FirstOrDefault(b => b.Name == "statsBox");
            if (statsBox != null)
            {
                var rowCounts = _dbService.GetTableRowCounts();
                long rowCount = 0;
                long tableSize = 0;

                if (rowCounts != null)
                {
                    var tableInfo = rowCounts.AsEnumerable().FirstOrDefault(r => r["Таблиця"].ToString() == tableName);
                    if (tableInfo != null && long.TryParse(tableInfo["Кількість рядків"]?.ToString() ?? "0", out long count))
                    {
                        rowCount = count;
                    }
                }

                tableSize = _dbService.GetTableSizeInKB(tableName);
                long dbSizeKB = _dbService.GetDatabaseSizeInKB();

                int columnCount = structure != null
                    ? structure.AsEnumerable().Count(r => r["Таблиця"].ToString() == tableName)
                    : 0;

                int pkCount = primaryKeys != null
                    ? primaryKeys.AsEnumerable().Count(r => r["Таблиця"].ToString() == tableName)
                    : 0;

                int indexCount = indexes != null
                    ? indexes.AsEnumerable().Count(r => r["Таблиця"].ToString() == tableName)
                    : 0;

                int fkCount = foreignKeys != null
                    ? foreignKeys.AsEnumerable().Count(r =>
                        r["Батьківська таблиця"].ToString() == tableName ||
                        r["Дочірня таблиця"].ToString() == tableName)
                    : 0;

                string indexFragmentation = GetIndexFragmentationInfo(tableName);
                string usageStats = GetTableUsageStats(tableName);

                statsBox.Text = $@"
📊 МОНІТОРИНГ ТАБЛИЦІ: {tableName}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📈 ОСНОВНА ІНФОРМАЦІЯ
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Таблиця:                 {tableName}
Кількість рядків:       {rowCount:N0}
Розмір таблиці:         {tableSize:N0} КБ ({(double)tableSize / 1024:F2} МБ)
Середній розмір рядка:  {(rowCount > 0 ? (tableSize * 1024 / rowCount) : 0):F0} байт
Заповненість таблиці:   {(dbSizeKB > 0 ? ((double)tableSize / dbSizeKB * 100) : 0):F2}% від БД

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🔧 СТРУКТУРА
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Кількість колонок:      {columnCount}
Первинних ключів:       {pkCount}
Індексів:               {indexCount}
Зв'язків (FK):          {fkCount}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📉 ФРАГМЕНТАЦІЯ ІНДЕКСІВ
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
{indexFragmentation}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
⚡ ВИКОРИСТАННЯ ТАБЛИЦІ
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
{usageStats}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
💾 БАЗА ДАНИХ
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Загальний розмір БД:    {dbSizeKB:N0} КБ ({(double)dbSizeKB / 1024:F2} МБ)
Статус:                 ✓ Активна

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🔍 РЕКОМЕНДАЦІЇ
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
{GetRecommendations(tableName, tableSize, indexCount)}
";
            }
        }

        // Отримання короткої інформації про фрагментацію індексів таблиці
        private string GetIndexFragmentationInfo(string tableName)
        {
            try
            {
                var indexData = _dbService.ExecuteQuery($@"
                    SELECT 
                        i.name as IndexName,
                        ps.avg_fragmentation_in_percent as Fragmentation
                    FROM sys.indexes i
                    INNER JOIN sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ps
                        ON i.object_id = ps.object_id AND i.index_id = ps.index_id
                    INNER JOIN sys.tables t ON i.object_id = t.object_id
                    WHERE t.name = '{tableName}' AND i.name IS NOT NULL
                    ORDER BY ps.avg_fragmentation_in_percent DESC
                ");

                if (indexData == null || indexData.Rows.Count == 0)
                    return "✓ Індекси відсутні або не потребують дефрагментації";

                string result = "";
                foreach (DataRow row in indexData.Rows)
                {
                    string indexName = row["IndexName"]?.ToString() ?? "Unknown";
                    decimal fragmentation = 0;
                    if (decimal.TryParse(row["Fragmentation"]?.ToString() ?? "0", out fragmentation))
                    {
                        string status = fragmentation < 10 ? "✓ ОК" : fragmentation < 30 ? "⚠️ ПОПЕРЕДЖЕННЯ" : "🔴 КРИТИЧНО";
                        result += $"{indexName.PadRight(30)} {fragmentation:F2}%  {status}\n";
                    }
                }

                return result;
            }
            catch
            {
                return "⚠️ Не вдалось отримати інформацію про фрагментацію індексів";
            }
        }

        // Коротка статистика використання таблиці (скани, пошуки, оновлення)
        private string GetTableUsageStats(string tableName)
        {
            try
            {
                var usageData = _dbService.ExecuteQuery($@"
                    SELECT 
                        SUM(s.user_seeks) as UserSeeks,
                        SUM(s.user_scans) as UserScans,
                        SUM(s.user_lookups) as UserLookups,
                        SUM(s.user_updates) as UserUpdates
                    FROM sys.dm_db_index_usage_stats s
                    INNER JOIN sys.tables t ON s.object_id = t.object_id
                    WHERE t.name = '{tableName}' AND database_id = DB_ID()
                ");

                if (usageData != null && usageData.Rows.Count > 0)
                {
                    long seeks = long.TryParse(usageData.Rows[0]["UserSeeks"]?.ToString() ?? "0", out long s) ? s : 0;
                    long scans = long.TryParse(usageData.Rows[0]["UserScans"]?.ToString() ?? "0", out long sc) ? sc : 0;
                    long lookups = long.TryParse(usageData.Rows[0]["UserLookups"]?.ToString() ?? "0", out long l) ? l : 0;
                    long updates = long.TryParse(usageData.Rows[0]["UserUpdates"]?.ToString() ?? "0", out long u) ? u : 0;

                    return $@"Пошуки:                {seeks:N0}
Повні скани таблиці:   {scans:N0}
Пошуки за індексом:    {lookups:N0}
Оновлення:             {updates:N0}";
                }

                return "Таблиця не використовувалась у цій сесії";
            }
            catch
            {
                return "⚠️ Не вдалось отримати статистику використання таблиці";
            }
        }

        // Формування загальних рекомендацій по обслуговуванню таблиці
        private string GetRecommendations(string tableName, long tableSize, int indexCount)
        {
            string recommendations = "✓ Таблиця в нормальному стані\n";

            if (tableSize > 100000)
                recommendations += "• Розгляньте архівування старих даних\n";

            if (indexCount > 10)
                recommendations += "• Велика кількість індексів - перевірте їх використання\n";

            var rowData = _dbService.ExecuteQuery($"SELECT COUNT(*) as cnt FROM [{tableName}]");
            if (rowData != null && rowData.Rows.Count > 0)
            {
                if (long.TryParse(rowData.Rows[0]["cnt"]?.ToString() ?? "0", out long count))
                {
                    if (count > 1_000_000)
                        recommendations += "• Таблиця дуже велика - розгляньте горизонтальне партиціонування\n";
                }
            }

            recommendations += "• Періодично виконуйте UPDATE STATISTICS для оптимізації запитів";

            return recommendations;
        }

        #endregion

        #region App Users

        // Вкладка «Керування користувачами» – довідник AppUsers
        private void ShowAppUsersTab()
        {
            const string tabName = "appUsers_tab";
            TabPage? existingTab = tabControl.TabPages.Cast<TabPage>().FirstOrDefault(t => t.Name == tabName);
            if (existingTab != null)
            {
                tabControl.SelectedTab = existingTab;
                return;
            }

            TabPage appUsersTab = new TabPage("Керування користувачами")
            {
                Name = tabName,
                Padding = new Padding(0)
            };

            DataGridView grid = CreateGrid("appUsersGrid");
            appUsersTab.Controls.Add(grid);

            Panel buttonPanel = new Panel
            {
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = Color.LightGray
            };

            Button addButton = new Button
            {
                Text = "➕ Додати",
                Location = new Point(10, 10),
                Width = 100,
                Height = 30
            };
            addButton.Click += (s, e) => AddAppUser(appUsersTab);
            buttonPanel.Controls.Add(addButton);

            Button editButton = new Button
            {
                Text = "✏️ Редагувати",
                Location = new Point(120, 10),
                Width = 100,
                Height = 30
            };
            editButton.Click += (s, e) => EditAppUser(appUsersTab);
            buttonPanel.Controls.Add(editButton);

            Button deleteButton = new Button
            {
                Text = "🗑️ Видалити",
                Location = new Point(230, 10),
                Width = 100,
                Height = 30
            };
            deleteButton.Click += (s, e) => DeleteAppUser(appUsersTab);
            buttonPanel.Controls.Add(deleteButton);

            appUsersTab.Controls.Add(buttonPanel);

            tabControl.TabPages.Add(appUsersTab);
            tabControl.SelectedTab = appUsersTab;

            LoadAppUsers(appUsersTab);
        }

        private void LoadAppUsers(TabPage tab)
        {
            DataGridView? grid = tab.Controls["appUsersGrid"] as DataGridView;
            if (grid == null) return;

            var result = _dbService.ExecuteQuery("SELECT Id, Login, PasswordHash, Role, IsActive, UpdatedAt FROM AppUsers ORDER BY Login");

            var columnHeaders = new Dictionary<string, string>
            {
                { "Id", "№" },
                { "Login", "Логін" },
                { "PasswordHash", "Пароль" },
                { "Role", "Роль" },
                { "IsActive", "Активний" },
                { "UpdatedAt", "Останнє оновлення" }
            };

            SetupDataGridView(grid, result, columnHeaders);
        }

        private void AddAppUser(TabPage tab)
        {
            AppUserForm form = new AppUserForm();
            if (form.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string query = "INSERT INTO AppUsers (Login, PasswordHash, Role, IsActive, UpdatedAt) VALUES (@login, @password, @role, @isActive, @updatedAt)";
                    SqlParameter[] parameters =
                    {
                        new SqlParameter("@login", form.UserLogin),
                        new SqlParameter("@password", form.UserPassword),
                        new SqlParameter("@role", form.UserRole),
                        new SqlParameter("@isActive", form.UserIsActive),
                        new SqlParameter("@updatedAt", DateTime.Now)
                    };

                    if (_dbService.ExecuteNonQueryWithParameters(query, parameters))
                    {
                        LogUserAction("CREATE", $"AppUser: {form.UserLogin}");
                        MessageBox.Show("Користувач додан!", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadAppUsers(tab);
                    }
                    else
                    {
                        LogUserAction("CREATE_FAILED", $"AppUser: {form.UserLogin}");
                        MessageBox.Show("Помилка при додаванні!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogUserAction("CREATE_ERROR", $"AppUser - {ex.Message}");
                    MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void EditAppUser(TabPage tab)
        {
            DataGridView? grid = tab.Controls["appUsersGrid"] as DataGridView;
            if (grid == null || grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Виберіть користувача!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int id = int.Parse(grid.SelectedRows[0].Cells["Id"].Value?.ToString() ?? "0");
            string login = grid.SelectedRows[0].Cells["Login"].Value?.ToString() ?? "";
            string password = grid.SelectedRows[0].Cells["PasswordHash"].Value?.ToString() ?? "";
            string role = grid.SelectedRows[0].Cells["Role"].Value?.ToString() ?? "Operator";
            bool isActive = (bool)(grid.SelectedRows[0].Cells["IsActive"].Value ?? false);

            AppUserForm form = new AppUserForm(login, role, isActive);
            if (form.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string query = "UPDATE AppUsers SET Role = @role, IsActive = @isActive, UpdatedAt = @updatedAt WHERE Id = @id";
                    SqlParameter[] parameters =
                    {
                        new SqlParameter("@id", id),
                        new SqlParameter("@role", form.UserRole),
                        new SqlParameter("@isActive", form.UserIsActive),
                        new SqlParameter("@updatedAt", DateTime.Now)
                    };

                    // Якщо пароль змінено – включаємо оновлення PasswordHash
                    if (!string.IsNullOrWhiteSpace(form.UserPassword) && form.UserPassword != password)
                    {
                        query = "UPDATE AppUsers SET Role = @role, IsActive = @isActive, PasswordHash = @password, UpdatedAt = @updatedAt WHERE Id = @id";
                        parameters = new[]
                        {
                            new SqlParameter("@id", id),
                            new SqlParameter("@role", form.UserRole),
                            new SqlParameter("@isActive", form.UserIsActive),
                            new SqlParameter("@password", form.UserPassword),
                            new SqlParameter("@updatedAt", DateTime.Now)
                        };
                    }

                    if (_dbService.ExecuteNonQueryWithParameters(query, parameters))
                    {
                        LogUserAction("UPDATE", $"AppUser ID={id}: {login}");
                        MessageBox.Show("Користувач оновлений!", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadAppUsers(tab);
                    }
                    else
                    {
                        LogUserAction("UPDATE_FAILED", $"AppUser ID={id}");
                        MessageBox.Show("Помилка при оновленні!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogUserAction("UPDATE_ERROR", $"AppUser ID={id} - {ex.Message}");
                    MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DeleteAppUser(TabPage tab)
        {
            DataGridView? grid = tab.Controls["appUsersGrid"] as DataGridView;
            if (grid == null || grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Виберіть користувача!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("Видалити цього користувача?", "Підтвердження", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                int id = int.Parse(grid.SelectedRows[0].Cells["Id"].Value?.ToString() ?? "0");
                string login = grid.SelectedRows[0].Cells["Login"].Value?.ToString() ?? "";
                try
                {
                    string query = "DELETE FROM AppUsers WHERE Id = @id";
                    SqlParameter[] parameters = { new SqlParameter("@id", id) };

                    if (_dbService.ExecuteNonQueryWithParameters(query, parameters))
                    {
                        LogUserAction("DELETE", $"AppUser ID={id}: {login}");
                        MessageBox.Show("Користувач видалений!", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadAppUsers(tab);
                    }
                    else
                    {
                        LogUserAction("DELETE_FAILED", $"AppUser ID={id}");
                        MessageBox.Show("Помилка при видаленні!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    LogUserAction("DELETE_ERROR", $"AppUser ID={id} - {ex.Message}");
                    MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

        #region Logging

        // Ініціалізація служби логування при старті головної форми
        private void InitializeLogging()
        {
            _logService = new LogService(_dbService);

            if (int.TryParse(_userId, out int userId))
            {
                _logService.StartSession(userId, _userId ?? "Unknown");
            }
        }

        // Завершення сесії логування при закритті програми
        private void FinalizeLogging()
        {
            _logService?.EndSession();
        }

        // Завантаження користувачів у фільтр вкладки логів
        private void LoadUsersForLogFilter(ComboBox box)
        {
            box.Items.Add(new FilterItem { Id = 0, Name = "(Усі)" });

            var result = _dbService.ExecuteQuery("SELECT Id, Login FROM AppUsers WHERE IsActive = 1 ORDER BY Login");
            if (result != null)
            {
                foreach (DataRow row in result.Rows)
                {
                    int id = (int)row["Id"];
                    string login = row["Login"]?.ToString() ?? "";
                    box.Items.Add(new FilterItem { Id = id, Name = login });
                }
            }

            box.DisplayMember = "Name";
            box.ValueMember = "Id";
            box.SelectedIndex = 0;
        }

        private void LoadLogs(TabPage tab)
        {
            DataGridView? grid = tab.Controls["logsGrid"] as DataGridView;
            if (grid == null) return;

            var result = _dbService.GetLogs();

            grid.DataSource = result;

            if (grid.Columns.Contains("LogId"))
                grid.Columns["LogId"].HeaderText = "№";
            if (grid.Columns.Contains("UserId"))
                grid.Columns["UserId"].Visible = false;
            if (grid.Columns.Contains("UserLogin"))
                grid.Columns["UserLogin"].HeaderText = "Користувач";
            if (grid.Columns.Contains("LogFileName"))
                grid.Columns["LogFileName"].HeaderText = "Файл логу";
            if (grid.Columns.Contains("CreatedAt"))
                grid.Columns["CreatedAt"].HeaderText = "Створено";
            if (grid.Columns.Contains("ClosedAt"))
                grid.Columns["ClosedAt"].HeaderText = "Закрито";
        }

        // Застосування фільтра за користувачем та діапазоном дат у вкладці логів
        private void ApplyLogFilter(TabPage tab, ComboBox userFilter, DateTimePicker dateFromPicker, DateTimePicker dateToPicker)
        {
            DataGridView? grid = tab.Controls["logsGrid"] as DataGridView;
            if (grid == null) return;

            int? userId = userFilter.SelectedItem is FilterItem fi && fi.Id > 0 ? fi.Id : null;
            DateTime dateFrom = dateFromPicker.Value.Date;
            DateTime dateTo = dateToPicker.Value.Date;

            var result = _dbService.GetLogs(userId, dateFrom, dateTo);

            grid.DataSource = result;

            if (grid.Columns.Contains("LogId"))
                grid.Columns["LogId"].HeaderText = "№";
            if (grid.Columns.Contains("UserId"))
                grid.Columns["UserId"].Visible = false;
            if (grid.Columns.Contains("UserLogin"))
                grid.Columns["UserLogin"].HeaderText = "Користувач";
            if (grid.Columns.Contains("LogFileName"))
                grid.Columns["LogFileName"].HeaderText = "Файл логу";
            if (grid.Columns.Contains("CreatedAt"))
                grid.Columns["CreatedAt"].HeaderText = "Створено";
            if (grid.Columns.Contains("ClosedAt"))
                grid.Columns["ClosedAt"].HeaderText = "Закрито";
        }

        // Перегляд текстового вмісту вибраного логу
        private void ViewLogContent(TabPage tab)
        {
            DataGridView? grid = tab.Controls["logsGrid"] as DataGridView;
            if (grid == null || grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Виберіть лог для перегляду!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int logId = (int)grid.SelectedRows[0].Cells["LogId"].Value;
            LogUserAction("VIEW_LOG", $"Log ID={logId}");

            byte[] fileContent = _dbService.GetLogFileContent(logId);
            string logText = _logService.ReadLogFile(fileContent);

            Form viewForm = new Form
            {
                Text = $"Перегляд логу #{logId}",
                Size = new Size(900, 600),
                StartPosition = FormStartPosition.CenterParent
            };

            RichTextBox richTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Courier New", 10),
                Text = logText,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.Lime
            };
            viewForm.Controls.Add(richTextBox);

            viewForm.ShowDialog();
        }

        // Видалення одного або декількох вибраних логів з БД
        private void DeleteLogFiles(TabPage tab)
        {
            DataGridView? grid = tab.Controls["logsGrid"] as DataGridView;
            if (grid == null || grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Виберіть логи для видалення!", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("Видалити вибрані логи?", "Підтвердження", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                int[] logIds = grid.SelectedRows.Cast<DataGridViewRow>()
                    .Select(r => (int)r.Cells["LogId"].Value)
                    .ToArray();

                if (_dbService.DeleteLogs(logIds))
                {
                    LogUserAction("DELETE", $"Logs: {string.Join(", ", logIds)}");
                    MessageBox.Show("Логи видалені!", "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadLogs(tab);
                }
            }
        }

        // Вкладка «Логи» – робота з таблицею Logs
        private void ShowLogsTab()
        {
            const string tabName = "logs_tab";
            TabPage? existingTab = tabControl.TabPages.Cast<TabPage>().FirstOrDefault(t => t.Name == tabName);
            if (existingTab != null)
            {
                tabControl.SelectedTab = existingTab;
                return;
            }

            TabPage logsTab = new TabPage("Логи")
            {
                Name = tabName,
                Padding = new Padding(0)
            };

            DataGridView grid = new DataGridView
            {
                Name = "logsGrid",
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true, // дає змогу виділяти кілька логів для видалення
                BackgroundColor = Color.White,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };
            logsTab.Controls.Add(grid);

            // Верхня панель з фільтрами та кнопками керування логами
            Panel filterPanel = new Panel
            {
                Height = 80,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(240, 240, 240),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10)
            };

            Label userLabel = new Label
            {
                Text = "Користувач:",
                Location = new Point(10, 10),
                Width = 80,
                Height = 20
            };
            filterPanel.Controls.Add(userLabel);

            ComboBox userFilter = new ComboBox
            {
                Name = "userFilter",
                Location = new Point(95, 10),
                Width = 150,
                Height = 20
            };
            LoadUsersForLogFilter(userFilter);
            filterPanel.Controls.Add(userFilter);

            Label dateFromLabel = new Label
            {
                Text = "Від:",
                Location = new Point(260, 10),
                Width = 40,
                Height = 20
            };
            filterPanel.Controls.Add(dateFromLabel);

            DateTimePicker dateFromPicker = new DateTimePicker
            {
                Name = "dateFromPicker",
                Location = new Point(305, 10),
                Width = 120,
                Height = 20,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now
            };
            filterPanel.Controls.Add(dateFromPicker);

            Label dateToLabel = new Label
            {
                Text = "До:",
                Location = new Point(430, 10),
                Width = 40,
                Height = 20
            };
            filterPanel.Controls.Add(dateToLabel);

            DateTimePicker dateToPicker = new DateTimePicker
            {
                Name = "dateToPicker",
                Location = new Point(475, 10),
                Width = 120,
                Height = 20,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Now
            };
            filterPanel.Controls.Add(dateToPicker);

            int btnWidth = 140;
            int btnHeight = 30;
            int spacing = 10;
            int startX = 10;
            int btnY = 40;

            Button filterBtn = new Button
            {
                Text = "🔍 Фільтрувати",
                Location = new Point(startX, btnY),
                Width = btnWidth,
                Height = btnHeight,
                BackColor = Color.LightBlue
            };
            filterBtn.Click += (s, e) => ApplyLogFilter(logsTab, userFilter, dateFromPicker, dateToPicker);
            filterPanel.Controls.Add(filterBtn);

            Button resetBtn = new Button
            {
                Text = "✖️ Скинути",
                Location = new Point(startX + btnWidth + spacing, btnY),
                Width = btnWidth,
                Height = btnHeight,
                BackColor = Color.LightCoral
            };
            resetBtn.Click += (s, e) => ResetLogFilter(logsTab, userFilter, dateFromPicker, dateToPicker);
            filterPanel.Controls.Add(resetBtn);

            Button refreshBtn = new Button
            {
                Text = "🔄 Оновити",
                Location = new Point(startX + (btnWidth + spacing) * 2, btnY),
                Width = btnWidth,
                Height = btnHeight,
                BackColor = Color.LightGreen
            };
            refreshBtn.Click += (s, e) => LoadLogs(logsTab);
            filterPanel.Controls.Add(refreshBtn);

            Button viewBtn = new Button
            {
                Text = "👁️ Переглянути",
                Location = new Point(startX + (btnWidth + spacing) * 3, btnY),
                Width = btnWidth,
                Height = btnHeight,
                BackColor = Color.LightYellow
            };
            viewBtn.Click += (s, e) => ViewLogContent(logsTab);
            filterPanel.Controls.Add(viewBtn);

            Button deleteBtn = new Button
            {
                Text = "🗑️ Видалити",
                Location = new Point(startX + (btnWidth + spacing) * 4, btnY),
                Width = btnWidth,
                Height = btnHeight,
                BackColor = Color.LightCoral
            };
            deleteBtn.Click += (s, e) => DeleteLogFiles(logsTab);
            filterPanel.Controls.Add(deleteBtn);

            logsTab.Controls.Add(filterPanel);

            tabControl.TabPages.Add(logsTab);
            tabControl.SelectedTab = logsTab;

            LoadLogs(logsTab);
        }

        private void ResetLogFilter(TabPage tab, ComboBox userFilter, DateTimePicker dateFromPicker, DateTimePicker dateToPicker)
        {
            userFilter.SelectedIndex = 0;
            dateFromPicker.Value = DateTime.Now;
            dateToPicker.Value = DateTime.Now;

            LoadLogs(tab);
        }

        #endregion
    }

    // Допоміжний клас для елементів фільтра (Id + назва для ComboBox)
    public class FilterItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";

        public override string ToString() => Name;
    }
}