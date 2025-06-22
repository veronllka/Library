-- СЛОЖНЫЕ ОБЪЕКТЫ БД БИБЛИОТЕКИ

-- 1. ПРЕДСТАВЛЕНИЯ
-- Представление популярных книг
IF OBJECT_ID('VW_PopularBooks', 'V') IS NOT NULL DROP VIEW VW_PopularBooks;
GO
CREATE VIEW VW_PopularBooks AS
SELECT b.КнигаID, b.Название, a.ФИО AS Автор, 
       COUNT(l.ВыдачаID) AS КоличествоВыдач,
       CASE WHEN COUNT(l.ВыдачаID) >= 5 THEN 'Популярная' ELSE 'Обычная' END AS Категория
FROM Книги b
LEFT JOIN Авторы a ON b.АвторID = a.АвторID
LEFT JOIN Выдачи l ON b.КнигаID = l.КнигаID
GROUP BY b.КнигаID, b.Название, a.ФИО;
GO

-- Представление читательской активности
IF OBJECT_ID('VW_ReaderActivity', 'V') IS NOT NULL DROP VIEW VW_ReaderActivity;
GO
CREATE VIEW VW_ReaderActivity AS
SELECT r.ЧитательID, r.Фамилия + ' ' + r.Имя AS ПолноеИмя,
       COUNT(l.ВыдачаID) AS ВсегоВыдач,
       SUM(CASE WHEN l.ДатаФактическогоВозврата IS NULL THEN 1 ELSE 0 END) AS АктивныхВыдач
FROM Читатели r
LEFT JOIN Выдачи l ON r.ЧитательID = l.ЧитательID
GROUP BY r.ЧитательID, r.Фамилия, r.Имя;
GO

-- 2. ФУНКЦИИ
-- Функция расчёта штрафа
IF OBJECT_ID('FN_CalculateFine', 'FN') IS NOT NULL DROP FUNCTION FN_CalculateFine;
GO
CREATE FUNCTION FN_CalculateFine(@ДатаВозврата DATE, @ДатаФакта DATE = NULL)
RETURNS DECIMAL(10,2)
AS
BEGIN
    DECLARE @Штраф DECIMAL(10,2) = 0;
    DECLARE @Дни INT = DATEDIFF(DAY, @ДатаВозврата, COALESCE(@ДатаФакта, GETDATE()));
    IF @Дни > 0 SET @Штраф = @Дни * 5.0;
    RETURN @Штраф;
END;
GO

-- Функция проверки читателя
IF OBJECT_ID('FN_CheckReader', 'FN') IS NOT NULL DROP FUNCTION FN_CheckReader;
GO
CREATE FUNCTION FN_CheckReader(@ЧитательID INT)
RETURNS NVARCHAR(100)
AS
BEGIN
    DECLARE @Активных INT;
    SELECT @Активных = COUNT(*) FROM Выдачи WHERE ЧитательID = @ЧитательID AND ДатаФактическогоВозврата IS NULL;
    RETURN CASE WHEN @Активных >= 5 THEN 'ОТКАЗ: Лимит превышен' ELSE 'РАЗРЕШЕНО' END;
END;
GO

-- 3. ХРАНИМЫЕ ПРОЦЕДУРЫ
-- Процедура умной выдачи
IF OBJECT_ID('SP_SmartIssue', 'P') IS NOT NULL DROP PROCEDURE SP_SmartIssue;
GO
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
END;
GO

-- Процедура обработки просрочек
IF OBJECT_ID('SP_ProcessOverdue', 'P') IS NOT NULL DROP PROCEDURE SP_ProcessOverdue;
GO
CREATE PROCEDURE SP_ProcessOverdue
    @Обработано INT OUTPUT, @СуммаШтрафов DECIMAL(15,2) OUTPUT
AS
BEGIN
    SELECT @Обработано = COUNT(*),
           @СуммаШтрафов = SUM(dbo.FN_CalculateFine(ДатаВозврата, NULL))
    FROM Выдачи 
    WHERE ДатаФактическогоВозврата IS NULL AND ДатаВозврата < GETDATE();
END;
GO

-- 4. ТАБЛИЦА АУДИТА
IF OBJECT_ID('AuditLog', 'U') IS NULL
CREATE TABLE AuditLog (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    Таблица NVARCHAR(50),
    Операция NVARCHAR(10),
    Данные NVARCHAR(MAX),
    Дата DATETIME2 DEFAULT GETDATE()
);
GO

-- 5. ТРИГГЕРЫ
-- Триггер аудита выдач
IF OBJECT_ID('TR_AuditLoans', 'TR') IS NOT NULL DROP TRIGGER TR_AuditLoans;
GO
CREATE TRIGGER TR_AuditLoans ON Выдачи
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    DECLARE @Операция NVARCHAR(10);
    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
        SET @Операция = 'UPDATE';
    ELSE IF EXISTS (SELECT * FROM inserted)
        SET @Операция = 'INSERT';
    ELSE
        SET @Операция = 'DELETE';
    
    INSERT INTO AuditLog (Таблица, Операция, Данные)
    VALUES ('Выдачи', @Операция, 'Изменение в таблице выдач');
END;
GO

-- Триггер проверки выдач
IF OBJECT_ID('TR_CheckLoan', 'TR') IS NOT NULL DROP TRIGGER TR_CheckLoan;
GO
CREATE TRIGGER TR_CheckLoan ON Выдачи
INSTEAD OF INSERT
AS
BEGIN
    DECLARE @ЧитательID INT, @КнигаID INT, @ДатаВыдачи DATE, @ДатаВозврата DATE;
    SELECT @ЧитательID = ЧитательID, @КнигаID = КнигаID, 
           @ДатаВыдачи = ДатаВыдачи, @ДатаВозврата = ДатаВозврата
    FROM inserted;
    
    IF @ДатаВыдачи > @ДатаВозврата
    BEGIN
        RAISERROR('Некорректные даты', 16, 1);
        RETURN;
    END
    
    INSERT INTO Выдачи (ЧитательID, КнигаID, ДатаВыдачи, ДатаВозврата)
    VALUES (@ЧитательID, @КнигаID, @ДатаВыдачи, @ДатаВозврата);
END;
GO

PRINT 'Все объекты созданы успешно!'; 