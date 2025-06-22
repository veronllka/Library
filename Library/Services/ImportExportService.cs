using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using Library.Models;
using Microsoft.Data.SqlClient;

namespace Library.Services
{
    public class ImportExportService
    {
        private readonly DatabaseService _databaseService;
        private readonly string _connectionString;

        public ImportExportService()
        {
            _databaseService = new DatabaseService();
            _connectionString = @"Data Source=WIN-IT3KG728UQJ\SQLEXPRESS;Initial Catalog=Library;Integrated Security=True;Connect Timeout=30;TrustServerCertificate=True;";
        }

        #region Экспорт данных

        public async Task<(bool Success, string Message)> ExportBooksAsync(string filePath, ExportFormat format)
        {
            try
            {
                var books = await _databaseService.GetBooksAsync();
                return await ExportDataAsync(books, filePath, format, "Книги");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка экспорта книг: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> ExportReadersAsync(string filePath, ExportFormat format)
        {
            try
            {
                var readers = await _databaseService.GetReadersAsync();
                return await ExportDataAsync(readers, filePath, format, "Читатели");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка экспорта читателей: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> ExportLoansAsync(string filePath, ExportFormat format)
        {
            try
            {
                var loans = await _databaseService.GetLoansAsync();
                return await ExportDataAsync(loans, filePath, format, "Выдачи");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка экспорта выдач: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> ExportAuthorsAsync(string filePath, ExportFormat format)
        {
            try
            {
                var authors = await _databaseService.GetAuthorsAsync();
                return await ExportDataAsync(authors, filePath, format, "Авторы");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка экспорта авторов: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> ExportPublishersAsync(string filePath, ExportFormat format)
        {
            try
            {
                var publishers = await _databaseService.GetPublishersAsync();
                return await ExportDataAsync(publishers, filePath, format, "Издательства");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка экспорта издательств: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> ExportGenresAsync(string filePath, ExportFormat format)
        {
            try
            {
                var genres = await _databaseService.GetGenresAsync();
                return await ExportDataAsync(genres, filePath, format, "Жанры");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка экспорта жанров: {ex.Message}");
            }
        }

        private async Task<(bool Success, string Message)> ExportDataAsync<T>(IEnumerable<T> data, string filePath, ExportFormat format, string dataType)
        {
            try
            {
                switch (format)
                {
                    case ExportFormat.JSON:
                        await ExportToJsonAsync(data, filePath);
                        break;
                    case ExportFormat.XML:
                        await ExportToXmlAsync(data, filePath, dataType);
                        break;
                    case ExportFormat.CSV:
                        await ExportToCsvAsync(data, filePath);
                        break;
                    case ExportFormat.TXT:
                        await ExportToTxtAsync(data, filePath);
                        break;
                    default:
                        return (false, "Неподдерживаемый формат экспорта");
                }

                return (true, $"Данные успешно экспортированы в файл: {filePath}");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка экспорта: {ex.Message}");
            }
        }

        private async Task ExportToJsonAsync<T>(IEnumerable<T> data, string filePath)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var json = JsonSerializer.Serialize(data, options);
            await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
        }

        private async Task ExportToXmlAsync<T>(IEnumerable<T> data, string filePath, string rootElementName)
        {
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
            var root = new XElement(rootElementName);

            foreach (var item in data)
            {
                var element = new XElement(typeof(T).Name);
                var properties = typeof(T).GetProperties();

                foreach (var prop in properties)
                {
                    var value = prop.GetValue(item);
                    if (value != null)
                    {
                        element.Add(new XElement(prop.Name, value));
                    }
                }

                root.Add(element);
            }

            doc.Add(root);
            await using var stream = new FileStream(filePath, FileMode.Create);
            await doc.SaveAsync(stream, SaveOptions.None, default);
        }

        private async Task ExportToCsvAsync<T>(IEnumerable<T> data, string filePath)
        {
            var csv = new StringBuilder();
            var properties = typeof(T).GetProperties();

            // Заголовки
            csv.AppendLine(string.Join(";", properties.Select(p => p.Name)));

            // Данные
            foreach (var item in data)
            {
                var values = properties.Select(p => 
                {
                    var value = p.GetValue(item);
                    return value?.ToString()?.Replace(";", ",") ?? "";
                });
                csv.AppendLine(string.Join(";", values));
            }

            await File.WriteAllTextAsync(filePath, csv.ToString(), Encoding.UTF8);
        }

        private async Task ExportToTxtAsync<T>(IEnumerable<T> data, string filePath)
        {
            var txt = new StringBuilder();
            var properties = typeof(T).GetProperties();

            foreach (var item in data)
            {
                txt.AppendLine(new string('=', 50));
                foreach (var prop in properties)
                {
                    var value = prop.GetValue(item);
                    txt.AppendLine($"{prop.Name}: {value ?? ""}");
                }
                txt.AppendLine();
            }

            await File.WriteAllTextAsync(filePath, txt.ToString(), Encoding.UTF8);
        }

        #endregion

        #region Импорт данных

        public async Task<(bool Success, string Message, int ImportedCount)> ImportBooksAsync(string filePath, ImportFormat format)
        {
            try
            {
                List<Book> books;
                switch (format)
                {
                    case ImportFormat.JSON:
                        books = await ImportFromJsonAsync<Book>(filePath);
                        break;
                    case ImportFormat.XML:
                        books = await ImportFromXmlAsync<Book>(filePath);
                        break;
                    case ImportFormat.CSV:
                        books = await ImportBooksFromCsvAsync(filePath);
                        break;
                    default:
                        return (false, "Неподдерживаемый формат импорта", 0);
                }

                int importedCount = await ImportBooksToDatabase(books);
                return (true, $"Успешно импортировано {importedCount} книг", importedCount);
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка импорта книг: {ex.Message}", 0);
            }
        }

        private async Task<List<T>> ImportFromJsonAsync<T>(string filePath)
        {
            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
        }

        private async Task<List<T>> ImportFromXmlAsync<T>(string filePath)
        {
            var doc = await Task.Run(() => XDocument.Load(filePath));
            var items = new List<T>();
            var properties = typeof(T).GetProperties();

            foreach (var element in doc.Root?.Elements() ?? Enumerable.Empty<XElement>())
            {
                var item = Activator.CreateInstance<T>();
                
                foreach (var prop in properties)
                {
                    var xmlElement = element.Element(prop.Name);
                    if (xmlElement != null)
                    {
                        var value = Convert.ChangeType(xmlElement.Value, prop.PropertyType);
                        prop.SetValue(item, value);
                    }
                }
                
                items.Add(item);
            }

            return items;
        }

        private async Task<List<Book>> ImportBooksFromCsvAsync(string filePath)
        {
            var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);
            var books = new List<Book>();

            if (lines.Length <= 1) return books;

            var headers = lines[0].Split(';');
            
            for (int i = 1; i < lines.Length; i++)
            {
                var values = lines[i].Split(';');
                if (values.Length >= 3)
                {
                    var book = new Book
                    {
                        Название = values.Length > 0 ? values[0] : "",
                        АвторИмя = values.Length > 1 ? values[1] : "",
                        ИздательствоНазвание = values.Length > 2 ? values[2] : "",
                        ЖанрНазвание = values.Length > 3 ? values[3] : "",
                        ГодИздания = values.Length > 4 && short.TryParse(values[4], out short year) ? year : null
                    };
                    books.Add(book);
                }
            }

            return books;
        }

        private async Task<int> ImportBooksToDatabase(List<Book> books)
        {
            int importedCount = 0;

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var book in books)
            {
                try
                {
                    // Проверяем, существует ли книга
                    var checkQuery = "SELECT COUNT(*) FROM Книги WHERE Название = @Название";
                    using var checkCommand = new SqlCommand(checkQuery, connection);
                    checkCommand.Parameters.AddWithValue("@Название", book.Название);
                    
                    int existingCount = (int)await checkCommand.ExecuteScalarAsync();
                    if (existingCount > 0) continue;

                    // Получаем или создаем автора, издательство, жанр
                    int? authorId = await GetOrCreateAuthorAsync(connection, book.АвторИмя);
                    int? publisherId = await GetOrCreatePublisherAsync(connection, book.ИздательствоНазвание);
                    int? genreId = await GetOrCreateGenreAsync(connection, book.ЖанрНазвание);

                    // Добавляем книгу
                    var insertQuery = @"
                        INSERT INTO Книги (Название, ГодИздания, ИздательствоID, ЖанрID)
                        VALUES (@Название, @ГодИздания, @ИздательствоID, @ЖанрID);
                        SELECT SCOPE_IDENTITY();";

                    using var insertCommand = new SqlCommand(insertQuery, connection);
                    insertCommand.Parameters.AddWithValue("@Название", book.Название);
                    insertCommand.Parameters.AddWithValue("@ГодИздания", (object?)book.ГодИздания ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@ИздательствоID", (object?)publisherId ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@ЖанрID", (object?)genreId ?? DBNull.Value);

                    var bookId = Convert.ToInt32(await insertCommand.ExecuteScalarAsync());

                    // Связываем с автором
                    if (authorId.HasValue)
                    {
                        var linkQuery = "INSERT INTO Книги_Авторы (КнигаID, АвторID) VALUES (@КнигаID, @АвторID)";
                        using var linkCommand = new SqlCommand(linkQuery, connection);
                        linkCommand.Parameters.AddWithValue("@КнигаID", bookId);
                        linkCommand.Parameters.AddWithValue("@АвторID", authorId.Value);
                        await linkCommand.ExecuteNonQueryAsync();
                    }

                    importedCount++;
                }
                catch (Exception ex)
                {
                    // Логируем ошибку, но продолжаем импорт
                    System.Diagnostics.Debug.WriteLine($"Ошибка импорта книги '{book.Название}': {ex.Message}");
                }
            }

            return importedCount;
        }

        private async Task<int?> GetOrCreateAuthorAsync(SqlConnection connection, string authorName)
        {
            if (string.IsNullOrWhiteSpace(authorName)) return null;

            // Проверяем существование
            var checkQuery = "SELECT АвторID FROM Авторы WHERE ФИО = @ФИО";
            using var checkCommand = new SqlCommand(checkQuery, connection);
            checkCommand.Parameters.AddWithValue("@ФИО", authorName);
            
            var result = await checkCommand.ExecuteScalarAsync();
            if (result != null) return (int)result;

            // Создаем нового
            var insertQuery = "INSERT INTO Авторы (ФИО) VALUES (@ФИО); SELECT SCOPE_IDENTITY();";
            using var insertCommand = new SqlCommand(insertQuery, connection);
            insertCommand.Parameters.AddWithValue("@ФИО", authorName);
            
            return Convert.ToInt32(await insertCommand.ExecuteScalarAsync());
        }

        private async Task<int?> GetOrCreatePublisherAsync(SqlConnection connection, string publisherName)
        {
            if (string.IsNullOrWhiteSpace(publisherName)) return null;

            var checkQuery = "SELECT ИздательствоID FROM Издательства WHERE Название = @Название";
            using var checkCommand = new SqlCommand(checkQuery, connection);
            checkCommand.Parameters.AddWithValue("@Название", publisherName);
            
            var result = await checkCommand.ExecuteScalarAsync();
            if (result != null) return (int)result;

            var insertQuery = "INSERT INTO Издательства (Название, Город) VALUES (@Название, ''); SELECT SCOPE_IDENTITY();";
            using var insertCommand = new SqlCommand(insertQuery, connection);
            insertCommand.Parameters.AddWithValue("@Название", publisherName);
            
            return Convert.ToInt32(await insertCommand.ExecuteScalarAsync());
        }

        private async Task<int?> GetOrCreateGenreAsync(SqlConnection connection, string genreName)
        {
            if (string.IsNullOrWhiteSpace(genreName)) return null;

            var checkQuery = "SELECT ЖанрID FROM Жанры WHERE Название = @Название";
            using var checkCommand = new SqlCommand(checkQuery, connection);
            checkCommand.Parameters.AddWithValue("@Название", genreName);
            
            var result = await checkCommand.ExecuteScalarAsync();
            if (result != null) return (int)result;

            var insertQuery = "INSERT INTO Жанры (Название) VALUES (@Название); SELECT SCOPE_IDENTITY();";
            using var insertCommand = new SqlCommand(insertQuery, connection);
            insertCommand.Parameters.AddWithValue("@Название", genreName);
            
            return Convert.ToInt32(await insertCommand.ExecuteScalarAsync());
        }

        #endregion
    }

    public enum ExportFormat
    {
        JSON,
        XML,
        CSV,
        TXT
    }

    public enum ImportFormat
    {
        JSON,
        XML,
        CSV
    }
} 