using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Data;
using Library.Models;
using Library.Services;
using Library.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Windows.Threading;
using MahApps.Metro.IconPacks;

namespace Library
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly User _currentUser;
        private readonly DatabaseService _databaseService;
        private DispatcherTimer? _timeTimer;

        // Коллекции для данных
        private ObservableCollection<Book> _books = [];
        private ObservableCollection<Reader> _readers = [];
        private ObservableCollection<Loan> _loans = [];
        private ObservableCollection<Author> _authors = [];
        private ObservableCollection<Publisher> _publishers = [];
        private ObservableCollection<Genre> _genres = [];

        public MainWindow(User user)
        {
            InitializeComponent();
            _currentUser = user;
            _databaseService = new DatabaseService();

            InitializeInterface();
            LoadStatistics();
            SetupTimer();
            ConfigureRoleAccess();
        }

        private void InitializeInterface()
        {
            // Настройка пользовательского интерфейса
            UserNameText.Text = _currentUser.ФИО;
            UserRoleText.Text = _currentUser.РольНазвание;
            
            // Установка иконки в зависимости от роли
            UserRoleIcon.Kind = _currentUser.РольНазвание switch
            {
                "Администратор" => PackIconMaterialKind.Crown,
                "Библиотекарь" => PackIconMaterialKind.BookOpen,
                "Читатель" => PackIconMaterialKind.AccountCircle,
                _ => PackIconMaterialKind.Account
            };

            StatusText.Text = $"Добро пожаловать, {_currentUser.ФИО}!";
        }

        private void SetupTimer()
        {
            // Таймер для отображения времени
            _timeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timeTimer.Tick += (s, e) => TimeText.Text = DateTime.Now.ToString("HH:mm:ss");
            _timeTimer.Start();
        }

        private void ConfigureRoleAccess()
        {
            // Настройка доступа в зависимости от роли
            switch (_currentUser.РольНазвание)
            {
                case "Читатель":
                    ConfigureReaderAccess();
                    break;

                case "Библиотекарь":
                    ConfigureLibrarianAccess();
                    break;

                case "Администратор":
                    ConfigureAdminAccess();
                    break;

                default:
                    // По умолчанию минимальные права
                    ConfigureReaderAccess();
                    break;
            }
        }

        private void ConfigureReaderAccess()
        {
            // 📚 ЧИТАТЕЛЬ - только просмотр книг и своих выдач
            var elementsToHide = new[] { 
                "AuthorsButton",        // Нет доступа к авторам  
                "ReadersButton",        // Нет доступа к читателям
                "ReportsMenuButton",    // Нет доступа к отчетам
                "IssueBookButton",      // Не может выдавать книги
                "BookReturnButton",     // Не может принимать возврат
                "BackupDatabaseButton", // Нет доступа к резервному копированию
                "ExportButton",         // Нет доступа к экспорту
                "ImportButton",         // Нет доступа к импорту
                "LinqButton",           // Нет доступа к демонстрации LINQ
                "AdvancedDatabaseButton", // Нет доступа к сложным объектам БД
                "QuickActionsPanel"     // Скрываем быстрые действия
            };

            HideElementsByName(elementsToHide);
            ShowWelcomeMessageForReader();
        }

        private void ConfigureLibrarianAccess()
        {
            // 👥 БИБЛИОТЕКАРЬ - работа с книгами, читателями, выдачами, но без критических операций
            var elementsToHide = new[] { 
                "BackupDatabaseButton", // НЕТ доступа к резервному копированию
                "ReportsMenuButton",    // НЕТ доступа к отчетам (только админ)
                "ImportButton",         // НЕТ доступа к импорту (может повредить данные)
                "LinqButton",           // НЕТ доступа к демонстрации LINQ
                "AdvancedDatabaseButton" // НЕТ доступа к сложным объектам БД (только админ)
            };

            HideElementsByName(elementsToHide);
            RestrictExportFunctionsForLibrarian();
        }

        private void ConfigureAdminAccess()
        {
            // 👑 АДМИНИСТРАТОР - полный доступ ко всем функциям
            // Все кнопки остаются видимыми
            ShowAdminWelcomeMessage();
        }

        private void RestrictExportFunctionsForLibrarian()
        {
            // Библиотекарь может экспортировать, но с ограничениями
            // Это можно настроить позже в ExportWindow
        }

        private void ShowWelcomeMessageForReader()
        {
            StatusText.Text = $"Добро пожаловать, {_currentUser.ФИО}! Вы можете просматривать каталог книг.";
        }

        private void ShowAdminWelcomeMessage()
        {
            StatusText.Text = $"Добро пожаловать, {_currentUser.ФИО}! У вас полный доступ к системе.";
        }

        private void HideButtonsByTag(string[] tags)
        {
            foreach (var tag in tags)
            {
                var buttons = FindVisualChildren<Button>(this)
                    .Where(b => b.Tag != null && b.Tag.ToString() == tag);
                
                foreach (var button in buttons)
                {
                    button.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void HideElementsByName(string[] names)
        {
            foreach (var name in names)
            {
                var element = FindName(name) as FrameworkElement;
                if (element != null)
                {
                    element.Visibility = Visibility.Collapsed;
                }
            }
        }
        
        // Вспомогательный метод для поиска элементов в визуальном дереве
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                
                if (child is T t)
                    yield return t;

                foreach (var childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }

        private async void LoadStatistics()
        {
            try
            {
                var books = await _databaseService.GetBooksAsync();
                var readers = await _databaseService.GetReadersAsync();
                var loans = await _databaseService.GetLoansAsync();

                TotalBooksText.Text = books.Count.ToString();
                TotalReadersText.Text = readers.Count.ToString();
                
                var activeLoans = loans.Where(l => l.ДатаФактическогоВозврата == null).Count();
                ActiveLoansText.Text = activeLoans.ToString();
                
                var overdueLoans = loans.Where(l => l.ДатаФактическогоВозврата == null && 
                                                   l.ДатаВозврата < DateOnly.FromDateTime(DateTime.Now)).Count();
                OverdueLoansText.Text = overdueLoans.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки статистики: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PlayContentAnimation()
        {
            var storyboard = (Storyboard)FindResource("ContentSlideAnimation");
            storyboard.Begin(ContentArea);
        }

        private void ShowContent(string title, string description, UIElement content)
        {
            CurrentSectionText.Text = title;
            CurrentSectionDescription.Text = description;
            
            WelcomePanel.Visibility = Visibility.Collapsed;
            ContentArea.Children.Clear();
            ContentArea.Children.Add(content);
            
            PlayContentAnimation();
            StatusText.Text = $"Открыт раздел: {title}";
        }

        private async void BooksButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadBooksAsync();
        }

        private async System.Threading.Tasks.Task LoadBooksAsync()
        {
            try
            {
                _books.Clear();
                var books = await _databaseService.GetBooksAsync();
                foreach (var book in books)
                    _books.Add(book);

                var dataGrid = CreateBooksDataGrid();
                ShowContent("📚 Управление книгами", 
                          $"Каталог содержит {_books.Count} книг", dataGrid);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки книг: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportBooks()
        {
            DatabaseService.ExportBooks();
        }

        private async void ReadersButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadReadersAsync();
        }

        private async System.Threading.Tasks.Task LoadReadersAsync()
        {
            try
            {
                _readers.Clear();
                var readers = await _databaseService.GetReadersAsync();
                foreach (var reader in readers)
                    _readers.Add(reader);

                var dataGrid = CreateReadersDataGrid();
                ShowContent("👥 Управление читателями", 
                          $"Зарегистрировано {_readers.Count} читателей", dataGrid);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки читателей: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportReaders()
        {
            DatabaseService.ExportReaders();
        }

        private async void LoansButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadLoansAsync();
        }

        private async System.Threading.Tasks.Task LoadLoansAsync()
        {
            try
            {
                _loans.Clear();
                var loans = await _databaseService.GetLoansAsync();
                
                // Для читателей показываем только их выдачи
                if (_currentUser.РольНазвание == "Читатель")
                {
                    // Фильтруем только выдачи текущего читателя
                    // Предполагается, что у пользователя есть связь с читателем через ФИО
                    loans = loans.Where(l => l.ЧитательИмя.Contains(_currentUser.ФИО) || 
                                           _currentUser.ФИО.Contains(l.ЧитательИмя.Split(' ')[0])).ToList();
                }
                
                foreach (var loan in loans)
                    _loans.Add(loan);

                string title = _currentUser.РольНазвание == "Читатель" ? "📋 Мои выдачи" : "📋 Управление выдачами";
                string description = _currentUser.РольНазвание == "Читатель" ? 
                    $"У вас {_loans.Count} выдач(и)" : 
                    $"Всего выдач: {_loans.Count}";
                    
                var dataGrid = CreateLoansDataGrid();
                ShowContent(title, description, dataGrid);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки выдач: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportLoans()
        {
            DatabaseService.ExportLoans();
        }

        private async void AuthorsButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadAuthorsAsync();
        }

        private async System.Threading.Tasks.Task LoadAuthorsAsync()
        {
            try
            {
                _authors.Clear();
                var authors = await _databaseService.GetAuthorsAsync();
                foreach (var author in authors)
                    _authors.Add(author);

                var dataGrid = CreateAuthorsDataGrid();
                ShowContent("✍️ Управление авторами", 
                          $"В базе {_authors.Count} авторов", dataGrid);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки авторов: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportAuthors()
        {
            DatabaseService.ExportAuthors();
        }

        private async void PublishersButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadPublishersAsync();
        }

        private async System.Threading.Tasks.Task LoadPublishersAsync()
        {
            try
            {
                _publishers.Clear();
                var publishers = await _databaseService.GetPublishersAsync();
                foreach (var publisher in publishers)
                    _publishers.Add(publisher);

                var dataGrid = CreatePublishersDataGrid();
                ShowContent("🏢 Управление издательствами", 
                          $"В базе {_publishers.Count} издательств", dataGrid);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки издательств: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportPublishers()
        {
            DatabaseService.ExportPublishers();
        }

        private async void GenresButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadGenresAsync();
        }

        private async System.Threading.Tasks.Task LoadGenresAsync()
        {
            try
            {
                _genres.Clear();
                var genres = await _databaseService.GetGenresAsync();
                foreach (var genre in genres)
                    _genres.Add(genre);

                var dataGrid = CreateGenresDataGrid();
                ShowContent("🎭 Управление жанрами", 
                          $"В базе {_genres.Count} жанров", dataGrid);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки жанров: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportGenres()
        {
            DatabaseService.ExportGenres();
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var reportsWindow = new Views.ReportsWindow();
                reportsWindow.Owner = this;
                reportsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии окна отчетов: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void QuickIssueButton_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем права доступа
            if (!HasAccessToFeature("issueBook"))
            {
                ShowAccessDeniedMessage("issueBook");
                return;
            }

            try
            {
                var issueWindow = new IssueBookWindow();
                issueWindow.Owner = this;
                if (issueWindow.ShowDialog() == true)
                {
                    LoadStatistics(); // Обновляем статистику
                    StatusText.Text = "Книга успешно выдана";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выдаче книги: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void QuickReturnButton_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем права доступа
            if (!HasAccessToFeature("bookReturn"))
            {
                ShowAccessDeniedMessage("bookReturn");
                return;
            }

            try
            {
                var loans = await _databaseService.GetActiveLoansAsync();
                if (!loans.Any())
                {
                    MessageBox.Show("Нет активных выдач для возврата.", "Информация", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Открываем окно возврата книги
                var returnBookWindow = new ReturnBookWindow();
                returnBookWindow.Owner = this;
                returnBookWindow.ShowDialog();
                
                // Обновляем статистику после закрытия окна
                LoadStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при возврате книги: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var logoutDialog = new Views.LogoutDialog();
            logoutDialog.Owner = this;
            
            if (logoutDialog.ShowDialog() == true && logoutDialog.ShouldLogout)
            {
                // Плавный переход к экрану входа
                _timeTimer?.Stop();
                
                // Создаем анимацию закрытия
                var fadeOut = new Storyboard();
                var opacityAnimation = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(400)
                };
                opacityAnimation.EasingFunction = new QuarticEase 
                { 
                    EasingMode = EasingMode.EaseIn 
                };
                
                Storyboard.SetTarget(opacityAnimation, this);
                Storyboard.SetTargetProperty(opacityAnimation, 
                    new PropertyPath("Opacity"));
                
                fadeOut.Children.Add(opacityAnimation);
                
                fadeOut.Completed += (s, args) =>
                {
                    var loginWindow = new LoginWindow();
                    loginWindow.Show();
                    this.Close();
                };
                
                fadeOut.Begin();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы действительно хотите закрыть приложение?", 
                                       "Подтверждение", MessageBoxButton.YesNo, 
                                       MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _timeTimer?.Stop();
                Application.Current.Shutdown();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        // Методы создания красивых DataGrid для различных сущностей
        private Views.ModernDataGridView CreateBooksDataGrid()
        {
            var dataGridView = new Views.ModernDataGridView
            {
                Title = "📚 Каталог книг",
                Description = "Управление книжным фондом библиотеки",
                ItemsSource = _books
            };

            var dataGrid = dataGridView.DataGrid;
            dataGrid.MinHeight = 400;

            // Определение колонок для книг
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "ID", 
                Binding = new Binding("КнигаID"),
                Width = 80
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "📖 Название", 
                Binding = new Binding("Название"),
                Width = 250
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "✍️ Автор", 
                Binding = new Binding("АвторИмя"),
                Width = 180
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "📅 Год", 
                Binding = new Binding("ГодИздания"),
                Width = 100
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "🏢 Издательство", 
                Binding = new Binding("ИздательствоНазвание"),
                Width = 180
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "🎭 Жанр", 
                Binding = new Binding("ЖанрНазвание"),
                Width = 140
            });

            // Подписка на события
            dataGridView.RefreshRequested += async (s, e) => await LoadBooksAsync();
            dataGridView.ExportRequested += (s, e) => ExportBooks();

            return dataGridView;
        }

        private Views.ModernDataGridView CreateReadersDataGrid()
        {
            var dataGridView = new Views.ModernDataGridView
            {
                Title = "👥 Читатели библиотеки",
                Description = "Управление читательским составом",
                ItemsSource = _readers
            };

            var dataGrid = dataGridView.DataGrid;
            dataGrid.MinHeight = 400;

            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "ID", 
                Binding = new Binding("ЧитательID"),
                Width = 80
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "👤 Фамилия", 
                Binding = new Binding("Фамилия"),
                Width = 150
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "👤 Имя", 
                Binding = new Binding("Имя"),
                Width = 150
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "👤 Отчество", 
                Binding = new Binding("Отчество"),
                Width = 150
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "🎂 Дата рождения", 
                Binding = new Binding("ДатаРождения") { StringFormat = "dd.MM.yyyy" },
                Width = 140
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "📞 Телефон", 
                Binding = new Binding("Телефон"),
                Width = 160
            });

            dataGridView.RefreshRequested += async (s, e) => await LoadReadersAsync();
            dataGridView.ExportRequested += (s, e) => ExportReaders();

            return dataGridView;
        }

        private Views.ModernDataGridView CreateLoansDataGrid()
        {
            var dataGridView = new Views.ModernDataGridView
            {
                Title = "📋 Журнал выдач",
                Description = "Отслеживание выданных и возвращенных книг",
                ItemsSource = _loans
            };

            var dataGrid = dataGridView.DataGrid;
            dataGrid.MinHeight = 400;

            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "ID", 
                Binding = new Binding("ВыдачаID"),
                Width = 80
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "👤 Читатель", 
                Binding = new Binding("ЧитательИмя"),
                Width = 200
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "📖 Книга", 
                Binding = new Binding("КнигаНазвание"),
                Width = 220
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "📅 Дата выдачи", 
                Binding = new Binding("ДатаВыдачи") { StringFormat = "dd.MM.yyyy" },
                Width = 140
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "🔄 Дата возврата", 
                Binding = new Binding("ДатаВозврата") { StringFormat = "dd.MM.yyyy" },
                Width = 140
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "📊 Статус", 
                Binding = new Binding("Статус"),
                Width = 120
            });

            dataGridView.RefreshRequested += async (s, e) => await LoadLoansAsync();
            dataGridView.ExportRequested += (s, e) => ExportLoans();

            return dataGridView;
        }

        private Views.ModernDataGridView CreateAuthorsDataGrid()
        {
            var dataGridView = new Views.ModernDataGridView
            {
                Title = "✍️ База авторов",
                Description = "Справочник писателей и литераторов",
                ItemsSource = _authors
            };

            var dataGrid = dataGridView.DataGrid;
            dataGrid.MinHeight = 400;

            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "ID", 
                Binding = new Binding("АвторID"),
                Width = 80
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "✍️ Полное имя", 
                Binding = new Binding("ФИО"),
                Width = 400
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "🎂 Год рождения", 
                Binding = new Binding("ГодРождения"),
                Width = 150
            });

            dataGridView.RefreshRequested += async (s, e) => await LoadAuthorsAsync();
            dataGridView.ExportRequested += (s, e) => ExportAuthors();

            return dataGridView;
        }

        private Views.ModernDataGridView CreatePublishersDataGrid()
        {
            var dataGridView = new Views.ModernDataGridView
            {
                Title = "🏢 Издательства",
                Description = "Справочник издательских домов и компаний",
                ItemsSource = _publishers
            };

            var dataGrid = dataGridView.DataGrid;
            dataGrid.MinHeight = 400;

            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "ID", 
                Binding = new Binding("ИздательствоID"),
                Width = 80
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "🏢 Название", 
                Binding = new Binding("Название"),
                Width = 350
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "🌍 Город", 
                Binding = new Binding("Город"),
                Width = 200
            });

            dataGridView.RefreshRequested += async (s, e) => await LoadPublishersAsync();
            dataGridView.ExportRequested += (s, e) => ExportPublishers();

            return dataGridView;
        }

        private Views.ModernDataGridView CreateGenresDataGrid()
        {
            var dataGridView = new Views.ModernDataGridView
            {
                Title = "🎭 Литературные жанры",
                Description = "Классификация книг по жанрам и направлениям",
                ItemsSource = _genres
            };

            var dataGrid = dataGridView.DataGrid;
            dataGrid.MinHeight = 400;

            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "ID", 
                Binding = new Binding("ЖанрID"),
                Width = 80
            });
            dataGrid.Columns.Add(new DataGridTextColumn 
            { 
                Header = "🎭 Название жанра", 
                Binding = new Binding("Название"),
                Width = 450
            });

            dataGridView.RefreshRequested += async (s, e) => await LoadGenresAsync();
            dataGridView.ExportRequested += (s, e) => ExportGenres();

            return dataGridView;
        }

        private UserControl CreateReportsPanel()
        {
            var userControl = new UserControl();
            var stackPanel = new StackPanel();

            var titleText = new TextBlock
            {
                Text = "📊 Статистика и отчеты",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 20)
            };

            var descText = new TextBlock
            {
                Text = "Здесь будет отображаться детальная статистика работы библиотеки.",
                FontSize = 14,
                Foreground = (Brush)FindResource("TextSecondaryColor"),
                Margin = new Thickness(0, 0, 0, 30)
            };

            stackPanel.Children.Add(titleText);
            stackPanel.Children.Add(descText);

            userControl.Content = stackPanel;
            return userControl;
        }

        private void DemonstrateLinqQueries()
        {
            // Демонстрация LINQ-запросов
            try
            {
                // 1. ORDER BY - сортировка книг по году издания
                var sortedBooks = _databaseService.GetBooksSortedByYear();
                MessageBox.Show($"Отсортированные книги по году издания (убывание):\n{string.Join("\n", sortedBooks.Select(b => $"{b.Название} ({b.ГодИздания})"))}",
                                "ORDER BY с LINQ");

                // 2. IN - поиск книг определенных жанров
                var genreBooks = _databaseService.GetBooksByGenres(new List<string> { "Роман", "Детектив", "Фантастика" });
                MessageBox.Show($"Книги жанров Роман, Детектив, Фантастика:\n{string.Join("\n", genreBooks.Select(b => $"{b.Название} - {b.ЖанрНазвание}"))}",
                                "IN с LINQ");
                
                // 3. EXISTS - пользователи, которые брали книги
                var borrowers = _databaseService.GetUsersWithBorrowedBooks();
                MessageBox.Show($"Пользователи, бравшие книги:\n{string.Join("\n", borrowers.Select(u => $"{u.ФИО}"))}",
                                "EXISTS с LINQ");
                
                // 4. CASE - статус книг (выдана/доступна)
                var booksStatuses = _databaseService.GetBooksStatusWithCase();
                MessageBox.Show($"Статусы книг:\n{string.Join("\n", booksStatuses.Select(kv => $"{kv.Key}: {kv.Value}"))}",
                                 "CASE с LINQ");
                
                // 5. LEFT OUTER JOIN - информация о выданных книгах
                var borrowInfo = _databaseService.GetBooksWithBorrowInfo().ToList();
                MessageBox.Show($"Информация о выдаче книг:\n{string.Join("\n", borrowInfo.Select(bi => 
                                $"{bi.Название} - {(bi.ЧитательИмя != null ? $"Выдана {bi.ЧитательИмя}" : "Не выдана")}"))}",
                                "LEFT OUTER JOIN с LINQ");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выполнении LINQ-запросов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DemonstrateLinqButton_Click(object sender, RoutedEventArgs e)
        {
            DemonstrateLinqQueries();
        }

        private void OpenAdvancedDatabaseWindow()
        {
            if (!HasAccessToFeature("advanced_db"))
            {
                ShowAccessDeniedMessage("работе со сложными объектами базы данных");
                return;
            }

            try
            {
                var advancedDbWindow = new Views.AdvancedDatabaseWindow();
                advancedDbWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии окна сложных объектов БД: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenSqlAdministrationWindow()
        {
            if (!HasAccessToFeature("sql_admin"))
            {
                ShowAccessDeniedMessage("sql_admin");
                return;
            }

            try
            {
                StatusText.Text = "Проверка подключения к SQL Server...";
                
                // Проверяем, что SQL Server доступен
                var (isConnected, errorMessage) = DatabaseService.TestDatabaseConnectionAsync().Result;
                
                if (!isConnected)
                {
                    var result = MessageBox.Show(
                        $"Не удается подключиться к SQL Server:\n{errorMessage}\n\n" +
                        "Возможные причины:\n" +
                        "• SQL Server не запущен\n" +
                        "• Неверные параметры подключения\n" +
                        "• Отсутствует база данных 'Library'\n\n" +
                        "Открыть окно администрирования в режиме диагностики?",
                        "Проблема с подключением",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    
                    if (result != MessageBoxResult.Yes)
                    {
                        StatusText.Text = "Отменено пользователем";
                        return;
                    }
                }
                
                StatusText.Text = "Открытие окна администрирования SQL Server...";
                
                var sqlAdminWindow = new Views.DatabaseAdministrationWindow();
                sqlAdminWindow.Owner = this;
                sqlAdminWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                sqlAdminWindow.ShowDialog();
                
                StatusText.Text = "Система готова к работе";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Критическая ошибка при открытии окна администрирования SQL Server:\n\n" +
                    $"Ошибка: {ex.Message}\n\n" +
                    $"Тип ошибки: {ex.GetType().Name}\n\n" +
                    "Рекомендации:\n" +
                    "• Убедитесь, что SQL Server Express установлен и запущен\n" +
                    "• Проверьте настройки подключения к базе данных\n" +
                    "• Перезапустите приложение с правами администратора", 
                    "Критическая ошибка", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
                
                StatusText.Text = "Ошибка открытия окна администрирования";
            }
        }

        private void OpenReturnBookWindow()
        {
            try
            {
                var returnBookWindow = new ReturnBookWindow();
                returnBookWindow.Owner = this;
                returnBookWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть окно возврата книг: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void OpenDatabaseBackupWindow()
        {
            try
            {
                var databaseBackupWindow = new DatabaseBackupWindow();
                databaseBackupWindow.Owner = this;
                databaseBackupWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть окно резервного копирования базы данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenExportWindow()
        {
            try
            {
                var exportWindow = new Views.ExportWindow();
                exportWindow.Owner = this;
                exportWindow.ShowDialog();
                
                // Обновляем статистику после возможного экспорта
                LoadStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть окно экспорта данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenImportWindow()
        {
            try
            {
                var importWindow = new Views.ImportWindow();
                importWindow.Owner = this;
                importWindow.ShowDialog();
                
                // Обновляем статистику и данные после возможного импорта
                LoadStatistics();
                
                // Если открыт раздел с книгами, обновляем его
                if (_currentView == "books")
                {
                    LoadBooksAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть окно импорта данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string? tag = button.Tag as string;

                // Проверяем права доступа перед выполнением действия
                if (!HasAccessToFeature(tag))
                {
                    ShowAccessDeniedMessage(tag);
                    return;
                }

                switch (tag)
                {
                    case "books":
                        LoadBooksData();
                        _currentView = "books";
                        UpdateVisibility();
                        break;

                    case "authors":
                        LoadAuthorsData();
                        _currentView = "authors";
                        UpdateVisibility();
                        break;

                    case "readers":
                        LoadReadersData();
                        _currentView = "readers";
                        UpdateVisibility();
                        break;

                    case "loans":
                        LoadLoansData();
                        _currentView = "loans";
                        UpdateVisibility();
                        break;

                    case "reports":
                        GenerateReport();
                        break;

                    case "issueBook":
                        OpenIssueBookDialog();
                        break;
                        
                    case "bookReturn":
                        OpenReturnBookWindow();
                        break;
                        
                    case "backupDatabase":
                        OpenDatabaseBackupWindow();
                        break;

                    case "export":
                        OpenExportWindow();
                        break;
                        
                    case "import":
                        OpenImportWindow();
                        break;

                    case "linq":
                        DemonstrateLinqQueries();
                        break;
                        
                    case "advanced_db":
                        OpenAdvancedDatabaseWindow();
                        break;
                        
                    case "sql_admin":
                        OpenSqlAdministrationWindow();
                        break;
                }
            }
        }

        private bool HasAccessToFeature(string? feature)
        {
            if (string.IsNullOrEmpty(feature)) return false;

            return _currentUser.РольНазвание switch
            {
                "Администратор" => true, // Полный доступ

                "Библиотекарь" => feature switch
                {
                    "books" => true,
                    "authors" => true,
                    "readers" => true,
                    "loans" => true,
                    "issueBook" => true,
                    "bookReturn" => true,
                    "export" => true,
                    "backupDatabase" => false,  // НЕТ доступа
                    "import" => false,          // НЕТ доступа
                    "reports" => false,         // НЕТ доступа
                    "linq" => false,            // НЕТ доступа
                    "advanced_db" => false,     // НЕТ доступа (только администратор)
                    "sql_admin" => false,       // НЕТ доступа (только администратор)
                    _ => false
                },

                "Читатель" => feature switch
                {
                    "books" => true,       // Только просмотр книг
                    "loans" => true,       // Только просмотр своих выдач
                    _ => false             // Все остальное запрещено
                },

                _ => false // По умолчанию нет доступа
            };
        }

        private void ShowAccessDeniedMessage(string? feature)
        {
            string featureName = feature switch
            {
                "readers" => "Читатели",
                "authors" => "Авторы",
                "reports" => "Отчеты",
                "issueBook" => "Выдача книг",
                "bookReturn" => "Возврат книг",
                "backupDatabase" => "Резервное копирование",
                "export" => "Экспорт данных",
                "import" => "Импорт данных",
                "linq" => "Демонстрация LINQ",
                "advanced_db" => "Сложные объекты базы данных",
                "sql_admin" => "Администрирование SQL Server",
                _ => "данная функция"
            };

            MessageBox.Show($"Доступ запрещен!\n\nУ вас нет прав для использования раздела \"{featureName}\".\n\nВаша роль: {_currentUser.РольНазвание}", 
                          "Ограничение доступа", 
                          MessageBoxButton.OK, 
                          MessageBoxImage.Warning);

            StatusText.Text = $"Доступ запрещен: {featureName}";
        }

        private string _currentView = "dashboard";
        
        private void UpdateVisibility()
        {
            // Метод для обновления видимости элементов в зависимости от текущего представления
            WelcomePanel.Visibility = _currentView == "dashboard" ? Visibility.Visible : Visibility.Collapsed;
            // Здесь можно добавить другие элементы, которые нужно показывать/скрывать
        }
        
        private void LoadBooksData()
        {
            LoadBooksAsync().ConfigureAwait(false);
        }
        
        private void LoadAuthorsData()
        {
            LoadAuthorsAsync().ConfigureAwait(false);
        }
        
        private void LoadReadersData()
        {
            LoadReadersAsync().ConfigureAwait(false);
        }
        
        private void LoadLoansData()
        {
            LoadLoansAsync().ConfigureAwait(false);
        }
        
        private void GenerateReport()
        {
            ReportsButton_Click(null, null);
        }
        
        private void OpenIssueBookDialog()
        {
            QuickIssueButton_Click(null, null);
        }
    }

    // Конвертеры для отображения статуса
    public class StatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Loan loan)
            {
                if (loan.Возвращена)
                    return "✅ Возвращена";
                else if (loan.Просрочена)
                    return "⚠️ Просрочена";
                else
                    return "📖 Активна";
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Loan loan)
            {
                if (loan.Возвращена)
                    return Brushes.LightGreen;
                else if (loan.Просрочена)
                    return Brushes.Orange;
                else
                    return Brushes.LightBlue;
            }
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}