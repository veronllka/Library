using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Library.Models;
using Library.Services;

namespace Library.Views
{
    // Конвертер для цвета статуса
    public class StatusColorConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string status)
            {
                return status switch
                {
                    "Активна" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")), // Зелёный
                    "Просрочена" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")), // Красный
                    "Возвращена" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B5CF6")), // Фиолетовый
                    _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")) // Серый по умолчанию
                };
            }
            
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public partial class ReturnBookWindow : Window
    {
        private readonly DatabaseService _databaseService;
        private ObservableCollection<Loan> _loans = new();
        private ObservableCollection<Loan> _filteredLoans = new();
        private bool _isSearchboxDefault = true;

        public ReturnBookWindow()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();

            LoadData();
        }

        private async void LoadData()
        {
            try
            {
                StatusText.Text = "Загрузка данных...";

                var loans = await _databaseService.GetActiveLoansAsync();
                _loans.Clear();
                _filteredLoans.Clear();
                
                foreach (var loan in loans)
                {
                    _loans.Add(loan);
                    _filteredLoans.Add(loan);
                }

                LoansDataGrid.ItemsSource = _filteredLoans;
                
                StatusText.Text = $"Найдено {_loans.Count} активных выдач";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}", "Ошибка", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Ошибка загрузки данных";
            }
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_isSearchboxDefault)
            {
                SearchBox.Text = string.Empty;
                _isSearchboxDefault = false;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SearchBox.Text = "Поиск по названию книги или читателю...";
                _isSearchboxDefault = true;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isSearchboxDefault)
            {
                FilterLoans();
            }
        }

        private void FilterLoans()
        {
            try
            {
                string searchText = SearchBox.Text.ToLower();
                
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    _filteredLoans.Clear();
                    foreach (var loan in _loans)
                    {
                        _filteredLoans.Add(loan);
                    }
                }
                else
                {
                    _filteredLoans.Clear();
                    foreach (var loan in _loans.Where(l => 
                        l.КнигаНазвание.ToLower().Contains(searchText) ||
                        l.ЧитательИмя.ToLower().Contains(searchText)))
                    {
                        _filteredLoans.Add(loan);
                    }
                }
                
                StatusText.Text = $"Найдено {_filteredLoans.Count} из {_loans.Count} выдач";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при фильтрации: {ex.Message}", "Ошибка", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void LoansDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ReturnSelectedBook();
        }

        private void ReturnBookButton_Click(object sender, RoutedEventArgs e)
        {
            ReturnSelectedBook();
        }

        private async void ReturnSelectedBook()
        {
            if (LoansDataGrid.SelectedItem is Loan selectedLoan)
            {
                var result = MessageBox.Show($"Вы действительно хотите оформить возврат книги\n\"{selectedLoan.КнигаНазвание}\"?", 
                                           "Подтверждение возврата", 
                                           MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _databaseService.ReturnBookAsync(selectedLoan.ВыдачаID);
                        
                        MessageBox.Show($"Книга \"{selectedLoan.КнигаНазвание}\" успешно возвращена.", 
                                      "Возврат выполнен", MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        LoadData(); // Перезагрузка списка выдач
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при возврате книги: {ex.Message}", 
                                      "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите книгу для возврата из списка.", 
                              "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
} 