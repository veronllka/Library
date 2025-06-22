using System;
using System.Collections.Generic;

namespace Library.Models
{
    public enum ReportType
    {
        BookPopularity,
        ReaderActivity,
        GenreStatistics,
        OverdueLoans
    }

    public class ReportParameters
    {
        public ReportType ReportType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TopCount { get; set; }
    }

    public abstract class BaseReport
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
    }

    public class BookPopularityReport : BaseReport
    {
        public int TotalLoans { get; set; }
        public List<BookPopularityItem> Books { get; set; } = new List<BookPopularityItem>();
    }

    public class BookPopularityItem
    {
        public int КнигаID { get; set; }
        public string Название { get; set; } = string.Empty;
        public string АвторИмя { get; set; } = string.Empty;
        public string ЖанрНазвание { get; set; } = string.Empty;
        public int КоличествоВыдач { get; set; }
        public string Статус { get; set; } = string.Empty;
    }

    public class ReaderActivityReport : BaseReport
    {
        public int TotalActiveReaders { get; set; }
        public double AverageLoansPerReader { get; set; }
        public List<ReaderActivityItem> Readers { get; set; } = new List<ReaderActivityItem>();
    }

    public class ReaderActivityItem
    {
        public int ЧитательID { get; set; }
        public string ИмяЧитателя { get; set; } = string.Empty;
        public string Телефон { get; set; } = string.Empty;
        public int КоличествоВыдач { get; set; }
        public int АктивныхВыдач { get; set; }
        public DateTime ПоследняяВыдача { get; set; }
        public string СтатусАктивности { get; set; } = string.Empty;
    }

    public class GenreStatisticsReport : BaseReport
    {
        public int TotalGenres { get; set; }
        public int TotalLoans { get; set; }
        public List<GenreStatisticsItem> Genres { get; set; } = new List<GenreStatisticsItem>();
    }

    public class GenreStatisticsItem
    {
        public int ЖанрID { get; set; }
        public string НазваниеЖанра { get; set; } = string.Empty;
        public int КоличествоВыдач { get; set; }
        public int КоличествоКниг { get; set; }
        public double Процент { get; set; }
        public string ПопулярностьУровень { get; set; } = string.Empty;
    }

    public class OverdueLoansReport : BaseReport
    {
        public int TotalOverdueLoans { get; set; }
        public double TotalOverdueFine { get; set; }
        public List<OverdueLoanItem> OverdueLoans { get; set; } = new List<OverdueLoanItem>();
    }

    public class OverdueLoanItem
    {
        public int ВыдачаID { get; set; }
        public string НазваниеКниги { get; set; } = string.Empty;
        public string ИмяЧитателя { get; set; } = string.Empty;
        public string Телефон { get; set; } = string.Empty;
        public DateTime ДатаВыдачи { get; set; }
        public DateTime ДатаВозврата { get; set; }
        public int ДнейПросрочки { get; set; }
        public double РазмерШтрафа { get; set; }
        public string СтатусШтрафа { get; set; } = string.Empty;
    }
} 