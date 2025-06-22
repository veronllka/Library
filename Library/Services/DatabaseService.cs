using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Library.Models;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Windows;

namespace Library.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            try
            {
                // Сначала пробуем подключиться к основной базе данных Library
                _connectionString = @"Data Source=WIN-IT3KG728UQJ\SQLEXPRESS;Initial Catalog=Library;Integrated Security=True;Connect Timeout=30;TrustServerCertificate=True;";
                
                // Проверяем доступность подключения при создании сервиса
                using var testConnection = new SqlConnection(_connectionString);
                testConnection.Open();
                testConnection.Close();
            }
            catch
            {
                // Если основная строка не работает, пробуем альтернативные варианты
                var alternativeConnections = new[]
                {
                    // Сначала пробуем с базой Library
                    @"Data Source=.\SQLEXPRESS;Initial Catalog=Library;Integrated Security=True;Connect Timeout=30;TrustServerCertificate=True;",
                    @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=Library;Integrated Security=True;Connect Timeout=30;TrustServerCertificate=True;",
                    @"Data Source=localhost\SQLEXPRESS;Initial Catalog=Library;Integrated Security=True;Connect Timeout=30;TrustServerCertificate=True;",
                    @"Data Source=localhost;Initial Catalog=Library;Integrated Security=True;Connect Timeout=30;TrustServerCertificate=True;",
                    
                    // Если базы Library нет, подключаемся к master для администрирования
                    @"Data Source=WIN-IT3KG728UQJ\SQLEXPRESS;Initial Catalog=master;Integrated Security=True;Connect Timeout=30;TrustServerCertificate=True;",
                    @"Data Source=.\SQLEXPRESS;Initial Catalog=master;Integrated Security=True;Connect Timeout=30;TrustServerCertificate=True;",
                    @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True;Connect Timeout=30;TrustServerCertificate=True;",
                    @"Data Source=localhost\SQLEXPRESS;Initial Catalog=master;Integrated Security=True;Connect Timeout=30;TrustServerCertificate=True;",
                    @"Data Source=localhost;Initial Catalog=master;Integrated Security=True;Connect Timeout=30;TrustServerCertificate=True;"
                };

                bool connectionFound = false;
                Exception lastException = null;
                
                foreach (var connStr in alternativeConnections)
                {
                    try
                    {
                        using var testConnection = new SqlConnection(connStr);
                        testConnection.Open();
                        testConnection.Close();
                        _connectionString = connStr;
                        connectionFound = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        continue;
                    }
                }

                if (!connectionFound)
                {
                    throw new Exception($"Не удалось установить подключение к базе данных. Убедитесь, что SQL Server запущен. Последняя ошибка: {lastException?.Message}");
                }
            }
        }

        public string GetConnectionString()
        {
            return _connectionString;
        }

        public DataTable ExecuteQuery(string query)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(query, connection);
            using var adapter = new SqlDataAdapter(command);
            var dataTable = new DataTable();
            adapter.Fill(dataTable);
            return dataTable;
        }

        public void ExecuteNonQuery(string query)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(query, connection);
            command.ExecuteNonQuery();
        }

        public static async Task<(bool Success, string ErrorMessage)> TestDatabaseConnectionAsync()
        {
            try
            {
                var connectionString = @"Data Source=WIN-IT3KG728UQJ\SQLEXPRESS;Initial Catalog=Library;Integrated Security=True;Connect Timeout=30;TrustServerCertificate=True;";
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                return (true, "Подключение успешно");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static User? ValidateUser(string username, string password)
        {
            try
            {
                // Используем ту же строку подключения, что и для других операций
                var connectionString = @"Data Source=WIN-IT3KG728UQJ\SQLEXPRESS;Initial Catalog=Library;Integrated Security=True;Connect Timeout=30;TrustServerCertificate=True;";

				using var connection = new SqlConnection(connectionString);
                connection.Open();

                // Получаем хеш пароля из базы данных для указанного логина
                var query = @"
                    SELECT п.ПользовательID, п.ФИО, п.Логин, п.Пароль, п.РольID, р.Название as РольНазвание
                    FROM Пользователи п
                    INNER JOIN Роли р ON п.РольID = р.РольID
                    WHERE п.Логин = @Логин";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Логин", username);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    string storedHash = reader.GetString("Пароль");
                    
                    // Проверяем, является ли пароль хешированным (начинается с префикса хеша)
                    bool isHashed = storedHash.Length > 20; // Простая проверка для определения хешированного пароля
                    
                    bool isValid;
                    if (isHashed)
                    {
                        // Если пароль хеширован, проверяем с помощью PasswordHasher
                        isValid = PasswordHasher.VerifyPassword(password, storedHash);
                    }
                    else
                    {
                        // Для обратной совместимости: если пароль не хеширован, сравниваем напрямую
                        isValid = password == storedHash;
                        
                        // Если пароль верный, можно обновить его на хешированную версию
                        if (isValid)
                        {
                            UpdatePasswordHash(reader.GetInt32("ПользовательID"), password);
                        }
                    }
                    
                    if (isValid)
                    {
                        return new User
                        {
                            ПользовательID = reader.GetInt32("ПользовательID"),
                            ФИО = reader.GetString("ФИО"),
                            Логин = reader.GetString("Логин"),
                            РольID = reader.GetInt32("РольID"),
                            РольНазвание = reader.GetString("РольНазвание")
                        };
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                // Логируем ошибку для отладки
                System.Diagnostics.Debug.WriteLine($"Ошибка авторизации: {ex.Message}");
                return null;
            }
        }

        // Метод для обновления пароля пользователя на хешированную версию
        private static void UpdatePasswordHash(int userId, string password)
        {
            try
            {
                var connectionString = @"Data Source=WIN-IT3KG728UQJ\SQLEXPRESS;Initial Catalog=Library;Integrated Security=True;Connect Timeout=30;TrustServerCertificate=True;";
                
                using var connection = new SqlConnection(connectionString);
                connection.Open();
                
                // Хешируем пароль
                string hashedPassword = PasswordHasher.HashPassword(password);
                
                // Обновляем пароль в базе данных
                var query = @"
                    UPDATE Пользователи
                    SET Пароль = @Пароль
                    WHERE ПользовательID = @ПользовательID";
                
                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Пароль", hashedPassword);
                command.Parameters.AddWithValue("@ПользовательID", userId);
                
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем работу программы
                System.Diagnostics.Debug.WriteLine($"Ошибка при обновлении хеша пароля: {ex.Message}");
            }
        }

        // Асинхронные методы для получения данных
        public async Task<List<Book>> GetBooksAsync()
        {
            var books = new List<Book>();
            
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        к.КнигаID, к.Название, к.ГодИздания,
                        а.ФИО as АвторИмя,
                        и.Название as ИздательствоНазвание,
                        ж.Название as ЖанрНазвание
                    FROM Книги к
                    LEFT JOIN Книги_Авторы ка ON к.КнигаID = ка.КнигаID
                    LEFT JOIN Авторы а ON ка.АвторID = а.АвторID
                    LEFT JOIN Издательства и ON к.ИздательствоID = и.ИздательствоID
                    LEFT JOIN Жанры ж ON к.ЖанрID = ж.ЖанрID
                    ORDER BY к.Название";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    books.Add(new Book
                    {
                        КнигаID = reader.GetInt32("КнигаID"),
                        Название = reader.GetString("Название"),
                        ГодИздания = reader.IsDBNull("ГодИздания") ? null : reader.GetInt16("ГодИздания"),
                        АвторИмя = reader.IsDBNull("АвторИмя") ? "Неизвестен" : reader.GetString("АвторИмя"),
                        ИздательствоНазвание = reader.IsDBNull("ИздательствоНазвание") ? "Неизвестно" : reader.GetString("ИздательствоНазвание"),
                        ЖанрНазвание = reader.IsDBNull("ЖанрНазвание") ? "Неопределен" : reader.GetString("ЖанрНазвание")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при получении списка книг: {ex.Message}");
            }

            return books;
        }

        public async Task<List<Reader>> GetReadersAsync()
        {
            var readers = new List<Reader>();
            
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT ЧитательID, Фамилия, Имя, Отчество, ДатаРождения, Телефон, Адрес
                    FROM Читатели
                    ORDER BY Фамилия, Имя";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    readers.Add(new Reader
                    {
                        ЧитательID = reader.GetInt32("ЧитательID"),
                        Фамилия = reader.GetString("Фамилия"),
                        Имя = reader.GetString("Имя"),
                        Отчество = reader.IsDBNull("Отчество") ? "" : reader.GetString("Отчество"),
                        ДатаРождения = reader.IsDBNull("ДатаРождения") ? null : DateOnly.FromDateTime(reader.GetDateTime("ДатаРождения")),
                        Телефон = reader.IsDBNull("Телефон") ? "" : reader.GetString("Телефон"),
                        Адрес = reader.IsDBNull("Адрес") ? "" : reader.GetString("Адрес"),
                        ФИО = $"{reader.GetString("Фамилия")} {reader.GetString("Имя")} {(reader.IsDBNull("Отчество") ? "" : reader.GetString("Отчество"))}"
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при получении списка читателей: {ex.Message}");
            }

            return readers;
        }

        public async Task<List<Loan>> GetLoansAsync()
        {
            var loans = new List<Loan>();
            
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        в.ВыдачаID, в.ЧитательID, в.КнигаID, в.ДатаВыдачи, в.ДатаВозврата, в.ДатаФактическогоВозврата,
                        ч.Фамилия + ' ' + ч.Имя + ISNULL(' ' + ч.Отчество, '') as ЧитательИмя,
                        к.Название as КнигаНазвание
                    FROM Выдачи в
                    INNER JOIN Читатели ч ON в.ЧитательID = ч.ЧитательID
                    INNER JOIN Книги к ON в.КнигаID = к.КнигаID
                    ORDER BY в.ДатаВыдачи DESC";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var loan = new Loan
                    {
                        ВыдачаID = reader.GetInt32("ВыдачаID"),
                        ЧитательID = reader.GetInt32("ЧитательID"),
                        КнигаID = reader.GetInt32("КнигаID"),
                        ДатаВыдачи = DateOnly.FromDateTime(reader.GetDateTime("ДатаВыдачи")),
                        ДатаВозврата = DateOnly.FromDateTime(reader.GetDateTime("ДатаВозврата")),
                        ДатаФактическогоВозврата = reader.IsDBNull("ДатаФактическогоВозврата") ? 
                            null : DateOnly.FromDateTime(reader.GetDateTime("ДатаФактическогоВозврата")),
                        ЧитательИмя = reader.GetString("ЧитательИмя"),
                        КнигаНазвание = reader.GetString("КнигаНазвание")
                    };

                    // Вычисляем статус
                    if (loan.ДатаФактическогоВозврата.HasValue)
                    {
                        loan.Статус = "Возвращена";
                    }
                    else if (loan.ДатаВозврата < DateOnly.FromDateTime(DateTime.Today))
                    {
                        loan.Статус = "Просрочена";
                    }
                    else
                    {
                        loan.Статус = "Активна";
                    }

                    loans.Add(loan);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при получении списка выдач: {ex.Message}");
            }

            return loans;
        }

        public async Task<List<Loan>> GetActiveLoansAsync()
        {
            var loans = await GetLoansAsync();
            return loans.Where(l => !l.ДатаФактическогоВозврата.HasValue).ToList();
        }

        public async Task<List<Author>> GetAuthorsAsync()
        {
            var authors = new List<Author>();
            
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = "SELECT АвторID, ФИО, ГодРождения FROM Авторы ORDER BY ФИО";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    authors.Add(new Author
                    {
                        АвторID = reader.GetInt32("АвторID"),
                        ФИО = reader.GetString("ФИО"),
                        ГодРождения = reader.IsDBNull("ГодРождения") ? null : reader.GetInt16("ГодРождения")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при получении списка авторов: {ex.Message}");
            }

            return authors;
        }

        public async Task<List<Publisher>> GetPublishersAsync()
        {
            var publishers = new List<Publisher>();
            
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = "SELECT ИздательствоID, Название, Город FROM Издательства ORDER BY Название";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    publishers.Add(new Publisher
                    {
                        ИздательствоID = reader.GetInt32("ИздательствоID"),
                        Название = reader.GetString("Название"),
                        Город = reader.IsDBNull("Город") ? "" : reader.GetString("Город")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при получении списка издательств: {ex.Message}");
            }

            return publishers;
        }

        public async Task<List<Genre>> GetGenresAsync()
        {
            var genres = new List<Genre>();
            
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = "SELECT ЖанрID, Название FROM Жанры ORDER BY Название";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    genres.Add(new Genre
                    {
                        ЖанрID = reader.GetInt32("ЖанрID"),
                        Название = reader.GetString("Название")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при получении списка жанров: {ex.Message}");
            }

            return genres;
        }

        // Асинхронный метод для выдачи книги
        public async Task IssueBookAsync(int readerId, int bookId, DateTime issueDate, DateTime returnDate)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    INSERT INTO Выдачи (ЧитательID, КнигаID, ДатаВыдачи, ДатаВозврата)
                    VALUES (@ЧитательID, @КнигаID, @ДатаВыдачи, @ДатаВозврата)";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ЧитательID", readerId);
                command.Parameters.AddWithValue("@КнигаID", bookId);
                command.Parameters.AddWithValue("@ДатаВыдачи", issueDate.Date);
                command.Parameters.AddWithValue("@ДатаВозврата", returnDate.Date);

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при выдаче книги: {ex.Message}");
            }
        }

        // Метод для возврата книги
        public async Task ReturnBookAsync(int loanId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    UPDATE Выдачи 
                    SET ДатаФактическогоВозврата = @ДатаВозврата
                    WHERE ВыдачаID = @ВыдачаID AND ДатаФактическогоВозврата IS NULL";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ВыдачаID", loanId);
                command.Parameters.AddWithValue("@ДатаВозврата", DateTime.Today);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    throw new Exception("Книга уже возвращена или выдача не найдена.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при возврате книги: {ex.Message}");
            }
        }

        // Синхронные методы для обратной совместимости
        public List<Book> GetBooks()
        {
            return GetBooksAsync().Result;
        }

        public List<Reader> GetReaders()
        {
            return GetReadersAsync().Result;
        }

        public List<Loan> GetLoans()
        {
            return GetLoansAsync().Result;
        }

        public List<Author> GetAuthors()
        {
            return GetAuthorsAsync().Result;
        }

        public List<Publisher> GetPublishers()
        {
            return GetPublishersAsync().Result;
        }

        public List<Genre> GetGenres()
        {
            return GetGenresAsync().Result;
        }

        public void IssueBook(int readerId, int bookId, DateTime issueDate, DateTime returnDate)
        {
            IssueBookAsync(readerId, bookId, issueDate, returnDate).Wait();
        }

        public void ReturnBook(int loanId)
        {
            ReturnBookAsync(loanId).Wait();
        }

        public List<Book> GetBooksSortedByYear(bool descending = true)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand(@"
                    SELECT 
                        к.КнигаID, к.Название, к.ГодИздания,
                        а.ФИО as АвторИмя,
                        и.Название as ИздательствоНазвание,
                        ж.Название as ЖанрНазвание
                    FROM Книги к
                    LEFT JOIN Книги_Авторы ка ON к.КнигаID = ка.КнигаID
                    LEFT JOIN Авторы а ON ка.АвторID = а.АвторID
                    LEFT JOIN Издательства и ON к.ИздательствоID = и.ИздательствоID
                    LEFT JOIN Жанры ж ON к.ЖанрID = ж.ЖанрID", connection);
                SqlDataAdapter adapter = new SqlDataAdapter(command);
                DataTable booksTable = new DataTable();
                adapter.Fill(booksTable);

                // Конвертируем DataTable в список Book
                List<Book> books = (from DataRow row in booksTable.Rows
                                  select new Book
                                  {
                                      КнигаID = Convert.ToInt32(row["КнигаID"]),
                                      Название = row["Название"].ToString(),
                                      ГодИздания = row["ГодИздания"] == DBNull.Value ? null : Convert.ToInt16(row["ГодИздания"]),
                                      АвторИмя = row["АвторИмя"] == DBNull.Value ? "Неизвестен" : row["АвторИмя"].ToString(),
                                      ИздательствоНазвание = row["ИздательствоНазвание"] == DBNull.Value ? "Неизвестно" : row["ИздательствоНазвание"].ToString(),
                                      ЖанрНазвание = row["ЖанрНазвание"] == DBNull.Value ? "Неопределен" : row["ЖанрНазвание"].ToString()
                                  }).ToList();
                
                // ORDER BY с LINQ
                return descending 
                    ? books.OrderByDescending(b => b.ГодИздания).ToList()
                    : books.OrderBy(b => b.ГодИздания).ToList();
            }
        }

        public List<Book> GetBooksByGenres(List<string> genres)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand(@"
                    SELECT 
                        к.КнигаID, к.Название, к.ГодИздания,
                        а.ФИО as АвторИмя,
                        и.Название as ИздательствоНазвание,
                        ж.Название as ЖанрНазвание
                    FROM Книги к
                    LEFT JOIN Книги_Авторы ка ON к.КнигаID = ка.КнигаID
                    LEFT JOIN Авторы а ON ка.АвторID = а.АвторID
                    LEFT JOIN Издательства и ON к.ИздательствоID = и.ИздательствоID
                    LEFT JOIN Жанры ж ON к.ЖанрID = ж.ЖанрID", connection);
                SqlDataAdapter adapter = new SqlDataAdapter(command);
                DataTable booksTable = new DataTable();
                adapter.Fill(booksTable);

                // Конвертируем DataTable в список Book
                List<Book> allBooks = (from DataRow row in booksTable.Rows
                                  select new Book
                                  {
                                      КнигаID = Convert.ToInt32(row["КнигаID"]),
                                      Название = row["Название"].ToString(),
                                      ГодИздания = row["ГодИздания"] == DBNull.Value ? null : Convert.ToInt16(row["ГодИздания"]),
                                      АвторИмя = row["АвторИмя"] == DBNull.Value ? "Неизвестен" : row["АвторИмя"].ToString(),
                                      ИздательствоНазвание = row["ИздательствоНазвание"] == DBNull.Value ? "Неизвестно" : row["ИздательствоНазвание"].ToString(),
                                      ЖанрНазвание = row["ЖанрНазвание"] == DBNull.Value ? "Неопределен" : row["ЖанрНазвание"].ToString()
                                  }).ToList();
                
                // IN с LINQ
                return allBooks.Where(b => genres.Contains(b.ЖанрНазвание)).ToList();
            }
        }

        public List<User> GetUsersWithBorrowedBooks()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                 SqlCommand usersCommand = new SqlCommand(@"
                    SELECT п.ПользовательID, п.ФИО, п.Логин, п.РольID, р.Название as РольНазвание
                    FROM Пользователи п
                    INNER JOIN Роли р ON п.РольID = р.РольID", connection);
                SqlDataAdapter usersAdapter = new SqlDataAdapter(usersCommand);
                DataTable usersTable = new DataTable();
                usersAdapter.Fill(usersTable);
                
                // Получаем все записи о выданных книгах
                SqlCommand borrowedCommand = new SqlCommand("SELECT * FROM Выдачи", connection);
                SqlDataAdapter borrowedAdapter = new SqlDataAdapter(borrowedCommand);
                DataTable borrowedTable = new DataTable();
                borrowedAdapter.Fill(borrowedTable);
                
                var users = (from DataRow row in usersTable.Rows
                            select new User
                            {
                                ПользовательID = Convert.ToInt32(row["ПользовательID"]),
                                ФИО = row["ФИО"].ToString(),
                                Логин = row["Логин"].ToString(),
                                РольID = Convert.ToInt32(row["РольID"]),
                                РольНазвание = row["РольНазвание"].ToString()
                            }).ToList();
                            
                var borrowedRecords = (from DataRow row in borrowedTable.Rows
                                     select new 
                                     {
                                         ЧитательID = Convert.ToInt32(row["ЧитательID"]),
                                         КнигаID = Convert.ToInt32(row["КнигаID"])
                                     }).ToList();
                
                // EXISTS с LINQ
                return users.Where(u => borrowedRecords.Any(b => b.ЧитательID == u.ПользовательID)).ToList();
            }
        }

        public Dictionary<string, string> GetBooksStatusWithCase()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                 SqlCommand booksCommand = new SqlCommand(@"
                    SELECT к.КнигаID, к.Название
                    FROM Книги к", connection);
                SqlDataAdapter booksAdapter = new SqlDataAdapter(booksCommand);
                DataTable booksTable = new DataTable();
                booksAdapter.Fill(booksTable);
                
                // Получаем все записи о выданных книгах
                SqlCommand borrowedCommand = new SqlCommand("SELECT * FROM Выдачи", connection);
                SqlDataAdapter borrowedAdapter = new SqlDataAdapter(borrowedCommand);
                DataTable borrowedTable = new DataTable();
                borrowedAdapter.Fill(borrowedTable);
                
                var books = (from DataRow row in booksTable.Rows
                            select new Book
                            {
                                КнигаID = Convert.ToInt32(row["КнигаID"]),
                                Название = row["Название"].ToString()
                            }).ToList();
                            
                var borrowedBooks = (from DataRow row in borrowedTable.Rows
                               where row["ДатаФактическогоВозврата"] == DBNull.Value
                               select Convert.ToInt32(row["КнигаID"])).ToList();
                
                // CASE/IF с LINQ
                return books.ToDictionary(
                    b => b.Название,
                    b => borrowedBooks.Contains(b.КнигаID) ? "Выдана" : "Доступна"
                );
            }
        }

        public IEnumerable<BookBorrowInfo> GetBooksWithBorrowInfo()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                 SqlCommand booksCommand = new SqlCommand(@"
                    SELECT 
                        к.КнигаID, к.Название,
                        а.ФИО as АвторИмя
                    FROM Книги к
                    LEFT JOIN Книги_Авторы ка ON к.КнигаID = ка.КнигаID
                    LEFT JOIN Авторы а ON ка.АвторID = а.АвторID", connection);
                SqlDataAdapter booksAdapter = new SqlDataAdapter(booksCommand);
                DataTable booksTable = new DataTable();
                booksAdapter.Fill(booksTable);
                
                // Получаем все записи о выданных книгах
                SqlCommand borrowedCommand = new SqlCommand("SELECT * FROM Выдачи", connection);
                SqlDataAdapter borrowedAdapter = new SqlDataAdapter(borrowedCommand);
                DataTable borrowedTable = new DataTable();
                borrowedAdapter.Fill(borrowedTable);
                
                 SqlCommand readersCommand = new SqlCommand("SELECT * FROM Читатели", connection);
                SqlDataAdapter readersAdapter = new SqlDataAdapter(readersCommand);
                DataTable readersTable = new DataTable();
                readersAdapter.Fill(readersTable);
                
                var books = (from DataRow row in booksTable.Rows
                            select new Book
                            {
                                КнигаID = Convert.ToInt32(row["КнигаID"]),
                                Название = row["Название"].ToString(),
                                АвторИмя = row["АвторИмя"] == DBNull.Value ? "Неизвестен" : row["АвторИмя"].ToString()
                            }).ToList();
                            
                var borrowedRecords = (from DataRow row in borrowedTable.Rows
                                     select new 
                                     {
                                         КнигаID = Convert.ToInt32(row["КнигаID"]),
                                         ЧитательID = Convert.ToInt32(row["ЧитательID"]),
                                         ДатаВыдачи = row["ДатаВыдачи"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["ДатаВыдачи"]),
                                         ДатаВозврата = row["ДатаВозврата"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["ДатаВозврата"])
                                     }).ToList();
                
                var readers = (from DataRow row in readersTable.Rows
                              select new 
                              {
                                  ЧитательID = Convert.ToInt32(row["ЧитательID"]),
                                  ФИО = $"{row["Фамилия"]} {row["Имя"]} {(row["Отчество"] == DBNull.Value ? "" : row["Отчество"])}"
                              }).ToList();
                
                // LEFT OUTER JOIN с LINQ
                var result = from book in books
                            join borrowed in borrowedRecords on book.КнигаID equals borrowed.КнигаID into bookBorrows
                            from bb in bookBorrows.DefaultIfEmpty()
                            join reader in readers on bb?.ЧитательID equals reader.ЧитательID into borrowReader
                            from br in borrowReader.DefaultIfEmpty()
                            select new BookBorrowInfo
                            {
                                Название = book.Название,
                                АвторИмя = book.АвторИмя,
                                ДатаВыдачи = bb?.ДатаВыдачи,
                                ДатаВозврата = bb?.ДатаВозврата,
                                ЧитательИмя = br?.ФИО
                            };
                            
                return result;
            }
        }

        // Класс для хранения информации о книге и её выдаче
        public class BookBorrowInfo
        {
            public required string Название { get; set; } = string.Empty;
            public required string АвторИмя { get; set; } = string.Empty;
            public DateTime? ДатаВыдачи { get; set; }
            public DateTime? ДатаВозврата { get; set; }
            public string? ЧитательИмя { get; set; }
        }

        // Методы для резервного копирования и восстановления базы данных
        public async Task<(bool Success, string Message)> BackupDatabaseAsync(string backupPath)
        {
            try
            {
                // Используем фиксированный путь C:\SQLBackups для резервных копий
                string backupDirectory = @"C:\SQLBackups";
                
                // Создаем директорию, если она не существует
                if (!Directory.Exists(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }
                
                // Генерируем уникальное имя файла
                string fileName = $"Library_Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.bak";
                backupPath = Path.Combine(backupDirectory, fileName);
                
                // Получаем имя базы данных из строки подключения
                var builder = new SqlConnectionStringBuilder(_connectionString);
                string databaseName = builder.InitialCatalog;

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Формируем SQL запрос для создания резервной копии
                string query = $@"
                    BACKUP DATABASE [{databaseName}]
                    TO DISK = @BackupPath
                    WITH FORMAT, INIT, NAME = @BackupName, DESCRIPTION = @BackupDescription
                ";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@BackupPath", backupPath);
                command.Parameters.AddWithValue("@BackupName", $"{databaseName} Backup - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                command.Parameters.AddWithValue("@BackupDescription", $"Резервная копия базы данных {databaseName} создана {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                await command.ExecuteNonQueryAsync();

                return (true, $"Резервная копия базы данных успешно создана:\n{backupPath}");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка при создании резервной копии: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> RestoreDatabaseAsync(string backupPath)
        {
            try
            {
                // Проверяем существование файла резервной копии
                if (!File.Exists(backupPath))
                {
                    return (false, $"Файл резервной копии не найден: {backupPath}");
                }
                
                // Проверяем, находится ли файл в директории C:\SQLBackups
                string backupDirectory = @"C:\SQLBackups";
                if (!backupPath.StartsWith(backupDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    // Если файл находится в другой директории, копируем его в C:\SQLBackups
                    string fileName = Path.GetFileName(backupPath);
                    string newBackupPath = Path.Combine(backupDirectory, fileName);
                    
                    try
                    {
                        File.Copy(backupPath, newBackupPath, true);
                        backupPath = newBackupPath;
                    }
                    catch (Exception ex)
                    {
                        return (false, $"Не удалось скопировать файл в директорию SQL Server: {ex.Message}");
                    }
                }

                // Подключаемся к мастер-базе для возможности восстановления
                var masterConnectionString = new SqlConnectionStringBuilder(_connectionString)
                {
                    InitialCatalog = "master"
                }.ConnectionString;

                using var masterConnection = new SqlConnection(masterConnectionString);
                await masterConnection.OpenAsync();

                // Получаем имя базы данных из строки подключения
                var builder = new SqlConnectionStringBuilder(_connectionString);
                string databaseName = builder.InitialCatalog;

                // Закрываем все соединения с БД
                string killConnectionsQuery = $@"
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                ";

                using (var killCommand = new SqlCommand(killConnectionsQuery, masterConnection))
                {
                    await killCommand.ExecuteNonQueryAsync();
                }

                // Формируем SQL запрос для восстановления базы данных
                string restoreQuery = $@"
                    RESTORE DATABASE [{databaseName}]
                    FROM DISK = @BackupPath
                    WITH REPLACE, STATS = 10;
                    
                    ALTER DATABASE [{databaseName}] SET MULTI_USER;
                ";

                using (var command = new SqlCommand(restoreQuery, masterConnection))
                {
                    command.Parameters.AddWithValue("@BackupPath", backupPath);
                    await command.ExecuteNonQueryAsync();
                }

                return (true, $"База данных успешно восстановлена из резервной копии:\n{backupPath}");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка при восстановлении базы данных: {ex.Message}");
            }
        }

        public static void ExportBooks()
        {
            try
            {
                MessageBox.Show("Функция экспорта в разработке!", "Информация", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void ExportReaders()
        {
            try
            {
                MessageBox.Show("Функция экспорта читателей в разработке!", "Информация", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void ExportLoans()
        {
            try
            {
                MessageBox.Show("Функция экспорта выдач в разработке!", "Информация", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void ExportAuthors()
        {
            try
            {
                MessageBox.Show("Функция экспорта авторов в разработке!", "Информация", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void ExportPublishers()
        {
            try
            {
                MessageBox.Show("Функция экспорта издательств в разработке!", "Информация", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void ExportGenres()
        {
            try
            {
                MessageBox.Show("Функция экспорта жанров в разработке!", "Информация", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Метод для создания нового пользователя с хешированным паролем
        public static async Task<(bool Success, string Message)> CreateUserAsync(string fullName, string login, string password, int roleId)
        {
            try
            {
                var connectionString = @"Data Source=WIN-IT3KG728UQJ\SQLEXPRESS;Initial Catalog=Library;Integrated Security=True;Connect Timeout=30;TrustServerCertificate=True;";
                
                // Проверяем, существует ли пользователь с таким логином
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    
                    var checkQuery = "SELECT COUNT(*) FROM Пользователи WHERE Логин = @Логин";
                    using var checkCommand = new SqlCommand(checkQuery, connection);
                    checkCommand.Parameters.AddWithValue("@Логин", login);
                    
                    int userCount = (int)await checkCommand.ExecuteScalarAsync();
                    if (userCount > 0)
                    {
                        return (false, "Пользователь с таким логином уже существует");
                    }
                    
                    // Хешируем пароль
                    string hashedPassword = PasswordHasher.HashPassword(password);
                    
                    // Создаем нового пользователя
                    var insertQuery = @"
                        INSERT INTO Пользователи (ФИО, Логин, Пароль, РольID)
                        VALUES (@ФИО, @Логин, @Пароль, @РольID);
                        SELECT SCOPE_IDENTITY();";
                    
                    using var insertCommand = new SqlCommand(insertQuery, connection);
                    insertCommand.Parameters.AddWithValue("@ФИО", fullName);
                    insertCommand.Parameters.AddWithValue("@Логин", login);
                    insertCommand.Parameters.AddWithValue("@Пароль", hashedPassword);
                    insertCommand.Parameters.AddWithValue("@РольID", roleId);
                    
                    // Получаем ID нового пользователя
                    var userId = Convert.ToInt32(await insertCommand.ExecuteScalarAsync());
                    
                    return (true, $"Пользователь успешно создан с ID: {userId}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка при создании пользователя: {ex.Message}");
            }
        }

        // Метод для изменения пароля пользователя
        public static async Task<(bool Success, string Message)> ChangePasswordAsync(int userId, string newPassword)
        {
            try
            {
                var connectionString = @"Data Source=WIN-IT3KG728UQJ\SQLEXPRESS;Initial Catalog=Library;Integrated Security=True;Connect Timeout=30;TrustServerCertificate=True;";
                
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                
                // Хешируем новый пароль
                string hashedPassword = PasswordHasher.HashPassword(newPassword);
                
                // Обновляем пароль в базе данных
                var query = @"
                    UPDATE Пользователи
                    SET Пароль = @Пароль
                    WHERE ПользовательID = @ПользовательID";
                
                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Пароль", hashedPassword);
                command.Parameters.AddWithValue("@ПользовательID", userId);
                
                int rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected > 0)
                {
                    return (true, "Пароль успешно изменен");
                }
                else
                {
                    return (false, "Пользователь не найден");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка при изменении пароля: {ex.Message}");
            }
        }
    }
} 