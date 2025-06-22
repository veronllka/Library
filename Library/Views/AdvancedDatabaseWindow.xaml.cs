using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Library.Services;

namespace Library.Views
{
    public partial class AdvancedDatabaseWindow : Window
    {
        private readonly DatabaseService _databaseService;

        public AdvancedDatabaseWindow()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            LoadMessage("Окно инициализировано. Готов к работе с объектами базы данных.");
            
             _ = InitializeDatabaseObjectsAsync();
        }

        private async Task InitializeDatabaseObjectsAsync()
        {
            try
            {
                 var checkQuery = @"
SELECT 
    CASE WHEN OBJECT_ID('VW_PopularBooks', 'V') IS NOT NULL THEN 1 ELSE 0 END AS ViewsExist,
    CASE WHEN OBJECT_ID('FN_CalculateFine', 'FN') IS NOT NULL THEN 1 ELSE 0 END AS FunctionsExist,
    CASE WHEN OBJECT_ID('SP_SmartIssue', 'P') IS NOT NULL THEN 1 ELSE 0 END AS ProceduresExist,
    CASE WHEN OBJECT_ID('AuditLog', 'U') IS NOT NULL THEN 1 ELSE 0 END AS TablesExist";

                var result = await Task.Run(() => _databaseService.ExecuteQuery(checkQuery));
                
                if (result.Rows.Count > 0)
                {
                    var row = result.Rows[0];
                    bool allExist = Convert.ToBoolean(row["ViewsExist"]) && 
                                   Convert.ToBoolean(row["FunctionsExist"]) && 
                                   Convert.ToBoolean(row["ProceduresExist"]) && 
                                   Convert.ToBoolean(row["TablesExist"]);

                    if (!allExist)
                    {
                        LoadMessage("Некоторые объекты БД отсутствуют. Создаём автоматически...");
                        await CreateDatabaseObjectsAsync();
                    }
                    else
                    {
                        LoadMessage("Все объекты БД найдены и готовы к работе.");
                    }
                }
            }
            catch (Exception ex)
            {
                LoadMessage($"Ошибка при проверке объектов БД: {ex.Message}");
                LoadMessage("Попробуйте создать объекты вручную кнопкой 'Создать объекты'");
            }
        }

        private void LoadMessage(string message)
        {
            LogTextBox.AppendText($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            LogTextBox.ScrollToEnd();
            StatusTextBlock.Text = message;
        }

        private async Task ExecuteQuery(string query, string description)
        {
            try
            {
                StatusTextBlock.Text = $"Выполняется: {description}...";
                var stopwatch = Stopwatch.StartNew();
                
                var result = await Task.Run(() => _databaseService.ExecuteQuery(query));
                
                stopwatch.Stop();
                
                ResultDataGrid.ItemsSource = result.DefaultView;
                SqlQueryTextBox.Text = query;
                RowCountTextBlock.Text = result.Rows.Count.ToString();
                ExecutionTimeTextBlock.Text = $"{stopwatch.ElapsedMilliseconds} мс";
                
                LoadMessage($"{description} выполнено успешно. Получено строк: {result.Rows.Count}");
            }
            catch (Exception ex)
            {
                LoadMessage($"Ошибка при выполнении {description}: {ex.Message}");
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка выполнения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteNonQuery(string query, string description)
        {
            try
            {
                StatusTextBlock.Text = $"Выполняется: {description}...";
                var stopwatch = Stopwatch.StartNew();
                
                await Task.Run(() => _databaseService.ExecuteNonQuery(query));
                
                stopwatch.Stop();
                ExecutionTimeTextBlock.Text = $"{stopwatch.ElapsedMilliseconds} мс";
                
                LoadMessage($"{description} выполнено успешно");
            }
            catch (Exception ex)
            {
                LoadMessage($"Ошибка при выполнении {description}: {ex.Message}");
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка выполнения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Представления
        private async void BtnPopularBooks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string checkQuery = "SELECT CASE WHEN OBJECT_ID('VW_PopularBooks', 'V') IS NOT NULL THEN 1 ELSE 0 END AS Существует";
                var checkResult = await Task.Run(() => _databaseService.ExecuteQuery(checkQuery));
                
                if (checkResult.Rows.Count > 0 && Convert.ToBoolean(checkResult.Rows[0]["Существует"]))
                {
                    string query = "SELECT * FROM VW_PopularBooks ORDER BY КоличествоВыдач DESC";
                    await ExecuteQuery(query, "Запрос популярных книг");
                }
                else
                {
                    LoadMessage("Представление VW_PopularBooks не найдено. Создайте объекты сначала.");
                    MessageBox.Show("Объекты базы данных не созданы!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LoadMessage($"Ошибка при получении популярных книг: {ex.Message}");
            }
        }

        private async void BtnReaderActivity_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string checkQuery = "SELECT CASE WHEN OBJECT_ID('VW_ReaderActivity', 'V') IS NOT NULL THEN 1 ELSE 0 END AS Существует";
                var checkResult = await Task.Run(() => _databaseService.ExecuteQuery(checkQuery));
                
                if (checkResult.Rows.Count > 0 && Convert.ToBoolean(checkResult.Rows[0]["Существует"]))
                {
                    string query = "SELECT * FROM VW_ReaderActivity ORDER BY ВсегоВыдач DESC";
                    await ExecuteQuery(query, "Запрос активности читателей");
                }
                else
                {
                    LoadMessage("Представление VW_ReaderActivity не найдено. Создайте объекты сначала.");
                    MessageBox.Show("Объекты базы данных не созданы!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LoadMessage($"Ошибка при получении активности читателей: {ex.Message}");
            }
        }

        // Функции
        private async void BtnCalculateFine_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверяем существование функции
                string checkQuery = "SELECT CASE WHEN OBJECT_ID('FN_CalculateFine', 'FN') IS NOT NULL THEN 1 ELSE 0 END AS Существует";
                var checkResult = await Task.Run(() => _databaseService.ExecuteQuery(checkQuery));
                
                if (checkResult.Rows.Count > 0 && Convert.ToBoolean(checkResult.Rows[0]["Существует"]))
                {
                    string query = @"
SELECT 
    'Штраф за 5 дней просрочки' AS Описание,
    dbo.FN_CalculateFine('2024-01-01', '2024-01-06') AS Штраф
UNION ALL
SELECT 
    'Штраф за 10 дней просрочки' AS Описание,
    dbo.FN_CalculateFine('2024-01-01', '2024-01-11') AS Штраф
UNION ALL
SELECT 
    'Штраф за текущую дату (если просрочено)' AS Описание,
    dbo.FN_CalculateFine('2024-01-01', NULL) AS Штраф";
                    
                    await ExecuteQuery(query, "Демонстрация функции расчёта штрафа");
                }
                else
                {
                    LoadMessage("Функция FN_CalculateFine не найдена. Нажмите 'Создать объекты' сначала.");
                    MessageBox.Show("Объекты базы данных не созданы!\nНажмите кнопку 'Создать объекты' для их создания.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LoadMessage($"Ошибка при тестировании функции: {ex.Message}");
            }
        }

        private async void BtnCheckReader_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string checkQuery = "SELECT CASE WHEN OBJECT_ID('FN_CheckReader', 'FN') IS NOT NULL THEN 1 ELSE 0 END AS Существует";
                var checkResult = await Task.Run(() => _databaseService.ExecuteQuery(checkQuery));
                
                if (checkResult.Rows.Count > 0 && Convert.ToBoolean(checkResult.Rows[0]["Существует"]))
                {
                    string query = @"
SELECT 
    r.ЧитательID,
    r.Фамилия + ' ' + r.Имя AS ПолноеИмя,
    dbo.FN_CheckReader(r.ЧитательID) AS СтатусПроверки
FROM Читатели r";
                    
                    await ExecuteQuery(query, "Проверка статуса читателей");
                }
                else
                {
                    LoadMessage("Функция FN_CheckReader не найдена. Создайте объекты сначала.");
                    MessageBox.Show("Объекты базы данных не созданы!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LoadMessage($"Ошибка при проверке статуса читателей: {ex.Message}");
            }
        }

        // Хранимые процедуры
        private async void BtnSmartIssue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var bookId = 1; // Можно сделать диалог выбора
                var readerId = 1; // Можно сделать диалог выбора
                
                using (var connection = new SqlConnection(_databaseService.GetConnectionString()))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand("SP_SmartIssue", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@КнигаID", bookId);
                        command.Parameters.AddWithValue("@ЧитательID", readerId);
                        
                        var resultParam = new SqlParameter("@Результат", SqlDbType.NVarChar, 200)
                        {
                            Direction = ParameterDirection.Output
                        };
                        command.Parameters.Add(resultParam);
                        
                        await command.ExecuteNonQueryAsync();
                        
                        string result = resultParam.Value?.ToString() ?? "Нет результата";
                        LoadMessage($"Результат умной выдачи: {result}");
                        
                        // Обновляем отображение выдач
                        await ExecuteQuery("SELECT TOP 10 * FROM Выдачи ORDER BY ВыдачаID DESC", "Последние выдачи");
                    }
                }
            }
            catch (Exception ex)
            {
                LoadMessage($"Ошибка в умной выдаче: {ex.Message}");
            }
        }

        private async void BtnProcessOverdue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var connection = new SqlConnection(_databaseService.GetConnectionString()))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand("SP_ProcessOverdue", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        
                        var processedParam = new SqlParameter("@Обработано", SqlDbType.Int)
                        {
                            Direction = ParameterDirection.Output
                        };
                        var finesParam = new SqlParameter("@СуммаШтрафов", SqlDbType.Decimal)
                        {
                            Direction = ParameterDirection.Output,
                            Precision = 15,
                            Scale = 2
                        };
                        
                        command.Parameters.Add(processedParam);
                        command.Parameters.Add(finesParam);
                        
                        await command.ExecuteNonQueryAsync();
                        
                        int processed = (int)(processedParam.Value ?? 0);
                        decimal fines = (decimal)(finesParam.Value ?? 0m);
                        
                        LoadMessage($"Обработано просрочек: {processed}, общая сумма штрафов: {fines:C}");
                        
                        // Показываем просроченные выдачи
                        string query = @"
SELECT l.ВыдачаID, r.Фамилия + ' ' + r.Имя AS Читатель, 
       b.Название AS Книга, l.ДатаВозврата,
       DATEDIFF(DAY, l.ДатаВозврата, GETDATE()) AS ДнейПросрочки,
       dbo.FN_CalculateFine(l.ДатаВозврата, NULL) AS Штраф
FROM Выдачи l
JOIN Читатели r ON l.ЧитательID = r.ЧитательID
JOIN Книги b ON l.КнигаID = b.КнигаID
WHERE l.ДатаФактическогоВозврата IS NULL AND l.ДатаВозврата < GETDATE()";
                        
                        await ExecuteQuery(query, "Просроченные выдачи");
                    }
                }
            }
            catch (Exception ex)
            {
                LoadMessage($"Ошибка при обработке просрочек: {ex.Message}");
            }
        }

        // Транзакции
        private async void BtnSmartReturn_Click(object sender, RoutedEventArgs e)
        {
            string transaction = @"
BEGIN TRANSACTION SmartReturn;
BEGIN TRY
    -- Пример умного возврата для выдачи ID=1
    DECLARE @ВыдачаID INT = 1;
    DECLARE @Штраф DECIMAL(10,2);
    
    -- Проверяем существование выдачи
    IF EXISTS (SELECT 1 FROM Выдачи WHERE ВыдачаID = @ВыдачаID AND ДатаФактическогоВозврата IS NULL)
    BEGIN
        -- Рассчитываем штраф
        SELECT @Штраф = dbo.FN_CalculateFine(ДатаВозврата, GETDATE())
        FROM Выдачи WHERE ВыдачаID = @ВыдачаID;
        
        -- Обновляем выдачу
        UPDATE Выдачи 
        SET ДатаФактическогоВозврата = GETDATE()
        WHERE ВыдачаID = @ВыдачаID;
        
        SELECT 'Книга возвращена' AS Результат, @Штраф AS Штраф;
    END
    ELSE
    BEGIN
        SELECT 'Активная выдача не найдена' AS Результат, 0 AS Штраф;
    END
    
    COMMIT TRANSACTION SmartReturn;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION SmartReturn;
    SELECT ERROR_MESSAGE() AS Ошибка;
END CATCH";

            await ExecuteQuery(transaction, "Транзакция умного возврата");
        }

        private async void BtnMassExtension_Click(object sender, RoutedEventArgs e)
        {
            string transaction = @"
BEGIN TRANSACTION MassExtension;
BEGIN TRY
    -- Продляем срок возврата для всех активных выдач на 7 дней
    UPDATE Выдачи 
    SET ДатаВозврата = DATEADD(DAY, 7, ДатаВозврата)
    WHERE ДатаФактическогоВозврата IS NULL;
    
    SELECT @@ROWCOUNT AS ПродленоВыдач;
    
    COMMIT TRANSACTION MassExtension;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION MassExtension;
    SELECT ERROR_MESSAGE() AS Ошибка;
END CATCH";

            await ExecuteQuery(transaction, "Транзакция массового продления");
        }

        // Триггеры
        private async void BtnViewAudit_Click(object sender, RoutedEventArgs e)
        {
            string query = "SELECT TOP 20 * FROM AuditLog ORDER BY Дата DESC";
            await ExecuteQuery(query, "Просмотр журнала аудита");
        }

        private async void BtnTestTriggers_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Тестируем триггер через попытку некорректной вставки
                string testQuery = @"
-- Тест триггера проверки дат
INSERT INTO Выдачи (ЧитательID, КнигаID, ДатаВыдачи, ДатаВозврата)
VALUES (1, 1, '2024-12-01', '2024-11-01'); -- Некорректные даты";

                await ExecuteNonQuery(testQuery, "Тест триггеров (ожидается ошибка)");
            }
            catch (Exception ex)
            {
                LoadMessage($"Триггер сработал корректно: {ex.Message}");
            }
        }

        // Управление объектами
        private async void BtnCreateObjects_Click(object sender, RoutedEventArgs e)
        {
            await CreateDatabaseObjectsAsync();
        }

        private async Task CreateDatabaseObjectsAsync()
        {
            try
            {
                LoadMessage("Создание объектов базы данных...");
                
                // Создаём каждый объект отдельно для лучшего контроля
                await CreateAuditTableAsync();
                await CreateViewsAsync();
                await CreateFunctionsAsync();
                await CreateProceduresAsync();
                await CreateTriggersAsync();

                LoadMessage("Все объекты базы данных успешно созданы!");
                StatusTextBlock.Text = "Объекты созданы успешно";
            }
            catch (Exception ex)
            {
                LoadMessage($"Ошибка при создании объектов: {ex.Message}");
                MessageBox.Show($"Ошибка при создании объектов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CreateAuditTableAsync()
        {
            try
            {
                string script = @"
IF OBJECT_ID('AuditLog', 'U') IS NULL
CREATE TABLE AuditLog (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    Таблица NVARCHAR(50),
    Операция NVARCHAR(10),
    Данные NVARCHAR(MAX),
    Дата DATETIME2 DEFAULT GETDATE()
);";
                await Task.Run(() => _databaseService.ExecuteNonQuery(script));
                LoadMessage("✓ Таблица аудита создана");
            }
            catch (Exception ex)
            {
                LoadMessage($"✗ Ошибка создания таблицы аудита: {ex.Message}");
            }
        }

        private async Task CreateViewsAsync()
        {
            try
            {
                // Представление популярных книг
                string popularBooksView = @"
IF OBJECT_ID('VW_PopularBooks', 'V') IS NOT NULL DROP VIEW VW_PopularBooks;
";
                await Task.Run(() => _databaseService.ExecuteNonQuery(popularBooksView));

                string createPopularBooks = @"
CREATE VIEW VW_PopularBooks AS
SELECT b.КнигаID, b.Название, 
       COALESCE(a.ФИО, 'Неизвестный автор') AS Автор, 
       COUNT(l.ВыдачаID) AS КоличествоВыдач,
       CASE WHEN COUNT(l.ВыдачаID) >= 5 THEN 'Популярная' ELSE 'Обычная' END AS Категория
FROM Книги b
LEFT JOIN Книги_Авторы ka ON b.КнигаID = ka.КнигаID
LEFT JOIN Авторы a ON ka.АвторID = a.АвторID
LEFT JOIN Выдачи l ON b.КнигаID = l.КнигаID
GROUP BY b.КнигаID, b.Название, a.ФИО;";
                await Task.Run(() => _databaseService.ExecuteNonQuery(createPopularBooks));
                LoadMessage("✓ Представление популярных книг создано");

                // Представление активности читателей
                string readerActivityView = @"
IF OBJECT_ID('VW_ReaderActivity', 'V') IS NOT NULL DROP VIEW VW_ReaderActivity;
";
                await Task.Run(() => _databaseService.ExecuteNonQuery(readerActivityView));

                string createReaderActivity = @"
CREATE VIEW VW_ReaderActivity AS
SELECT r.ЧитательID, r.Фамилия + ' ' + r.Имя AS ПолноеИмя,
       COUNT(l.ВыдачаID) AS ВсегоВыдач,
       SUM(CASE WHEN l.ДатаФактическогоВозврата IS NULL THEN 1 ELSE 0 END) AS АктивныхВыдач
FROM Читатели r
LEFT JOIN Выдачи l ON r.ЧитательID = l.ЧитательID
GROUP BY r.ЧитательID, r.Фамилия, r.Имя;";
                await Task.Run(() => _databaseService.ExecuteNonQuery(createReaderActivity));
                LoadMessage("✓ Представление активности читателей создано");
            }
            catch (Exception ex)
            {
                LoadMessage($"✗ Ошибка создания представлений: {ex.Message}");
            }
        }

        private async Task CreateFunctionsAsync()
        {
            try
            {
                // Функция расчёта штрафа
                string dropCalculateFine = @"
IF OBJECT_ID('FN_CalculateFine', 'FN') IS NOT NULL DROP FUNCTION FN_CalculateFine;
";
                await Task.Run(() => _databaseService.ExecuteNonQuery(dropCalculateFine));

                string createCalculateFine = @"
CREATE FUNCTION FN_CalculateFine(@ДатаВозврата DATE, @ДатаФакта DATE = NULL)
RETURNS DECIMAL(10,2)
AS
BEGIN
    DECLARE @Штраф DECIMAL(10,2) = 0;
    DECLARE @Дни INT = DATEDIFF(DAY, @ДатаВозврата, COALESCE(@ДатаФакта, GETDATE()));
    IF @Дни > 0 SET @Штраф = @Дни * 5.0;
    RETURN @Штраф;
END;";
                await Task.Run(() => _databaseService.ExecuteNonQuery(createCalculateFine));
                LoadMessage("✓ Функция расчёта штрафа создана");

                // Функция проверки читателя
                string dropCheckReader = @"
IF OBJECT_ID('FN_CheckReader', 'FN') IS NOT NULL DROP FUNCTION FN_CheckReader;
";
                await Task.Run(() => _databaseService.ExecuteNonQuery(dropCheckReader));

                string createCheckReader = @"
CREATE FUNCTION FN_CheckReader(@ЧитательID INT)
RETURNS NVARCHAR(100)
AS
BEGIN
    DECLARE @Активных INT;
    SELECT @Активных = COUNT(*) FROM Выдачи WHERE ЧитательID = @ЧитательID AND ДатаФактическогоВозврата IS NULL;
    RETURN CASE WHEN @Активных >= 5 THEN 'ОТКАЗ: Лимит превышен' ELSE 'РАЗРЕШЕНО' END;
END;";
                await Task.Run(() => _databaseService.ExecuteNonQuery(createCheckReader));
                LoadMessage("✓ Функция проверки читателя создана");
            }
            catch (Exception ex)
            {
                LoadMessage($"✗ Ошибка создания функций: {ex.Message}");
            }
        }

        private async Task CreateProceduresAsync()
        {
            try
            {
                // Процедура умной выдачи
                string dropSmartIssue = @"
IF OBJECT_ID('SP_SmartIssue', 'P') IS NOT NULL DROP PROCEDURE SP_SmartIssue;
";
                await Task.Run(() => _databaseService.ExecuteNonQuery(dropSmartIssue));

                string createSmartIssue = @"
CREATE PROCEDURE SP_SmartIssue
    @КнигаID INT, @ЧитательID INT, @Результат NVARCHAR(200) OUTPUT
AS
BEGIN
    DECLARE @Проверка NVARCHAR(100) = dbo.FN_CheckReader(@ЧитательID);
    IF LEFT(@Проверка, 5) = 'ОТКАЗ'
    BEGIN
        SET @Результат = @Проверка;
        RETURN;
    END
    
    INSERT INTO Выдачи (ЧитательID, КнигаID, ДатаВыдачи, ДатаВозврата)
    VALUES (@ЧитательID, @КнигаID, GETDATE(), DATEADD(DAY, 14, GETDATE()));
    
    SET @Результат = 'УСПЕХ: Книга выдана';
END;";
                await Task.Run(() => _databaseService.ExecuteNonQuery(createSmartIssue));
                LoadMessage("✓ Процедура умной выдачи создана");

                // Процедура обработки просрочек
                string dropProcessOverdue = @"
IF OBJECT_ID('SP_ProcessOverdue', 'P') IS NOT NULL DROP PROCEDURE SP_ProcessOverdue;
";
                await Task.Run(() => _databaseService.ExecuteNonQuery(dropProcessOverdue));

                string createProcessOverdue = @"
CREATE PROCEDURE SP_ProcessOverdue
    @Обработано INT OUTPUT, @СуммаШтрафов DECIMAL(15,2) OUTPUT
AS
BEGIN
    SELECT @Обработано = COUNT(*),
           @СуммаШтрафов = SUM(dbo.FN_CalculateFine(ДатаВозврата, NULL))
    FROM Выдачи 
    WHERE ДатаФактическогоВозврата IS NULL AND ДатаВозврата < GETDATE();
END;";
                await Task.Run(() => _databaseService.ExecuteNonQuery(createProcessOverdue));
                LoadMessage("✓ Процедура обработки просрочек создана");
            }
            catch (Exception ex)
            {
                LoadMessage($"✗ Ошибка создания процедур: {ex.Message}");
            }
        }

        private async Task CreateTriggersAsync()
        {
            try
            {
                // Триггер аудита (упрощённая версия без INSTEAD OF)
                string dropAuditTrigger = @"
IF OBJECT_ID('TR_AuditLoans', 'TR') IS NOT NULL DROP TRIGGER TR_AuditLoans;
";
                await Task.Run(() => _databaseService.ExecuteNonQuery(dropAuditTrigger));

                string createAuditTrigger = @"
CREATE TRIGGER TR_AuditLoans
ON Выдачи
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Операция NVARCHAR(10);
    
    IF EXISTS(SELECT * FROM inserted) AND EXISTS(SELECT * FROM deleted)
        SET @Операция = 'UPDATE';
    ELSE IF EXISTS(SELECT * FROM inserted)
        SET @Операция = 'INSERT';
    ELSE
        SET @Операция = 'DELETE';
    
    INSERT INTO AuditLog (Таблица, Операция, Данные)
    VALUES ('Выдачи', @Операция, 'Операция выполнена: ' + @Операция);
END;";
                await Task.Run(() => _databaseService.ExecuteNonQuery(createAuditTrigger));
                LoadMessage("✓ Триггер аудита создан");

                LoadMessage("✓ Триггеры созданы (упрощённая версия для совместимости)");
            }
            catch (Exception ex)
            {
                LoadMessage($"✗ Ошибка создания триггеров: {ex.Message}");
            }
        }

        private async void BtnDropObjects_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Вы уверены, что хотите удалить все созданные объекты базы данных?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                string dropScript = @"
-- Удаление объектов
IF OBJECT_ID('TR_CheckLoan', 'TR') IS NOT NULL DROP TRIGGER TR_CheckLoan;
IF OBJECT_ID('TR_AuditLoans', 'TR') IS NOT NULL DROP TRIGGER TR_AuditLoans;
IF OBJECT_ID('SP_ProcessOverdue', 'P') IS NOT NULL DROP PROCEDURE SP_ProcessOverdue;
IF OBJECT_ID('SP_SmartIssue', 'P') IS NOT NULL DROP PROCEDURE SP_SmartIssue;
IF OBJECT_ID('FN_CheckReader', 'FN') IS NOT NULL DROP FUNCTION FN_CheckReader;
IF OBJECT_ID('FN_CalculateFine', 'FN') IS NOT NULL DROP FUNCTION FN_CalculateFine;
IF OBJECT_ID('VW_ReaderActivity', 'V') IS NOT NULL DROP VIEW VW_ReaderActivity;
IF OBJECT_ID('VW_PopularBooks', 'V') IS NOT NULL DROP VIEW VW_PopularBooks;
IF OBJECT_ID('AuditLog', 'U') IS NOT NULL DROP TABLE AuditLog;";

                await ExecuteNonQuery(dropScript, "Удаление объектов базы данных");
            }
        }
    }
} 