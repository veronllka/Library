
-- 1. ORDER BY - Сортировка книг по году издания (по убыванию)
SELECT Название, АвторИмя, ГодИздания
FROM Книги 
ORDER BY ГодИздания DESC;

-- 2. IN - Поиск книг определенных жанров
SELECT Название, АвторИмя, ЖанрНазвание
FROM Книги
WHERE ЖанрНазвание IN ('Фантастика', 'Роман', 'Детектив');

-- 3. BETWEEN - Книги, изданные в определенный период
SELECT Название, АвторИмя, ГодИздания
FROM Книги
WHERE ГодИздания BETWEEN 2000 AND 2022;

-- 4. LIKE - Поиск книг по части названия
SELECT Название, АвторИмя
FROM Книги
WHERE Название LIKE '%война%';

-- 5. COUNT - Количество книг каждого автора
SELECT АвторИмя, COUNT(*) AS КоличествоКниг
FROM Книги
GROUP BY АвторИмя;

-- 6. GROUP BY с HAVING - Авторы с более чем 3 книгами
SELECT АвторИмя, COUNT(*) AS КоличествоКниг
FROM Книги
GROUP BY АвторИмя
HAVING COUNT(*) > 3;

-- 7. Подзапрос - Книги авторов, у которых больше 3 книг
SELECT Название, АвторИмя
FROM Книги
WHERE АвторИмя IN (
    SELECT АвторИмя 
    FROM Книги 
    GROUP BY АвторИмя 
    HAVING COUNT(*) > 3
);

-- 8. EXISTS - Пользователи, которые брали книги
SELECT ПользовательID, ФИО, Логин
FROM Пользователи p
WHERE EXISTS (
    SELECT 1 
    FROM Выдачи v 
    WHERE v.ЧитательID = p.ПользовательID
);

-- 9. INNER JOIN - Информация о выданных книгах
SELECT p.ФИО, k.Название, v.ДатаВыдачи, v.ДатаВозврата
FROM Выдачи v
INNER JOIN Пользователи p ON v.ЧитательID = p.ПользовательID
INNER JOIN Книги k ON v.КнигаID = k.КнигаID;

-- 10. OUTER JOIN - Все книги и информация о их выдаче
SELECT k.Название, k.АвторИмя, v.ДатаВыдачи, v.ДатаВозврата, p.ФИО
FROM Книги k
LEFT OUTER JOIN Выдачи v ON k.КнигаID = v.КнигаID
LEFT OUTER JOIN Пользователи p ON v.ЧитательID = p.ПользовательID;

-- 11. EXCEPT - Книги, которые никогда не выдавались
SELECT КнигаID, Название
FROM Книги
EXCEPT
SELECT k.КнигаID, k.Название
FROM Книги k
JOIN Выдачи v ON k.КнигаID = v.КнигаID;

-- 12. INTERSECT - Книги, которые были выданы и возвращены
SELECT k.КнигаID, k.Название
FROM Книги k
JOIN Выдачи v ON k.КнигаID = v.КнигаID
WHERE v.ДатаФактическогоВозврата IS NOT NULL
INTERSECT
SELECT k.КнигаID, k.Название
FROM Книги k
JOIN Выдачи v ON k.КнигаID = v.КнигаID;

-- 13. CASE - Классификация книг по году издания
SELECT Название, ГодИздания,
CASE
    WHEN ГодИздания < 1900 THEN 'Классика'
    WHEN ГодИздания BETWEEN 1900 AND 2000 THEN 'Современная'
    ELSE 'Новинка'
END AS Категория
FROM Книги;

-- 14. IF (IIF) - Доступность книги
SELECT Название, 
    IIF(EXISTS(SELECT 1 FROM Выдачи WHERE КнигаID = Книги.КнигаID AND ДатаФактическогоВозврата IS NULL),
        'Выдана', 'Доступна') AS Статус
FROM Книги;

-- 15. Комбинированный запрос (LIKE, ORDER BY, GROUP BY)
SELECT ЖанрНазвание, COUNT(*) AS КоличествоКниг
FROM Книги
WHERE Название LIKE '%история%'
GROUP BY ЖанрНазвание
ORDER BY КоличествоКниг DESC; 