-- ===============================================
-- СЛОЖНЫЕ ТРАНЗАКЦИИ
-- ===============================================

-- ===============================================
-- ТРАНЗАКЦИЯ 1: Умный возврат книги с расчётом штрафов
-- ===============================================

-- Пример использования транзакции 1
BEGIN
    DECLARE @ВыдачаID INT = 1; -- ID возвращаемой выдачи
    DECLARE @ДатаФактическогоВозврата DATE = CAST(GETDATE() AS DATE);
    DECLARE @Штраф DECIMAL(10,2);
    DECLARE @ДнейПросрочки INT;
    DECLARE @ErrorMessage NVARCHAR(4000);

    BEGIN TRY
        BEGIN TRANSACTION SmartBookReturn;
        
        -- 1. Проверка существования активной выдачи
        IF NOT EXISTS (
            SELECT 1 FROM Выдачи 
            WHERE ВыдачаID = @ВыдачаID AND ДатаФактическогоВозврата IS NULL
        )
        BEGIN
            RAISERROR('Выдача с ID %d не найдена или уже закрыта', 16, 1, @ВыдачаID);
        END
        
        -- 2. Получение информации о выдаче
        DECLARE @ЧитательID INT, @КнигаID INT, @ДатаВозврата DATE;
        
        SELECT 
            @ЧитательID = ЧитательID,
            @КнигаID = КнигаID,
            @ДатаВозврата = ДатаВозврата
        FROM Выдачи 
        WHERE ВыдачаID = @ВыдачаID;
        
        -- 3. Расчёт штрафа
        SET @Штраф = dbo.FN_CalculateFine(@ДатаВозврата, @ДатаФактическогоВозврата);
        SET @ДнейПросрочки = CASE 
            WHEN @ДатаФактическогоВозврата > @ДатаВозврата 
            THEN DATEDIFF(DAY, @ДатаВозврата, @ДатаФактическогоВозврата)
            ELSE 0 
        END;
        
        -- 4. Обновление записи о выдаче
        UPDATE Выдачи 
        SET 
            ДатаФактическогоВозврата = @ДатаФактическогоВозврата,
            СуммаШтрафа = @Штраф
        WHERE ВыдачаID = @ВыдачаID;
        
        -- 5. Если есть штраф, создаём запись в таблице штрафов (если такая есть)
        IF @Штраф > 0
        BEGIN
            -- Создаём таблицу штрафов, если её нет
            IF OBJECT_ID('dbo.Штрафы', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Штрафы (
                    ШтрафID INT IDENTITY(1,1) PRIMARY KEY,
                    ВыдачаID INT NOT NULL,
                    ЧитательID INT NOT NULL,
                    СуммаШтрафа DECIMAL(10,2) NOT NULL,
                    ДнейПросрочки INT NOT NULL,
                    ДатаНачисления DATE DEFAULT CAST(GETDATE() AS DATE),
                    ДатаОплаты DATE NULL,
                    Статус NVARCHAR(20) DEFAULT 'Не оплачен',
                    FOREIGN KEY (ВыдачаID) REFERENCES Выдачи(ВыдачаID),
                    FOREIGN KEY (ЧитательID) REFERENCES Читатели(ЧитательID)
                );
            END;
            
            INSERT INTO dbo.Штрафы (ВыдачаID, ЧитательID, СуммаШтрафа, ДнейПросрочки)
            VALUES (@ВыдачаID, @ЧитательID, @Штраф, @ДнейПросрочки);
        END
        
        -- 6. Логирование успешного возврата
        DECLARE @СообщениеУспеха NVARCHAR(500);
        DECLARE @НазваниеКниги NVARCHAR(200);
        DECLARE @ИмяЧитателя NVARCHAR(200);
        
        SELECT @НазваниеКниги = Название FROM Книги WHERE КнигаID = @КнигаID;
        SELECT @ИмяЧитателя = Фамилия + ' ' + Имя FROM Читатели WHERE ЧитательID = @ЧитательID;
        
        SET @СообщениеУспеха = 'Книга "' + @НазваниеКниги + '" возвращена читателем ' + @ИмяЧитателя;
        
        IF @ДнейПросрочки > 0
            SET @СообщениеУспеха = @СообщениеУспеха + '. Просрочка: ' + CAST(@ДнейПросрочки AS NVARCHAR(10)) + 
                                   ' дн., штраф: ' + CAST(@Штраф AS NVARCHAR(20)) + ' руб.';
        ELSE
            SET @СообщениеУспеха = @СообщениеУспеха + '. Возврат в срок.';
        
        PRINT @СообщениеУспеха;
        
        COMMIT TRANSACTION SmartBookReturn;
        PRINT 'Транзакция возврата книги успешно завершена.';
        
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION SmartBookReturn;
        SET @ErrorMessage = ERROR_MESSAGE();
        PRINT 'Ошибка в транзакции возврата: ' + @ErrorMessage;
        THROW;
    END CATCH
END;

-- ===============================================
-- ТРАНЗАКЦИЯ 2: Массовое продление сроков возврата
-- ===============================================

-- Пример использования транзакции 2
BEGIN
    DECLARE @ДнейПродления INT = 7; -- На сколько дней продляем
    DECLARE @МаксимумПросрочек INT = 1; -- Не продлеваем, если больше 1 просрочки
    DECLARE @ПродленоВыдач INT = 0;
    DECLARE @ОтказановВыдач INT = 0;
    DECLARE @ErrorMessage NVARCHAR(4000);

    BEGIN TRY
        BEGIN TRANSACTION MassExtension;
        
        -- Таблица для хранения результатов
        DECLARE @РезультатыПродления TABLE (
            ВыдачаID INT,
            ЧитательID INT,
            ИмяЧитателя NVARCHAR(200),
            НазваниеКниги NVARCHAR(200),
            СтараяДатаВозврата DATE,
            НоваяДатаВозврата DATE,
            Статус NVARCHAR(50),
            Причина NVARCHAR(200)
        );
        
        -- 1. Анализ кандидатов на продление (активные выдачи, истекающие в ближайшие 3 дня)
        INSERT INTO @РезультатыПродления (
            ВыдачаID, ЧитательID, ИмяЧитателя, НазваниеКниги, 
            СтараяДатаВозврата, Статус, Причина
        )
        SELECT 
            l.ВыдачаID,
            l.ЧитательID,
            r.Фамилия + ' ' + r.Имя AS ИмяЧитателя,
            b.Название AS НазваниеКниги,
            l.ДатаВозврата,
            'В обработке',
            'Истекает в течение 3 дней'
        FROM Выдачи l
        INNER JOIN Читатели r ON l.ЧитательID = r.ЧитательID
        INNER JOIN Книги b ON l.КнигаID = b.КнигаID
        WHERE l.ДатаФактическогоВозврата IS NULL
            AND l.ДатаВозврата BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(DAY, 3, CAST(GETDATE() AS DATE));
        
        -- 2. Проверка каждого кандидата и продление
        DECLARE @ТекущаяВыдачаID INT, @ТекущийЧитательID INT, @ТекущаяДатаВозврата DATE;
        DECLARE @КоличествоПросрочек INT;
        DECLARE @ПроверкаЧитателя NVARCHAR(200);
        
        DECLARE extension_cursor CURSOR FOR
        SELECT ВыдачаID, ЧитательID, СтараяДатаВозврата
        FROM @РезультатыПродления
        WHERE Статус = 'В обработке';
        
        OPEN extension_cursor;
        FETCH NEXT FROM extension_cursor INTO @ТекущаяВыдачаID, @ТекущийЧитательID, @ТекущаяДатаВозврата;
        
        WHILE @@FETCH_STATUS = 0
        BEGIN
            -- Проверка количества текущих просрочек у читателя
            SELECT @КоличествоПросрочек = COUNT(*)
            FROM Выдачи 
            WHERE ЧитательID = @ТекущийЧитательID 
                AND ДатаФактическогоВозврата IS NULL 
                AND ДатаВозврата < CAST(GETDATE() AS DATE);
            
            -- Проверка общего состояния читателя
            SET @ПроверкаЧитателя = dbo.FN_CheckReaderEligibility(@ТекущийЧитательID, 10, @МаксимумПросрочек);
            
            IF @КоличествоПросрочек <= @МаксимумПросрочек AND LEFT(@ПроверкаЧитателя, 5) != 'ОТКАЗ'
            BEGIN
                -- Продляем срок
                DECLARE @НоваяДатаВозврата DATE = DATEADD(DAY, @ДнейПродления, @ТекущаяДатаВозврата);
                
                UPDATE Выдачи 
                SET ДатаВозврата = @НоваяДатаВозврата
                WHERE ВыдачаID = @ТекущаяВыдачаID;
                
                UPDATE @РезультатыПродления
                SET 
                    НоваяДатаВозврата = @НоваяДатаВозврата,
                    Статус = 'Продлено',
                    Причина = 'Срок продлён на ' + CAST(@ДнейПродления AS NVARCHAR(10)) + ' дней'
                WHERE ВыдачаID = @ТекущаяВыдачаID;
                
                SET @ПродленоВыдач = @ПродленоВыдач + 1;
            END
            ELSE
            BEGIN
                -- Отказ в продлении
                DECLARE @ПричинаОтказа NVARCHAR(200);
                
                IF @КоличествоПросрочек > @МаксимумПросрочек
                    SET @ПричинаОтказа = 'Слишком много просрочек (' + CAST(@КоличествоПросрочек AS NVARCHAR(10)) + ')';
                ELSE
                    SET @ПричинаОтказа = 'Нарушены условия: ' + @ПроверкаЧитателя;
                
                UPDATE @РезультатыПродления
                SET 
                    Статус = 'Отказано',
                    Причина = @ПричинаОтказа
                WHERE ВыдачаID = @ТекущаяВыдачаID;
                
                SET @ОтказановВыдач = @ОтказановВыдач + 1;
            END
            
            FETCH NEXT FROM extension_cursor INTO @ТекущаяВыдачаID, @ТекущийЧитательID, @ТекущаяДатаВозврата;
        END
        
        CLOSE extension_cursor;
        DEALLOCATE extension_cursor;
        
        -- 3. Создание отчёта о продлении
        PRINT '=== ОТЧЁТ О МАССОВОМ ПРОДЛЕНИИ СРОКОВ ===';
        PRINT 'Дата операции: ' + CONVERT(NVARCHAR(19), GETDATE(), 120);
        PRINT 'Продлено выдач: ' + CAST(@ПродленоВыдач AS NVARCHAR(10));
        PRINT 'Отказано в продлении: ' + CAST(@ОтказановВыдач AS NVARCHAR(10));
        PRINT '';
        
        -- Детальный отчёт
        DECLARE @ВыдачаОтчёт NVARCHAR(500);
        DECLARE report_cursor CURSOR FOR
        SELECT 
            ИмяЧитателя + ': "' + НазваниеКниги + '" - ' + Статус + ' (' + Причина + ')'
        FROM @РезультатыПродления
        ORDER BY Статус DESC, ИмяЧитателя;
        
        OPEN report_cursor;
        FETCH NEXT FROM report_cursor INTO @ВыдачаОтчёт;
        
        WHILE @@FETCH_STATUS = 0
        BEGIN
            PRINT @ВыдачаОтчёт;
            FETCH NEXT FROM report_cursor INTO @ВыдачаОтчёт;
        END
        
        CLOSE report_cursor;
        DEALLOCATE report_cursor;
        
        COMMIT TRANSACTION MassExtension;
        PRINT '';
        PRINT 'Транзакция массового продления успешно завершена.';
        
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION MassExtension;
        SET @ErrorMessage = ERROR_MESSAGE();
        PRINT 'Ошибка в транзакции массового продления: ' + @ErrorMessage;
        THROW;
    END CATCH
END;

-- ===============================================
-- ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ СОЗДАННЫХ ОБЪЕКТОВ
-- ===============================================

PRINT '';
PRINT '=== ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ СОЗДАННЫХ ОБЪЕКТОВ ===';

-- Пример 1: Использование представления для анализа популярности
PRINT '1. Топ-5 самых популярных книг:';
SELECT TOP 5
    Название,
    АвторИмя,
    КоличествоВыдач,
    КатегорияПопулярности
FROM dbo.VW_PopularBooksDetailed
ORDER BY КоличествоВыдач DESC;

-- Пример 2: Использование функции расчёта штрафа
PRINT '';
PRINT '2. Расчёт штрафа за просрочку на 10 дней:';
SELECT dbo.FN_CalculateFine('2024-01-01', '2024-01-11') AS Штраф;

-- Пример 3: Проверка права читателя на выдачу
PRINT '';
PRINT '3. Проверка права читателя с ID=1 на выдачу:';
SELECT dbo.FN_CheckReaderEligibility(1) AS ПроверкаПрава;

-- Пример 4: Использование хранимой процедуры умной выдачи
PRINT '';
PRINT '4. Пример умной выдачи книги:';
DECLARE @Результат NVARCHAR(500), @НоваяВыдачаID INT;
EXEC dbo.SP_SmartBookIssue 
    @КнигаID = 1,
    @ЧитательID = 1,
    @СрокВозврата = 14,
    @Результат = @Результат OUTPUT,
    @НоваяВыдачаID = @НоваяВыдачаID OUTPUT;
    
PRINT 'Результат: ' + @Результат;
IF @НоваяВыдачаID IS NOT NULL
    PRINT 'ID новой выдачи: ' + CAST(@НоваяВыдачаID AS NVARCHAR(10));

-- Пример 5: Анализ просроченных выдач
PRINT '';
PRINT '5. Обработка просроченных выдач:';
DECLARE @ОбработаноВыдач INT, @ОбщаяСуммаШтрафов DECIMAL(15,2), @СписокПросрочекXML XML;
EXEC dbo.SP_ProcessOverdueLoans 
    @ДнейПросрочки = 1,
    @ОбработаноВыдач = @ОбработаноВыдач OUTPUT,
    @ОбщаяСуммаШтрафов = @ОбщаяСуммаШтрафов OUTPUT,
    @СписокПросрочекXML = @СписокПросрочекXML OUTPUT;

PRINT 'Обработано просроченных выдач: ' + CAST(@ОбработаноВыдач AS NVARCHAR(10));
PRINT 'Общая сумма штрафов: ' + CAST(@ОбщаяСуммаШтрафов AS NVARCHAR(20)) + ' руб.';

PRINT '';
PRINT '=== ВСЕ ОБЪЕКТЫ ГОТОВЫ К ИСПОЛЬЗОВАНИЮ! ==='; 