using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Library.Models;
using Microsoft.Data.SqlClient;

namespace Library.Services
{
    public class ReportService
    {
        private readonly DatabaseService _databaseService;

        public ReportService()
        {
            _databaseService = new DatabaseService();
        }

        public async Task<BaseReport> GenerateReportAsync(ReportParameters parameters)
        {
            return parameters.ReportType switch
            {
                ReportType.BookPopularity => await GenerateBookPopularityReportAsync(parameters),
                ReportType.ReaderActivity => await GenerateReaderActivityReportAsync(parameters),
                ReportType.GenreStatistics => await GenerateGenreStatisticsReportAsync(parameters),
                ReportType.OverdueLoans => await GenerateOverdueLoansReportAsync(parameters),
                _ => throw new ArgumentException("Неизвестный тип отчета")
            };
        }

        private async Task<BookPopularityReport> GenerateBookPopularityReportAsync(ReportParameters parameters)
        {
            var report = new BookPopularityReport
            {
                Title = "Отчет по популярности книг",
                Description = "Анализ популярности книг на основе количества выдач",
                Period = FormatPeriod(parameters.StartDate, parameters.EndDate),
                GeneratedAt = DateTime.Now
            };

            try
            {
                using var connection = new SqlConnection(_databaseService.GetConnectionString());
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        k.КнигаID,
                        k.Название,
                        ISNULL(a.ФИО, 'Неизвестен') as АвторИмя,
                        ISNULL(g.Название, 'Неопределен') as НазваниеЖанра,
                        COUNT(v.ВыдачаID) as КоличествоВыдач,
                        CASE 
                            WHEN COUNT(v.ВыдачаID) >= 10 THEN 'Очень популярная'
                            WHEN COUNT(v.ВыдачаID) >= 5 THEN 'Популярная'
                            WHEN COUNT(v.ВыдачаID) >= 2 THEN 'Умеренно популярная'
                            ELSE 'Малопопулярная'
                        END as Статус
                    FROM Книги k
                    LEFT JOIN Книги_Авторы ka ON k.КнигаID = ka.КнигаID
                    LEFT JOIN Авторы a ON ka.АвторID = a.АвторID
                    LEFT JOIN Жанры g ON k.ЖанрID = g.ЖанрID
                    LEFT JOIN Выдачи v ON k.КнигаID = v.КнигаID 
                        AND v.ДатаВыдачи BETWEEN @StartDate AND @EndDate
                    GROUP BY k.КнигаID, k.Название, a.ФИО, g.Название
                    ORDER BY COUNT(v.ВыдачаID) DESC";

                if (parameters.TopCount > 0)
                {
                    query = query.Replace("ORDER BY COUNT(v.ВыдачаID) DESC", 
                                        $"ORDER BY COUNT(v.ВыдачаID) DESC OFFSET 0 ROWS FETCH NEXT {parameters.TopCount} ROWS ONLY");
                }

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@StartDate", parameters.StartDate);
                command.Parameters.AddWithValue("@EndDate", parameters.EndDate);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    report.Books.Add(new BookPopularityItem
                    {
                        КнигаID = reader.GetInt32("КнигаID"),
                        Название = reader.GetString("Название"),
                        АвторИмя = reader.GetString("АвторИмя"),
                        ЖанрНазвание = reader.GetString("НазваниеЖанра"),
                        КоличествоВыдач = reader.GetInt32("КоличествоВыдач"),
                        Статус = reader.GetString("Статус")
                    });
                }

                report.TotalLoans = report.Books.Sum(b => b.КоличествоВыдач);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при генерации отчета по популярности книг: {ex.Message}");
            }

            return report;
        }

        private async Task<ReaderActivityReport> GenerateReaderActivityReportAsync(ReportParameters parameters)
        {
            var report = new ReaderActivityReport
            {
                Title = "Отчет по активности читателей",
                Description = "Анализ активности читателей библиотеки",
                Period = FormatPeriod(parameters.StartDate, parameters.EndDate),
                GeneratedAt = DateTime.Now
            };

            try
            {
                using var connection = new SqlConnection(_databaseService.GetConnectionString());
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        ch.ЧитательID,
                        ch.Фамилия + ' ' + ch.Имя + ISNULL(' ' + ch.Отчество, '') as ИмяЧитателя,
                        ch.Телефон,
                        COUNT(v.ВыдачаID) as КоличествоВыдач,
                        COUNT(CASE WHEN v.ДатаФактическогоВозврата IS NULL THEN 1 END) as АктивныхВыдач,
                        MAX(v.ДатаВыдачи) as ПоследняяВыдача,
                        CASE 
                            WHEN COUNT(v.ВыдачаID) >= 10 THEN 'Очень активный'
                            WHEN COUNT(v.ВыдачаID) >= 5 THEN 'Активный'
                            WHEN COUNT(v.ВыдачаID) >= 1 THEN 'Умеренно активный'
                            ELSE 'Неактивный'
                        END as СтатусАктивности
                    FROM Читатели ch
                    LEFT JOIN Выдачи v ON ch.ЧитательID = v.ЧитательID 
                        AND v.ДатаВыдачи BETWEEN @StartDate AND @EndDate
                    GROUP BY ch.ЧитательID, ch.Фамилия, ch.Имя, ch.Отчество, ch.Телефон
                    HAVING COUNT(v.ВыдачаID) > 0
                    ORDER BY COUNT(v.ВыдачаID) DESC";

                if (parameters.TopCount > 0)
                {
                    query = query.Replace("ORDER BY COUNT(v.ВыдачаID) DESC", 
                                        $"ORDER BY COUNT(v.ВыдачаID) DESC OFFSET 0 ROWS FETCH NEXT {parameters.TopCount} ROWS ONLY");
                }

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@StartDate", parameters.StartDate);
                command.Parameters.AddWithValue("@EndDate", parameters.EndDate);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    report.Readers.Add(new ReaderActivityItem
                    {
                        ЧитательID = reader.GetInt32("ЧитательID"),
                        ИмяЧитателя = reader.GetString("ИмяЧитателя"),
                        Телефон = reader.IsDBNull("Телефон") ? "Не указан" : reader.GetString("Телефон"),
                        КоличествоВыдач = reader.GetInt32("КоличествоВыдач"),
                        АктивныхВыдач = reader.GetInt32("АктивныхВыдач"),
                        ПоследняяВыдача = reader.IsDBNull("ПоследняяВыдача") ? DateTime.MinValue : reader.GetDateTime("ПоследняяВыдача"),
                        СтатусАктивности = reader.GetString("СтатусАктивности")
                    });
                }

                report.TotalActiveReaders = report.Readers.Count;
                report.AverageLoansPerReader = report.Readers.Count > 0 ? 
                    report.Readers.Average(r => r.КоличествоВыдач) : 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при генерации отчета по активности читателей: {ex.Message}");
            }

            return report;
        }

        private async Task<GenreStatisticsReport> GenerateGenreStatisticsReportAsync(ReportParameters parameters)
        {
            var report = new GenreStatisticsReport
            {
                Title = "Статистика по жанрам",
                Description = "Анализ популярности жанров книг",
                Period = FormatPeriod(parameters.StartDate, parameters.EndDate),
                GeneratedAt = DateTime.Now
            };

            try
            {
                using var connection = new SqlConnection(_databaseService.GetConnectionString());
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        g.ЖанрID,
                        g.Название as НазваниеЖанра,
                        COUNT(v.ВыдачаID) as КоличествоВыдач,
                        COUNT(DISTINCT k.КнигаID) as КоличествоКниг
                    FROM Жанры g
                    LEFT JOIN Книги k ON g.ЖанрID = k.ЖанрID
                    LEFT JOIN Выдачи v ON k.КнигаID = v.КнигаID 
                        AND v.ДатаВыдачи BETWEEN @StartDate AND @EndDate
                    GROUP BY g.ЖанрID, g.Название
                    ORDER BY COUNT(v.ВыдачаID) DESC";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@StartDate", parameters.StartDate);
                command.Parameters.AddWithValue("@EndDate", parameters.EndDate);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    report.Genres.Add(new GenreStatisticsItem
                    {
                        ЖанрID = reader.GetInt32("ЖанрID"),
                        НазваниеЖанра = reader.GetString("НазваниеЖанра"),
                        КоличествоВыдач = reader.GetInt32("КоличествоВыдач"),
                        КоличествоКниг = reader.GetInt32("КоличествоКниг")
                    });
                }

                report.TotalGenres = report.Genres.Count;
                report.TotalLoans = report.Genres.Sum(g => g.КоличествоВыдач);

                // Вычисляем проценты и уровни популярности
                foreach (var genre in report.Genres)
                {
                    genre.Процент = report.TotalLoans > 0 ? 
                        (double)genre.КоличествоВыдач / report.TotalLoans * 100 : 0;
                    
                    genre.ПопулярностьУровень = genre.Процент switch
                    {
                        >= 20 => "Очень популярный",
                        >= 10 => "Популярный",
                        >= 5 => "Умеренно популярный",
                        _ => "Малопопулярный"
                    };
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при генерации статистики по жанрам: {ex.Message}");
            }

            return report;
        }

        private async Task<OverdueLoansReport> GenerateOverdueLoansReportAsync(ReportParameters parameters)
        {
            var report = new OverdueLoansReport
            {
                Title = "Отчет по просроченным выдачам",
                Description = "Анализ просроченных выдач и штрафов",
                Period = FormatPeriod(parameters.StartDate, parameters.EndDate),
                GeneratedAt = DateTime.Now
            };

            try
            {
                using var connection = new SqlConnection(_databaseService.GetConnectionString());
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        v.ВыдачаID,
                        k.Название as НазваниеКниги,
                        ch.Фамилия + ' ' + ch.Имя + ISNULL(' ' + ch.Отчество, '') as ИмяЧитателя,
                        ch.Телефон,
                        v.ДатаВыдачи,
                        v.ДатаВозврата,
                        DATEDIFF(day, v.ДатаВозврата, GETDATE()) as ДнейПросрочки,
                        CASE 
                            WHEN v.ДатаВозврата IS NULL THEN 0
                            ELSE DATEDIFF(day, v.ДатаВозврата, GETDATE()) * 10.0
                        END as РазмерШтрафа
                    FROM Выдачи v
                    INNER JOIN Книги k ON v.КнигаID = k.КнигаID
                    INNER JOIN Читатели ch ON v.ЧитательID = ch.ЧитательID
                    WHERE v.ДатаВозврата < GETDATE() 
                        AND v.ДатаФактическогоВозврата IS NULL
                        AND v.ДатаВыдачи BETWEEN @StartDate AND @EndDate
                    ORDER BY DATEDIFF(day, v.ДатаВозврата, GETDATE()) DESC";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@StartDate", parameters.StartDate);
                command.Parameters.AddWithValue("@EndDate", parameters.EndDate);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var daysOverdue = reader.GetInt32("ДнейПросрочки");
                    var fineAmount = reader.GetDouble("РазмерШтрафа");
                    
                    report.OverdueLoans.Add(new OverdueLoanItem
                    {
                        ВыдачаID = reader.GetInt32("ВыдачаID"),
                        НазваниеКниги = reader.GetString("НазваниеКниги"),
                        ИмяЧитателя = reader.GetString("ИмяЧитателя"),
                        Телефон = reader.IsDBNull("Телефон") ? "Не указан" : reader.GetString("Телефон"),
                        ДатаВыдачи = reader.GetDateTime("ДатаВыдачи"),
                        ДатаВозврата = reader.GetDateTime("ДатаВозврата"),
                        ДнейПросрочки = daysOverdue,
                        РазмерШтрафа = fineAmount,
                        СтатусШтрафа = daysOverdue switch
                        {
                            <= 7 => "Небольшая просрочка",
                            <= 30 => "Средняя просрочка",
                            _ => "Серьезная просрочка"
                        }
                    });
                }

                report.TotalOverdueLoans = report.OverdueLoans.Count;
                report.TotalOverdueFine = report.OverdueLoans.Sum(o => o.РазмерШтрафа);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при генерации отчета по просроченным выдачам: {ex.Message}");
            }

            return report;
        }

        private string FormatPeriod(DateTime startDate, DateTime endDate)
        {
            var culture = new CultureInfo("ru-RU");
            return $"{startDate.ToString("dd MMMM yyyy", culture)} - {endDate.ToString("dd MMMM yyyy", culture)}";
        }
    }
} 