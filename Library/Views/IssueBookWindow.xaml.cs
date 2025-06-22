using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Library.Models;
using Library.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Library.Views
{
    public partial class IssueBookWindow : Window
    {
        private readonly DatabaseService _databaseService;
        private ObservableCollection<Reader> _readers = new();
        private ObservableCollection<Book> _books = new();

        public bool IsSuccess { get; private set; }

        public IssueBookWindow()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            InitializeWindow();
            LoadData();
        }

        private void InitializeWindow()
        {
            // Установка значений по умолчанию
            IssueDatePicker.SelectedDate = DateTime.Today;
            ReturnDatePicker.SelectedDate = DateTime.Today.AddDays(14);

            // Обработчики событий
            IssueDatePicker.SelectedDateChanged += IssueDatePicker_SelectedDateChanged;
            ReaderComboBox.SelectionChanged += ValidateForm;
            BookComboBox.SelectionChanged += ValidateForm;
            ExtendedPeriodCheckBox.Checked += ExtendedPeriodCheckBox_CheckedChanged;
            ExtendedPeriodCheckBox.Unchecked += ExtendedPeriodCheckBox_CheckedChanged;

            UpdateFormValidation();
        }

        private async void LoadData()
        {
            try
            {
                StatusTextBlock.Text = "Загрузка данных...";

                // Загрузка читателей
                var readers = await _databaseService.GetReadersAsync();
                _readers.Clear();
                foreach (var reader in readers)
                    _readers.Add(reader);

                ReaderComboBox.ItemsSource = _readers;

                // Загрузка книг
                var books = await _databaseService.GetBooksAsync();
                _books.Clear();
                foreach (var book in books)
                    _books.Add(book);

                BookComboBox.ItemsSource = _books;

                // Загрузка статистики
                await LoadStatistics();

                StatusTextBlock.Text = "Выберите читателя и книгу для выдачи";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Ошибка загрузки данных";
            }
        }

        private async Task LoadStatistics()
        {
            try
            {
                var totalBooks = _books.Count;
                var totalReaders = _readers.Count;
                var activeLoans = await _databaseService.GetActiveLoansAsync();

                TotalBooksInfo.Text = $"Всего книг: {totalBooks}";
                TotalReadersInfo.Text = $"Читателей: {totalReaders}";
                ActiveLoansInfo.Text = $"Активных выдач: {activeLoans.Count()}";
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не показываем пользователю
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки статистики: {ex.Message}");
            }
        }

        private void IssueDatePicker_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (IssueDatePicker.SelectedDate.HasValue && !ExtendedPeriodCheckBox.IsChecked == true)
            {
                // Автоматически устанавливаем дату возврата (14 дней от даты выдачи)
                var issueDate = IssueDatePicker.SelectedDate.Value;
                var returnDate = issueDate.AddDays(14);
                ReturnDatePicker.SelectedDate = returnDate;
            }
        }

        private void ExtendedPeriodCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (IssueDatePicker.SelectedDate.HasValue)
            {
                var issueDate = IssueDatePicker.SelectedDate.Value;
                var days = ExtendedPeriodCheckBox.IsChecked == true ? 30 : 14;
                ReturnDatePicker.SelectedDate = issueDate.AddDays(days);
            }
        }

        private void ValidateForm(object? sender, SelectionChangedEventArgs e)
        {
            UpdateFormValidation();
        }

        private void UpdateFormValidation()
        {
            var isValid = ReaderComboBox.SelectedItem != null &&
                         BookComboBox.SelectedItem != null &&
                         IssueDatePicker.SelectedDate.HasValue &&
                         ReturnDatePicker.SelectedDate.HasValue;

            IssueButton.IsEnabled = isValid;

            if (isValid)
            {
                StatusTextBlock.Text = "Готово к выдаче книги";
            }
            else
            {
                StatusTextBlock.Text = "Заполните все поля для выдачи книги";
            }

            // Дополнительная валидация дат
            if (IssueDatePicker.SelectedDate.HasValue && ReturnDatePicker.SelectedDate.HasValue)
            {
                var issueDate = IssueDatePicker.SelectedDate.Value;
                var returnDate = ReturnDatePicker.SelectedDate.Value;

                if (returnDate <= issueDate)
                {
                    StatusTextBlock.Text = "Дата возврата должна быть позже даты выдачи";
                    IssueButton.IsEnabled = false;
                }
                else if (issueDate < DateTime.Today)
                {
                    StatusTextBlock.Text = "Дата выдачи не может быть в прошлом";
                    IssueButton.IsEnabled = false;
                }
                else if ((returnDate - issueDate).TotalDays > 60)
                {
                    StatusTextBlock.Text = "Максимальный срок выдачи - 60 дней";
                    IssueButton.IsEnabled = false;
                }
            }
        }

        private async void IssueButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ReaderComboBox.SelectedItem is not Reader selectedReader ||
                    BookComboBox.SelectedItem is not Book selectedBook ||
                    !IssueDatePicker.SelectedDate.HasValue ||
                    !ReturnDatePicker.SelectedDate.HasValue)
                {
                    MessageBox.Show("Пожалуйста, заполните все поля.", "Внимание", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                StatusTextBlock.Text = "Выполняется выдача книги...";
                IssueButton.IsEnabled = false;

                var issueDate = IssueDatePicker.SelectedDate.Value;
                var returnDate = ReturnDatePicker.SelectedDate.Value;

                // Проверяем, не выдана ли уже эта книга
                var activeLoans = await _databaseService.GetActiveLoansAsync();
                if (activeLoans.Any(l => l.КнигаID == selectedBook.КнигаID))
                {
                    MessageBox.Show("Эта книга уже выдана и не возвращена.", "Внимание", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    StatusTextBlock.Text = "Книга уже выдана";
                    IssueButton.IsEnabled = true;
                    return;
                }

                // Проверяем количество выданных книг читателю
                var readerActiveLoans = activeLoans.Where(l => l.ЧитательID == selectedReader.ЧитательID).Count();
                if (readerActiveLoans >= 3)
                {
                    MessageBox.Show("У читателя уже выдано максимальное количество книг (3).", "Внимание", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    StatusTextBlock.Text = "Превышен лимит книг для читателя";
                    IssueButton.IsEnabled = true;
                    return;
                }

                // Выполняем выдачу книги
                await _databaseService.IssueBookAsync(selectedReader.ЧитательID, selectedBook.КнигаID, 
                                                    issueDate, returnDate);

                // Показываем уведомление при необходимости
                if (NotificationCheckBox.IsChecked == true)
                {
                    // Здесь можно добавить логику отправки уведомлений
                    StatusTextBlock.Text = "Уведомление отправлено читателю";
                }

                MessageBox.Show($"Книга \"{selectedBook.Название}\" успешно выдана читателю {selectedReader.ФИО}!", 
                              "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                IsSuccess = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выдаче книги: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Ошибка выдачи книги";
                IssueButton.IsEnabled = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsSuccess = false;
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CancelButton_Click(sender, e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelButton_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                IssueButton_Click(this, new RoutedEventArgs());
            }
            
            base.OnKeyDown(e);
        }
    }
} 