-- ===============================================
-- СЛОЖНЫЕ ОБЪЕКТЫ БАЗЫ ДАННЫХ БИБЛИОТЕКИ
-- ===============================================

-- ===============================================
-- 1. ПРЕДСТАВЛЕНИЯ (VIEWS)
-- ===============================================

-- Представление 1: Детальная информация о популярных книгах
IF OBJECT_ID('dbo.VW_PopularBooksDetailed', 'V') IS NOT NULL
    DROP VIEW dbo.VW_PopularBooksDetailed;
GO

CREATE VIEW dbo.VW_PopularBooksDetailed
AS
SELECT 
    b.КнигаID,
    b.Название,
    a.ФИО AS АвторИмя,
    g.Название AS ЖанрНазвание,
    p.Название AS ИздательствоНазвание,
    b.ГодИздания,
    COUNT(l.ВыдачаID) AS КоличествоВыдач,
    AVG(CAST(DATEDIFF(day, l.ДатаВыдачи, COALESCE(l.ДатаФактическогоВозврата, l.ДатаВозврата)) AS FLOAT)) AS СреднийСрокЧтения,
    MAX(l.ДатаВыдачи) AS ПоследняяВыдача,
    CASE 
        WHEN COUNT(l.ВыдачаID) >= 10 THEN 'Очень популярная'
        WHEN COUNT(l.ВыдачаID) >= 5 THEN 'Популярная'
        WHEN COUNT(l.ВыдачаID) >= 1 THEN 'Средняя популярность'
        ELSE 'Непопулярная'
    END AS КатегорияПопулярности,
    SUM(CASE WHEN l.ДатаФактическогоВозврата IS NULL AND l.ДатаВозврата < CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS КоличествоПросрочек
FROM Книги b
LEFT JOIN Авторы a ON b.АвторID = a.АвторID
LEFT JOIN Жанры g ON b.ЖанрID = g.ЖанрID
LEFT JOIN Издательства p ON b.ИздательствоID = p.ИздательствоID
LEFT JOIN Выдачи l ON b.КнигаID = l.КнигаID
GROUP BY b.КнигаID, b.Название, a.ФИО, g.Название, p.Название, b.ГодИздания;
GO

-- Представление 2: Аналитика читательской активности
IF OBJECT_ID('dbo.VW_ReaderAnalytics', 'V') IS NOT NULL
    DROP VIEW dbo.VW_ReaderAnalytics;
GO

CREATE VIEW dbo.VW_ReaderAnalytics
AS
SELECT 
    r.ЧитательID,
    r.Фамилия + ' ' + r.Имя + ' ' + ISNULL(r.Отчество, '') AS ПолноеИмя,
    r.ДатаРождения,
    DATEDIFF(YEAR, r.ДатаРождения, GETDATE()) AS Возраст,
    CASE 
        WHEN DATEDIFF(YEAR, r.ДатаРождения, GETDATE()) < 18 THEN 'Несовершеннолетний'
        WHEN DATEDIFF(YEAR, r.ДатаРождения, GETDATE()) BETWEEN 18 AND 30 THEN 'Молодёжь'
        WHEN DATEDIFF(YEAR, r.ДатаРождения, GETDATE()) BETWEEN 31 AND 50 THEN 'Средний возраст'
        ELSE 'Пожилой'
    END AS ВозрастнаяКатегория,
    COUNT(l.ВыдачаID) AS ОбщееКоличествоВыдач,
    SUM(CASE WHEN l.ДатаФактическогоВозврата IS NULL THEN 1 ELSE 0 END) AS АктивныхВыдач,
    SUM(CASE WHEN l.ДатаФактическогоВозврата IS NULL AND l.ДатаВозврата < CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS ПросроченныхВыдач,
    MAX(l.ДатаВыдачи) AS ПоследняяАктивность,
    DATEDIFF(DAY, MAX(l.ДатаВыдачи), GETDATE()) AS ДнейСПоследнейАктивности,
    CASE 
        WHEN COUNT(l.ВыдачаID) >= 20 THEN 'Очень активный'
        WHEN COUNT(l.ВыдачаID) >= 10 THEN 'Активный'
        WHEN COUNT(l.ВыдачаID) >= 5 THEN 'Умеренно активный'
        WHEN COUNT(l.ВыдачаID) >= 1 THEN 'Малоактивный'
        ELSE 'Неактивный'
    END AS УровеньАктивности,
    AVG(CAST(DATEDIFF(day, l.ДатаВыдачи, COALESCE(l.ДатаФактическогоВозврата, GETDATE())) AS FLOAT)) AS СреднийСрокПользования
FROM Читатели r
LEFT JOIN Выдачи l ON r.ЧитательID = l.ЧитательID
GROUP BY r.ЧитательID, r.Фамилия, r.Имя, r.Отчество, r.ДатаРождения;
GO

-- ===============================================
-- 2. ФУНКЦИИ (FUNCTIONS)
-- ===============================================

-- Функция 1: Расчёт штрафа за просрочку
IF OBJECT_ID('dbo.FN_CalculateFine', 'FN') IS NOT NULL
    DROP FUNCTION dbo.FN_CalculateFine;
GO

CREATE FUNCTION dbo.FN_CalculateFine(
    @ДатаВозврата DATE,
    @ДатаФактическогоВозврата DATE = NULL,
    @СтавкаШтрафа DECIMAL(10,2) = 5.0
)
RETURNS DECIMAL(10,2)
AS
BEGIN
    DECLARE @Штраф DECIMAL(10,2) = 0;
    DECLARE @ДнейПросрочки INT;
    
    -- Если книга не возвращена, считаем от текущей даты
    DECLARE @КонтрольнаяДата DATE = COALESCE(@ДатаФактическогоВозврата, CAST(GETDATE() AS DATE));
    
    -- Вычисляем количество дней просрочки
    SET @ДнейПросрочки = DATEDIFF(DAY, @ДатаВозврата, @КонтрольнаяДата);
    
    -- Если есть просрочка, рассчитываем штраф
    IF @ДнейПросрочки > 0
    BEGIN
        SET @Штраф = @ДнейПросрочки * @СтавкаШтрафа;
        
        -- Прогрессивная шкала штрафов
        IF @ДнейПросрочки > 30
            SET @Штраф = @Штраф * 1.5; -- +50% за просрочку более месяца
        
        IF @ДнейПросрочки > 60
            SET @Штраф = @Штраф * 1.3; -- ещё +30% за просрочку более 2 месяцев
    END
    
    RETURN @Штраф;
END;
GO

-- Функция 2: Проверка доступности читателя для новой выдачи
IF OBJECT_ID('dbo.FN_CheckReaderEligibility', 'FN') IS NOT NULL
    DROP FUNCTION dbo.FN_CheckReaderEligibility;
GO

CREATE FUNCTION dbo.FN_CheckReaderEligibility(
    @ЧитательID INT,
    @МаксимумВыдач INT = 5,
    @МаксимумПросрочек INT = 2
)
RETURNS NVARCHAR(200)
AS
BEGIN
    DECLARE @Результат NVARCHAR(200);
    DECLARE @АктивныхВыдач INT;
    DECLARE @ПросроченныхВыдач INT;
    DECLARE @ОбщийШтраф DECIMAL(10,2);
    
    -- Подсчёт активных выдач
    SELECT @АктивныхВыдач = COUNT(*)
    FROM Выдачи 
    WHERE ЧитательID = @ЧитательID AND ДатаФактическогоВозврата IS NULL;
    
    -- Подсчёт просроченных выдач
    SELECT @ПросроченныхВыдач = COUNT(*)
    FROM Выдачи 
    WHERE ЧитательID = @ЧитательID 
        AND ДатаФактическогоВозврата IS NULL 
        AND ДатаВозврата < CAST(GETDATE() AS DATE);
    
    -- Подсчёт общего штрафа
    SELECT @ОбщийШтраф = SUM(dbo.FN_CalculateFine(ДатаВозврата, ДатаФактическогоВозврата))
    FROM Выдачи 
    WHERE ЧитательID = @ЧитательID AND ДатаФактическогоВозврата IS NULL;
    
    -- Проверка условий
    IF @АктивныхВыдач >= @МаксимумВыдач
        SET @Результат = 'ОТКАЗ: Превышен лимит активных выдач (' + CAST(@АктивныхВыдач AS NVARCHAR(10)) + '/' + CAST(@МаксимумВыдач AS NVARCHAR(10)) + ')';
    ELSE IF @ПросроченныхВыдач > @МаксимумПросрочек
        SET @Результат = 'ОТКАЗ: Слишком много просроченных книг (' + CAST(@ПросроченныхВыдач AS NVARCHAR(10)) + ')';
    ELSE IF @ОбщийШтраф > 100.0
        SET @Результат = 'ОТКАЗ: Большая сумма штрафа (' + CAST(@ОбщийШтраф AS NVARCHAR(20)) + ' руб.)';
    ELSE
        SET @Результат = 'РАЗРЕШЕНО: Активных=' + CAST(@АктивныхВыдач AS NVARCHAR(10)) + ', Просрочек=' + CAST(@ПросроченныхВыдач AS NVARCHAR(10)) + ', Штраф=' + CAST(ISNULL(@ОбщийШтраф, 0) AS NVARCHAR(20)) + ' руб.';
    
    RETURN @Результат;
END;
GO

-- ===============================================
-- 3. ХРАНИМЫЕ ПРОЦЕДУРЫ (STORED PROCEDURES)
-- ===============================================

-- Процедура 1: Умная выдача книги с проверками
IF OBJECT_ID('dbo.SP_SmartBookIssue', 'P') IS NOT NULL
    DROP PROCEDURE dbo.SP_SmartBookIssue;
GO

CREATE PROCEDURE dbo.SP_SmartBookIssue
(
    @КнигаID INT,
    @ЧитательID INT,
    @СрокВозврата INT = 14, -- дней
    @Результат NVARCHAR(500) OUTPUT,
    @НоваяВыдачаID INT OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @ПроверкаЧитателя NVARCHAR(200);
    DECLARE @КоличествоЭкземпляров INT;
    DECLARE @АктивныхВыдачЭтойКниги INT;
    DECLARE @ДатаВыдачи DATE = CAST(GETDATE() AS DATE);
    DECLARE @ДатаВозврата DATE = DATEADD(DAY, @СрокВозврата, @ДатаВыдачи);
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- 1. Проверка существования книги
        IF NOT EXISTS (SELECT 1 FROM Книги WHERE КнигаID = @КнигаID)
        BEGIN
            SET @Результат = 'ОШИБКА: Книга с ID ' + CAST(@КнигаID AS NVARCHAR(10)) + ' не найдена';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- 2. Проверка существования читателя
        IF NOT EXISTS (SELECT 1 FROM Читатели WHERE ЧитательID = @ЧитательID)
        BEGIN
            SET @Результат = 'ОШИБКА: Читатель с ID ' + CAST(@ЧитательID AS NVARCHAR(10)) + ' не найден';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- 3. Проверка права читателя на выдачу
        SET @ПроверкаЧитателя = dbo.FN_CheckReaderEligibility(@ЧитательID);
        IF LEFT(@ПроверкаЧитателя, 5) = 'ОТКАЗ'
        BEGIN
            SET @Результат = @ПроверкаЧитателя;
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- 4. Проверка доступности книги (если есть информация о количестве экземпляров)
        -- Предполагаем, что у нас есть поле КоличествоЭкземпляров в таблице Книги
        SELECT @КоличествоЭкземпляров = ISNULL(1, 1) FROM Книги WHERE КнигаID = @КнигаID; -- по умолчанию 1
        
        SELECT @АктивныхВыдачЭтойКниги = COUNT(*)
        FROM Выдачи 
        WHERE КнигаID = @КнигаID AND ДатаФактическогоВозврата IS NULL;
        
        IF @АктивныхВыдачЭтойКниги >= @КоличествоЭкземпляров
        BEGIN
            SET @Результат = 'ОТКАЗ: Все экземпляры книги уже выданы (' + CAST(@АктивныхВыдачЭтойКниги AS NVARCHAR(10)) + '/' + CAST(@КоличествоЭкземпляров AS NVARCHAR(10)) + ')';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- 5. Проверка на повторную выдачу той же книги
        IF EXISTS (
            SELECT 1 FROM Выдачи 
            WHERE КнигаID = @КнигаID 
                AND ЧитательID = @ЧитательID 
                AND ДатаФактическогоВозврата IS NULL
        )
        BEGIN
            SET @Результат = 'ОТКАЗ: Эта книга уже выдана данному читателю';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- 6. Создание записи о выдаче
        INSERT INTO Выдачи (ЧитательID, КнигаID, ДатаВыдачи, ДатаВозврата)
        VALUES (@ЧитательID, @КнигаID, @ДатаВыдачи, @ДатаВозврата);
        
        SET @НоваяВыдачаID = SCOPE_IDENTITY();
        
        -- 7. Формирование успешного результата
        DECLARE @НазваниеКниги NVARCHAR(200);
        DECLARE @ИмяЧитателя NVARCHAR(200);
        
        SELECT @НазваниеКниги = Название FROM Книги WHERE КнигаID = @КнигаID;
        SELECT @ИмяЧитателя = Фамилия + ' ' + Имя FROM Читатели WHERE ЧитательID = @ЧитательID;
        
        SET @Результат = 'УСПЕХ: Книга "' + @НазваниеКниги + '" выдана читателю ' + @ИмяЧитателя + 
                         '. Срок возврата: ' + CONVERT(NVARCHAR(10), @ДатаВозврата, 104) + 
                         '. ID выдачи: ' + CAST(@НоваяВыдачаID AS NVARCHAR(10));
        
        COMMIT TRANSACTION;
        
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        SET @Результат = 'ОШИБКА: ' + ERROR_MESSAGE();
    END CATCH
END;
GO

-- Процедура 2: Массовая обработка просроченных выдач
IF OBJECT_ID('dbo.SP_ProcessOverdueLoans', 'P') IS NOT NULL
    DROP PROCEDURE dbo.SP_ProcessOverdueLoans;
GO

CREATE PROCEDURE dbo.SP_ProcessOverdueLoans
(
    @ДнейПросрочки INT = 1,
    @ОтправитьУведомления BIT = 1,
    @РассчитатьШтрафы BIT = 1,
    @ОбработаноВыдач INT OUTPUT,
    @ОбщаяСуммаШтрафов DECIMAL(15,2) OUTPUT,
    @СписокПросрочекXML XML OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @ПросроченныеВыдачи TABLE (
        ВыдачаID INT,
        ЧитательID INT,
        КнигаID INT,
        ИмяЧитателя NVARCHAR(200),
        НазваниеКниги NVARCHAR(200),
        ТелефонЧитателя NVARCHAR(20),
        ДатаВыдачи DATE,
        ДатаВозврата DATE,
        ДнейПросрочки INT,
        СуммаШтрафа DECIMAL(10,2)
    );
    
    BEGIN TRY
        -- 1. Поиск просроченных выдач
        INSERT INTO @ПросроченныеВыдачи
        SELECT 
            l.ВыдачаID,
            l.ЧитательID,
            l.КнигаID,
            r.Фамилия + ' ' + r.Имя + ' ' + ISNULL(r.Отчество, '') AS ИмяЧитателя,
            b.Название AS НазваниеКниги,
            r.Телефон AS ТелефонЧитателя,
            l.ДатаВыдачи,
            l.ДатаВозврата,
            DATEDIFF(DAY, l.ДатаВозврата, CAST(GETDATE() AS DATE)) AS ДнейПросрочки,
            dbo.FN_CalculateFine(l.ДатаВозврата, NULL) AS СуммаШтрафа
        FROM Выдачи l
        INNER JOIN Читатели r ON l.ЧитательID = r.ЧитательID
        INNER JOIN Книги b ON l.КнигаID = b.КнигаID
        WHERE l.ДатаФактическогоВозврата IS NULL
            AND DATEDIFF(DAY, l.ДатаВозврата, CAST(GETDATE() AS DATE)) >= @ДнейПросрочки;
        
        -- 2. Подсчёт статистики
        SELECT 
            @ОбработаноВыдач = COUNT(*),
            @ОбщаяСуммаШтрафов = SUM(СуммаШтрафа)
        FROM @ПросроченныеВыдачи;
        
        -- 3. Создание XML отчёта
        SELECT @СписокПросрочекXML = (
            SELECT 
                ВыдачаID AS [@id],
                ИмяЧитателя AS [Читатель],
                ТелефонЧитателя AS [Телефон],
                НазваниеКниги AS [Книга],
                ДатаВозврата AS [СрокВозврата],
                ДнейПросрочки AS [ДнейПросрочки],
                СуммаШтрафа AS [Штраф]
            FROM @ПросроченныеВыдачи
            FOR XML PATH('Просрочка'), ROOT('ПросроченныеВыдачи')
        );
        
        -- 4. Логирование результатов (можно создать таблицу логов)
        DECLARE @СообщениеЛога NVARCHAR(500);
        SET @СообщениеЛога = 'Обработано просроченных выдач: ' + CAST(@ОбработаноВыдач AS NVARCHAR(10)) + 
                            ', общая сумма штрафов: ' + CAST(@ОбщаяСуммаШтрафов AS NVARCHAR(20)) + ' руб.';
        
        -- Здесь можно добавить вставку в таблицу логов
        PRINT @СообщениеЛога;
        
    END TRY
    BEGIN CATCH
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        PRINT 'Ошибка в процедуре SP_ProcessOverdueLoans: ' + @ErrorMessage;
        THROW;
    END CATCH
END;
GO

-- ===============================================
-- 4. ТРИГГЕРЫ (TRIGGERS)
-- ===============================================

-- Триггер 1: Аудит изменений в таблице Выдачи
IF OBJECT_ID('dbo.TR_LoanAudit', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_LoanAudit;
GO

-- Сначала создадим таблицу аудита, если её нет
IF OBJECT_ID('dbo.AuditLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLog (
        ЛогID INT IDENTITY(1,1) PRIMARY KEY,
        ТаблицаИмя NVARCHAR(50) NOT NULL,
        ОперацияТип NVARCHAR(10) NOT NULL,
        СтарыеЗначения NVARCHAR(MAX),
        НовыеЗначения NVARCHAR(MAX),
        ДатаВремя DATETIME2 DEFAULT GETDATE(),
        Пользователь NVARCHAR(100) DEFAULT SYSTEM_USER,
        ЗатронутыйID INT
    );
END;
GO

CREATE TRIGGER dbo.TR_LoanAudit
ON dbo.Выдачи
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Операция NVARCHAR(10);
    
    -- Определение типа операции
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @Операция = 'UPDATE';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @Операция = 'INSERT';
    ELSE
        SET @Операция = 'DELETE';
    
    -- Логирование INSERT
    IF @Операция = 'INSERT'
    BEGIN
        INSERT INTO dbo.AuditLog (ТаблицаИмя, ОперацияТип, НовыеЗначения, ЗатронутыйID)
        SELECT 
            'Выдачи',
            'INSERT',
            'ВыдачаID=' + CAST(ВыдачаID AS NVARCHAR(10)) + 
            ', ЧитательID=' + CAST(ЧитательID AS NVARCHAR(10)) + 
            ', КнигаID=' + CAST(КнигаID AS NVARCHAR(10)) + 
            ', ДатаВыдачи=' + CONVERT(NVARCHAR(10), ДатаВыдачи, 104) + 
            ', ДатаВозврата=' + CONVERT(NVARCHAR(10), ДатаВозврата, 104),
            ВыдачаID
        FROM inserted;
    END
    
    -- Логирование UPDATE
    IF @Операция = 'UPDATE'
    BEGIN
        INSERT INTO dbo.AuditLog (ТаблицаИмя, ОперацияТип, СтарыеЗначения, НовыеЗначения, ЗатронутыйID)
        SELECT 
            'Выдачи',
            'UPDATE',
            'ВыдачаID=' + CAST(d.ВыдачаID AS NVARCHAR(10)) + 
            ', ЧитательID=' + CAST(d.ЧитательID AS NVARCHAR(10)) + 
            ', КнигаID=' + CAST(d.КнигаID AS NVARCHAR(10)) + 
            ', ДатаВыдачи=' + CONVERT(NVARCHAR(10), d.ДатаВыдачи, 104) + 
            ', ДатаВозврата=' + CONVERT(NVARCHAR(10), d.ДатаВозврата, 104) +
            ', ДатаФактическогоВозврата=' + ISNULL(CONVERT(NVARCHAR(10), d.ДатаФактическогоВозврата, 104), 'NULL'),
            'ВыдачаID=' + CAST(i.ВыдачаID AS NVARCHAR(10)) + 
            ', ЧитательID=' + CAST(i.ЧитательID AS NVARCHAR(10)) + 
            ', КнигаID=' + CAST(i.КнигаID AS NVARCHAR(10)) + 
            ', ДатаВыдачи=' + CONVERT(NVARCHAR(10), i.ДатаВыдачи, 104) + 
            ', ДатаВозврата=' + CONVERT(NVARCHAR(10), i.ДатаВозврата, 104) +
            ', ДатаФактическогоВозврата=' + ISNULL(CONVERT(NVARCHAR(10), i.ДатаФактическогоВозврата, 104), 'NULL'),
            i.ВыдачаID
        FROM deleted d
        INNER JOIN inserted i ON d.ВыдачаID = i.ВыдачаID;
    END
    
    -- Логирование DELETE
    IF @Операция = 'DELETE'
    BEGIN
        INSERT INTO dbo.AuditLog (ТаблицаИмя, ОперацияТип, СтарыеЗначения, ЗатронутыйID)
        SELECT 
            'Выдачи',
            'DELETE',
            'ВыдачаID=' + CAST(ВыдачаID AS NVARCHAR(10)) + 
            ', ЧитательID=' + CAST(ЧитательID AS NVARCHAR(10)) + 
            ', КнигаID=' + CAST(КнигаID AS NVARCHAR(10)) + 
            ', ДатаВыдачи=' + CONVERT(NVARCHAR(10), ДатаВыдачи, 104) + 
            ', ДатаВозврата=' + CONVERT(NVARCHAR(10), ДатаВозврата, 104),
            ВыдачаID
        FROM deleted;
    END
END;
GO

-- Триггер 2: Проверка бизнес-правил при выдаче книг
IF OBJECT_ID('dbo.TR_LoanBusinessRules', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_LoanBusinessRules;
GO

CREATE TRIGGER dbo.TR_LoanBusinessRules
ON dbo.Выдачи
INSTEAD OF INSERT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @ЧитательID INT, @КнигаID INT, @ДатаВыдачи DATE, @ДатаВозврата DATE;
    DECLARE @ПроверкаРезультат NVARCHAR(200);
    DECLARE @АктивныхВыдач INT;
    
    -- Курсор для обработки множественных вставок
    DECLARE loan_cursor CURSOR FOR
    SELECT ЧитательID, КнигаID, ДатаВыдачи, ДатаВозврата
    FROM inserted;
    
    OPEN loan_cursor;
    FETCH NEXT FROM loan_cursor INTO @ЧитательID, @КнигаID, @ДатаВыдачи, @ДатаВозврата;
    
    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- 1. Проверка корректности дат
        IF @ДатаВыдачи > @ДатаВозврата
        BEGIN
            RAISERROR('Дата возврата не может быть раньше даты выдачи', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        IF @ДатаВыдачи > CAST(GETDATE() AS DATE)
        BEGIN
            RAISERROR('Дата выдачи не может быть в будущем', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- 2. Проверка максимального срока выдачи (например, 60 дней)
        IF DATEDIFF(DAY, @ДатаВыдачи, @ДатаВозврата) > 60
        BEGIN
            RAISERROR('Максимальный срок выдачи - 60 дней', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- 3. Проверка права читателя на выдачу
        SET @ПроверкаРезультат = dbo.FN_CheckReaderEligibility(@ЧитательID);
        IF LEFT(@ПроверкаРезультат, 5) = 'ОТКАЗ'
        BEGIN
            RAISERROR('Выдача запрещена: %s', 16, 1, @ПроверкаРезультат);
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- 4. Проверка на повторную выдачу той же книги
        IF EXISTS (
            SELECT 1 FROM Выдачи 
            WHERE КнигаID = @КнигаID 
                AND ЧитательID = @ЧитательID 
                AND ДатаФактическогоВозврата IS NULL
        )
        BEGIN
            RAISERROR('Эта книга уже выдана данному читателю', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- 5. Если все проверки пройдены, вставляем запись
        INSERT INTO Выдачи (ЧитательID, КнигаID, ДатаВыдачи, ДатаВозврата)
        VALUES (@ЧитательID, @КнигаID, @ДатаВыдачи, @ДатаВозврата);
        
        FETCH NEXT FROM loan_cursor INTO @ЧитательID, @КнигаID, @ДатаВыдачи, @ДатаВозврата;
    END
    
    CLOSE loan_cursor;
    DEALLOCATE loan_cursor;
END;
GO

-- ===============================================
-- 5. ИНДЕКСЫ ДЛЯ ОПТИМИЗАЦИИ
-- ===============================================

-- Индекс для быстрого поиска активных выдач читателя
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Выдачи_ЧитательID_Активные')
    CREATE NONCLUSTERED INDEX IX_Выдачи_ЧитательID_Активные
    ON dbo.Выдачи (ЧитательID, ДатаФактическогоВозврата)
    INCLUDE (КнигаID, ДатаВыдачи, ДатаВозврата);

-- Индекс для быстрого поиска просроченных выдач
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Выдачи_Просроченные')
    CREATE NONCLUSTERED INDEX IX_Выдачи_Просроченные
    ON dbo.Выдачи (ДатаВозврата, ДатаФактическогоВозврата)
    WHERE ДатаФактическогоВозврата IS NULL;

-- Индекс для аудита
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditLog_ДатаВремя')
    CREATE NONCLUSTERED INDEX IX_AuditLog_ДатаВремя
    ON dbo.AuditLog (ДатаВремя DESC);

PRINT 'Все сложные объекты базы данных успешно созданы!';
PRINT '✅ Представления: VW_PopularBooksDetailed, VW_ReaderAnalytics';
PRINT '✅ Функции: FN_CalculateFine, FN_CheckReaderEligibility';  
PRINT '✅ Процедуры: SP_SmartBookIssue, SP_ProcessOverdueLoans';
PRINT '✅ Триггеры: TR_LoanAudit, TR_LoanBusinessRules';
PRINT '✅ Индексы: IX_Выдачи_ЧитательID_Активные, IX_Выдачи_Просроченные, IX_AuditLog_ДатаВремя'; 