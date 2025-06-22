using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using Library.Services;

namespace Library.Views
{
    public partial class DatabaseAdministrationWindow : Window
    {
        private DatabaseService? _databaseService;
        private string? _connectionString;
        private DispatcherTimer? _refreshTimer;
        private DispatcherTimer? _clockTimer;

        public DatabaseAdministrationWindow()
        {
            try
            {
                 InitializeComponent();
                
                 _connectionString = string.Empty;
                
                 Loaded += OnWindowLoaded;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Критическая ошибка в конструкторе: {ex.Message}");
                 MessageBox.Show($"Критическая ошибка при создании окна:\n{ex.Message}", 
                              "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                 try
                {
                    _databaseService = new DatabaseService();
                    _connectionString = _databaseService.GetConnectionString();
                    SetStatus("Подключение к базе данных установлено");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка инициализации БД: {ex.Message}");
                    _connectionString = string.Empty;
                    SetStatus("Работа без подключения к БД");
                }
                
                 try
                {
                    _refreshTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(30)
                    };
                    _refreshTimer.Tick += RefreshTimer_Tick;
                    
                    _clockTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(1)
                    };
                    _clockTimer.Tick += ClockTimer_Tick;
                    
                     _refreshTimer?.Start();
                    _clockTimer?.Start();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка инициализации таймеров: {ex.Message}");
                }
                
                 UpdateServerInfo();
                
                 if (!string.IsNullOrEmpty(_connectionString))
                {
                    await InitializeDataAsync();
                }
                else
                {
                    SetStatus("Готов к работе (ограниченный режим)");
                }
            }
            catch (Exception ex)
            {
                SetStatus("Ошибка инициализации");
                MessageBox.Show($"Ошибка при инициализации окна:\n{ex.Message}", 
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdateServerInfo()
        {
            try
            {
                if (ServerInfoText == null) return;
                
                if (string.IsNullOrEmpty(_connectionString))
                {
                    ServerInfoText.Text = "Подключение к базе данных не установлено";
                    return;
                }
                
                var builder = new SqlConnectionStringBuilder(_connectionString!);
                ServerInfoText.Text = $"Сервер: {builder.DataSource} | База: {builder.InitialCatalog}";
            }
            catch (Exception ex)
            {
                if (ServerInfoText != null)
                {
                    ServerInfoText.Text = $"Ошибка получения информации о сервере: {ex.Message}";
                }
                System.Diagnostics.Debug.WriteLine($"Ошибка UpdateServerInfo: {ex.Message}");
            }
        }

        private async Task InitializeDataAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_connectionString))
                {
                    SetStatus("Подключение к базе данных недоступно");
                    return;
                }
                
                SetStatus("Инициализация данных...");
                
                // Параллельная загрузка всех данных
                await Task.WhenAll(
                    RefreshServerInfoAsync(),
                    RefreshDatabasesAsync(),
                    RefreshSecurityAsync()
                );
                
                SetStatus("Готов");
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка инициализации: {ex.Message}");
                MessageBox.Show($"Ошибка при инициализации данных:\n{ex.Message}", 
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            // Автоматическое обновление данных
            _ = RefreshSecurityAsync();
        }

        private void ClockTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (CurrentTimeText != null)
                {
                    CurrentTimeText.Text = DateTime.Now.ToString("HH:mm:ss dd.MM.yyyy");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления времени: {ex.Message}");
            }
        }

        private void SetStatus(string message)
        {
            try
            {
                if (StatusText != null)
                {
                    StatusText.Text = message;
                }
            }
            catch
            {
                // Игнорируем ошибки при обновлении статуса
            }
        }

        #region Информация о сервере

        private async Task RefreshServerInfoAsync()
        {
            try
            {
                SetStatus("Загрузка информации о сервере...");
                
                var serverInfo = new List<ServerInfoItem>();
                
                using var connection = new SqlConnection(_connectionString!);
                await connection.OpenAsync();

                // Версия SQL Server
                var versionQuery = "SELECT @@VERSION as ServerVersion";
                using var versionCmd = new SqlCommand(versionQuery, connection);
                var version = await versionCmd.ExecuteScalarAsync() as string;
                serverInfo.Add(new ServerInfoItem("Версия SQL Server", version?.Split('\n')[0] ?? "Неизвестно"));

                // Информация о сервере
                var infoQuery = @"
                    SELECT 
                        @@SERVERNAME as ServerName,
                        SERVERPROPERTY('Edition') as Edition,
                        SERVERPROPERTY('ProductLevel') as ProductLevel,
                        SERVERPROPERTY('EngineEdition') as EngineEdition,
                        SERVERPROPERTY('Collation') as Collation";
                
                using var infoCmd = new SqlCommand(infoQuery, connection);
                using var reader = await infoCmd.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    serverInfo.Add(new ServerInfoItem("Имя сервера", reader["ServerName"]?.ToString() ?? ""));
                    serverInfo.Add(new ServerInfoItem("Редакция", reader["Edition"]?.ToString() ?? ""));
                    serverInfo.Add(new ServerInfoItem("Уровень продукта", reader["ProductLevel"]?.ToString() ?? ""));
                    serverInfo.Add(new ServerInfoItem("Тип движка", GetEngineEditionName(reader["EngineEdition"]?.ToString())));
                    serverInfo.Add(new ServerInfoItem("Кодировка", reader["Collation"]?.ToString() ?? ""));
                }
                reader.Close();

                // Время запуска сервера
                var uptimeQuery = "SELECT sqlserver_start_time FROM sys.dm_os_sys_info";
                using var uptimeCmd = new SqlCommand(uptimeQuery, connection);
                var startTime = await uptimeCmd.ExecuteScalarAsync();
                if (startTime != null)
                {
                    var uptime = DateTime.Now - (DateTime)startTime;
                    serverInfo.Add(new ServerInfoItem("Время работы", 
                        $"{uptime.Days}д {uptime.Hours}ч {uptime.Minutes}м"));
                }

                // Обновляем UI в главном потоке
                Dispatcher.Invoke(() =>
                {
                    if (ServerInfoGrid != null)
                        ServerInfoGrid.ItemsSource = serverInfo;
                    if (ConnectionStatusText != null)
                        ConnectionStatusText.Text = "Подключение активно";
                    if (LastConnectedText != null)
                        LastConnectedText.Text = $"Последнее подключение: {DateTime.Now:HH:mm:ss}";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    if (ConnectionStatusText != null)
                        ConnectionStatusText.Text = "Ошибка подключения";
                    if (LastConnectedText != null)
                        LastConnectedText.Text = $"Ошибка: {ex.Message}";
                });
            }
        }

        private static string GetEngineEditionName(string? engineEdition)
        {
            return engineEdition switch
            {
                "1" => "Personal/Desktop",
                "2" => "Standard",
                "3" => "Enterprise",
                "4" => "Express",
                "5" => "SQL Database",
                "6" => "SQL Data Warehouse",
                _ => "Неизвестно"
            };
        }

        private async void RefreshServerInfoButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshServerInfoAsync();
        }

        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetStatus("Тестирование подключения...");
                
                var stopwatch = Stopwatch.StartNew();
                var (success, message) = await DatabaseService.TestDatabaseConnectionAsync();
                stopwatch.Stop();
                
                string resultMessage = success 
                    ? $"Подключение успешно! Время отклика: {stopwatch.ElapsedMilliseconds}мс"
                    : $"Ошибка подключения: {message}";
                
                MessageBox.Show(resultMessage, "Тест подключения", 
                              MessageBoxButton.OK, 
                              success ? MessageBoxImage.Information : MessageBoxImage.Error);
                
                SetStatus("Готов");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка тестирования: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Готов");
            }
        }

        #endregion

        #region Управление базами данных

        private async Task RefreshDatabasesAsync()
        {
            try
            {
                SetStatus("Загрузка списка баз данных...");
                
                var databases = new List<DatabaseInfo>();
                
                using var connection = new SqlConnection(_connectionString!);
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        d.name as DatabaseName,
                        d.database_id,
                        d.state_desc as State,
                        SUSER_SNAME(d.owner_sid) as Owner,
                        d.create_date as CreateDate,
                        CASE 
                            WHEN d.name IN ('master', 'model', 'msdb', 'tempdb') THEN 'Системная'
                            ELSE 'Пользовательская'
                        END as DatabaseType,
                        CAST(SUM(size) * 8.0 / 1024 AS DECIMAL(10,2)) as SizeMB
                    FROM sys.databases d
                    LEFT JOIN sys.master_files f ON d.database_id = f.database_id
                    GROUP BY d.name, d.database_id, d.state_desc, d.owner_sid, d.create_date
                    ORDER BY d.name";

                using var cmd = new SqlCommand(query, connection);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    databases.Add(new DatabaseInfo
                    {
                        Name = reader["DatabaseName"]?.ToString() ?? "",
                        State = reader["State"]?.ToString() ?? "",
                        Owner = reader["Owner"]?.ToString() ?? "",
                        CreateDate = ((DateTime)reader["CreateDate"]).ToString("dd.MM.yyyy HH:mm"),
                        SizeMB = reader["SizeMB"]?.ToString() ?? "0",
                        LastModified = DateTime.Now.ToString("dd.MM.yyyy HH:mm") // Упрощено
                    });
                }

                Dispatcher.Invoke(() =>
                {
                    if (DatabasesGrid != null)
                        DatabasesGrid.ItemsSource = databases;
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    SetStatus($"Ошибка загрузки БД: {ex.Message}");
                });
            }
        }

        private async void RefreshDatabasesButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshDatabasesAsync();
        }

        private void CreateDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CreateDatabaseDialog();
            if (dialog.ShowDialog() == true)
            {
                _ = CreateDatabaseAsync(dialog.DatabaseName, dialog.InitialSize, dialog.GrowthSize);
            }
        }

        private async Task CreateDatabaseAsync(string dbName, int initialSize, int growthSize)
        {
            try
            {
                SetStatus($"Создание базы данных {dbName}...");
                
                using var connection = new SqlConnection(_connectionString!);
                await connection.OpenAsync();

                var query = $@"
                    CREATE DATABASE [{dbName}]
                    ON (
                        NAME = '{dbName}',
                        FILENAME = 'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA\{dbName}.mdf',
                        SIZE = {initialSize}MB,
                        FILEGROWTH = {growthSize}MB
                    )
                    LOG ON (
                        NAME = '{dbName}_Log',
                        FILENAME = 'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA\{dbName}_Log.ldf',
                        SIZE = {initialSize / 4}MB,
                        FILEGROWTH = {growthSize / 2}MB
                    )";

                using var cmd = new SqlCommand(query, connection);
                await cmd.ExecuteNonQueryAsync();

                MessageBox.Show($"База данных '{dbName}' успешно создана!", "Успех", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
                
                await RefreshDatabasesAsync();
                SetStatus("Готов");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания базы данных:\n{ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Готов");
            }
        }

        private async void BackupDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDb = DatabasesGrid.SelectedItem as DatabaseInfo;
            if (selectedDb == null)
            {
                MessageBox.Show("Выберите базу данных для создания резервной копии.", "Внимание", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "SQL Server backup files (*.bak)|*.bak",
                FileName = $"{selectedDb.Name}_backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak",
                InitialDirectory = @"C:\SQLBackups"
            };

            if (dialog.ShowDialog() == true)
            {
                await CreateBackupAsync(selectedDb.Name, dialog.FileName);
            }
        }

        private async Task CreateBackupAsync(string databaseName, string backupPath)
        {
            try
            {
                SetStatus($"Создание резервной копии {databaseName}...");
                
                // Создаем директорию если не существует
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                
                using var connection = new SqlConnection(_connectionString!);
                await connection.OpenAsync();

                var query = $@"
                    BACKUP DATABASE [{databaseName}]
                    TO DISK = @BackupPath
                    WITH FORMAT, INIT, NAME = @BackupName";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@BackupPath", backupPath);
                cmd.Parameters.AddWithValue("@BackupName", $"{databaseName} Backup - {DateTime.Now}");
                
                await cmd.ExecuteNonQueryAsync();

                MessageBox.Show($"Резервная копия успешно создана:\n{backupPath}", "Успех", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
                SetStatus("Готов");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания резервной копии:\n{ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Готов");
            }
        }

        private async void RestoreDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "SQL Server backup files (*.bak)|*.bak",
                InitialDirectory = @"C:\SQLBackups"
            };

            if (dialog.ShowDialog() == true)
            {
                var restoreDialog = new RestoreDatabaseDialog();
                if (restoreDialog.ShowDialog() == true)
                {
                    await RestoreDatabaseAsync(restoreDialog.DatabaseName, dialog.FileName);
                }
            }
        }

        private async Task RestoreDatabaseAsync(string databaseName, string backupPath)
        {
            try
            {
                SetStatus($"Восстановление базы данных {databaseName}...");
                
                // Подключаемся к master базе
                var masterConnectionString = new SqlConnectionStringBuilder(_connectionString!)
                {
                    InitialCatalog = "master"
                }.ConnectionString;

                using var connection = new SqlConnection(masterConnectionString);
                await connection.OpenAsync();

                // Завершаем активные подключения к БД
                var killQuery = $@"
                    IF EXISTS (SELECT name FROM sys.databases WHERE name = '{databaseName}')
                    BEGIN
                        ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE
                    END";
                
                using var killCmd = new SqlCommand(killQuery, connection);
                await killCmd.ExecuteNonQueryAsync();

                // Восстанавливаем БД
                var restoreQuery = $@"
                    RESTORE DATABASE [{databaseName}]
                    FROM DISK = @BackupPath
                    WITH REPLACE";

                using var restoreCmd = new SqlCommand(restoreQuery, connection);
                restoreCmd.Parameters.AddWithValue("@BackupPath", backupPath);
                await restoreCmd.ExecuteNonQueryAsync();

                // Возвращаем в многопользовательский режим
                var multiUserQuery = $"ALTER DATABASE [{databaseName}] SET MULTI_USER";
                using var multiCmd = new SqlCommand(multiUserQuery, connection);
                await multiCmd.ExecuteNonQueryAsync();

                MessageBox.Show($"База данных '{databaseName}' успешно восстановлена!", "Успех", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
                
                await RefreshDatabasesAsync();
                SetStatus("Готов");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка восстановления базы данных:\n{ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Готов");
            }
        }

        #endregion



        #region Безопасность

        private async Task RefreshSecurityAsync()
        {
            try
            {
                SetStatus("Загрузка информации о безопасности...");
                
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Логины сервера
                var loginsQuery = @"
                    SELECT 
                        name as LoginName,
                        type_desc as LoginType,
                        default_database_name as DefaultDatabase,
                        create_date as CreateDate,
                        modify_date as LastLogin,
                        is_disabled as IsDisabled
                    FROM sys.server_principals
                    WHERE type IN ('S', 'U', 'G')
                    ORDER BY name";

                using var loginsCmd = new SqlCommand(loginsQuery, connection);
                using var loginsReader = await loginsCmd.ExecuteReaderAsync();
                
                var logins = new List<LoginInfo>();
                while (await loginsReader.ReadAsync())
                {
                    logins.Add(new LoginInfo
                    {
                        LoginName = loginsReader["LoginName"].ToString(),
                        LoginType = loginsReader["LoginType"].ToString(),
                        DefaultDatabase = loginsReader["DefaultDatabase"].ToString(),
                        CreateDate = ((DateTime)loginsReader["CreateDate"]).ToString("dd.MM.yyyy"),
                        LastLogin = ((DateTime)loginsReader["LastLogin"]).ToString("dd.MM.yyyy"),
                        IsEnabled = !(bool)loginsReader["IsDisabled"]
                    });
                }
                loginsReader.Close();

                // Пользователи БД
                var usersQuery = @"
                    SELECT 
                        dp.name as UserName,
                        sp.name as LoginName,
                        dp.type_desc as UserType,
                        dp.default_schema_name as DefaultSchema,
                        dp.create_date as CreateDate
                    FROM sys.database_principals dp
                    LEFT JOIN sys.server_principals sp ON dp.sid = sp.sid
                    WHERE dp.type IN ('S', 'U', 'G')
                    AND dp.name NOT IN ('guest', 'INFORMATION_SCHEMA', 'sys')
                    ORDER BY dp.name";

                using var usersCmd = new SqlCommand(usersQuery, connection);
                using var usersReader = await usersCmd.ExecuteReaderAsync();
                
                var dbUsers = new List<DatabaseUserInfo>();
                while (await usersReader.ReadAsync())
                {
                    dbUsers.Add(new DatabaseUserInfo
                    {
                        UserName = usersReader["UserName"].ToString(),
                        LoginName = usersReader["LoginName"]?.ToString() ?? "N/A",
                        Role = "User", // Упрощено
                        DefaultSchema = usersReader["DefaultSchema"]?.ToString() ?? "dbo",
                        CreateDate = ((DateTime)usersReader["CreateDate"]).ToString("dd.MM.yyyy")
                    });
                }
                usersReader.Close();

                // Роли сервера
                var serverRolesQuery = @"
                    SELECT 
                        name as RoleName,
                        'Системная роль сервера' as Description
                    FROM sys.server_principals
                    WHERE type = 'R'
                    ORDER BY name";

                using var serverRolesCmd = new SqlCommand(serverRolesQuery, connection);
                using var serverRolesReader = await serverRolesCmd.ExecuteReaderAsync();
                
                var serverRoles = new List<RoleInfo>();
                while (await serverRolesReader.ReadAsync())
                {
                    serverRoles.Add(new RoleInfo
                    {
                        RoleName = serverRolesReader["RoleName"].ToString(),
                        Description = serverRolesReader["Description"].ToString()
                    });
                }
                serverRolesReader.Close();

                // Роли БД
                var dbRolesQuery = @"
                    SELECT 
                        dp.name as RoleName,
                        USER_NAME(dp.owning_principal_id) as Owner
                    FROM sys.database_principals dp
                    WHERE dp.type = 'R'
                    AND dp.is_fixed_role = 0
                    ORDER BY dp.name";

                using var dbRolesCmd = new SqlCommand(dbRolesQuery, connection);
                using var dbRolesReader = await dbRolesCmd.ExecuteReaderAsync();
                
                var dbRoles = new List<DatabaseRoleInfo>();
                while (await dbRolesReader.ReadAsync())
                {
                    dbRoles.Add(new DatabaseRoleInfo
                    {
                        RoleName = dbRolesReader["RoleName"].ToString(),
                        Owner = dbRolesReader["Owner"]?.ToString() ?? "N/A"
                    });
                }

                // Обновляем UI
                Dispatcher.Invoke(() =>
                {
                    LoginsGrid.ItemsSource = logins;
                    DatabaseUsersGrid.ItemsSource = dbUsers;
                    ServerRolesGrid.ItemsSource = serverRoles;
                    DatabaseRolesGrid.ItemsSource = dbRoles;
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    SetStatus($"Ошибка загрузки безопасности: {ex.Message}");
                });
            }
        }

        private async void RefreshSecurityButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshSecurityAsync();
        }

        private void CreateLoginButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CreateLoginDialog();
            if (dialog.ShowDialog() == true)
            {
                _ = CreateLoginAsync(dialog.LoginName, dialog.Password, dialog.DefaultDatabase);
            }
        }

        private async Task CreateLoginAsync(string loginName, string password, string defaultDatabase)
        {
            try
            {
                SetStatus($"Создание логина {loginName}...");
                
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = $@"
                    CREATE LOGIN [{loginName}]
                    WITH PASSWORD = @Password,
                    DEFAULT_DATABASE = [{defaultDatabase}],
                    CHECK_EXPIRATION = OFF,
                    CHECK_POLICY = OFF";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Password", password);
                await cmd.ExecuteNonQueryAsync();

                MessageBox.Show($"Логин '{loginName}' успешно создан!", "Успех", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
                
                await RefreshSecurityAsync();
                SetStatus("Готов");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания логина:\n{ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Готов");
            }
        }

        private void ManagePermissionsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция управления правами будет реализована в следующей версии.", 
                          "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AuditLogButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция журнала аудита будет реализована в следующей версии.", 
                          "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        

        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer?.Stop();
            _clockTimer?.Stop();
            base.OnClosed(e);
        }
    }

    // Классы для данных
    public class ServerInfoItem
    {
        public string Parameter { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;

        public ServerInfoItem(string parameter, string value)
        {
            Parameter = parameter;
            Value = value;
        }
    }

    public class DatabaseInfo
    {
        public string Name { get; set; } = string.Empty;
        public string SizeMB { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string CreateDate { get; set; } = string.Empty;
        public string LastModified { get; set; } = string.Empty;
    }



    public class LoginInfo
    {
        public string LoginName { get; set; } = string.Empty;
        public string LoginType { get; set; } = string.Empty;
        public string DefaultDatabase { get; set; } = string.Empty;
        public string CreateDate { get; set; } = string.Empty;
        public string LastLogin { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }

    public class DatabaseUserInfo
    {
        public string UserName { get; set; } = string.Empty;
        public string LoginName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string DefaultSchema { get; set; } = string.Empty;
        public string CreateDate { get; set; } = string.Empty;
    }

    public class RoleInfo
    {
        public string RoleName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class DatabaseRoleInfo
    {
        public string RoleName { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
    }
} 