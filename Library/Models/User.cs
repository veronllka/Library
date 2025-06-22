using System;

namespace Library.Models
{
    public class User
    {
        public int ПользовательID { get; set; }
        public string ФИО { get; set; } = string.Empty;
        public string Логин { get; set; } = string.Empty;
        public int РольID { get; set; }
        public string РольНазвание { get; set; } = string.Empty;
    }

    public class Author
    {
        public int АвторID { get; set; }
        public string ФИО { get; set; } = string.Empty;
        public short? ГодРождения { get; set; }
    }

    public class Publisher
    {
        public int ИздательствоID { get; set; }
        public string Название { get; set; } = string.Empty;
        public string Город { get; set; } = string.Empty;
    }

    public class Genre
    {
        public int ЖанрID { get; set; }
        public string Название { get; set; } = string.Empty;
    }

    public class Book
    {
        public int КнигаID { get; set; }
        public string Название { get; set; }
        public short? ГодИздания { get; set; }
        public string АвторИмя { get; set; }
        public string ИздательствоНазвание { get; set; }
        public string ЖанрНазвание { get; set; }
    }

    public class Reader
    {
        public int ЧитательID { get; set; }
        public string Фамилия { get; set; } = string.Empty;
        public string Имя { get; set; } = string.Empty;
        public string Отчество { get; set; } = string.Empty;
        public DateOnly? ДатаРождения { get; set; }
        public string Телефон { get; set; } = string.Empty;
        public string Адрес { get; set; } = string.Empty;
        public string ФИО { get; set; } = string.Empty;
    }

    public class Loan
    {
        public int ВыдачаID { get; set; }
        public int ЧитательID { get; set; }
        public int КнигаID { get; set; }
        public DateOnly ДатаВыдачи { get; set; }
        public DateOnly ДатаВозврата { get; set; }
        public DateOnly? ДатаФактическогоВозврата { get; set; }
        public string ЧитательИмя { get; set; } = string.Empty;
        public string КнигаНазвание { get; set; } = string.Empty;
        public string Статус { get; set; } = string.Empty;

        // Вычисляемые свойства
        public bool Возвращена => ДатаФактическогоВозврата.HasValue;
        public bool Просрочена => !Возвращена && ДатаВозврата < DateOnly.FromDateTime(DateTime.Today);
    }
} 