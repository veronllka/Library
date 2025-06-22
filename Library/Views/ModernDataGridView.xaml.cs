using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Library.Views
{
    public partial class ModernDataGridView : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(ModernDataGridView), 
                new PropertyMetadata("Данные", OnTitleChanged));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register("Description", typeof(string), typeof(ModernDataGridView), 
                new PropertyMetadata("Описание данных", OnDescriptionChanged));

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(IEnumerable), typeof(ModernDataGridView), 
                new PropertyMetadata(null, OnItemsSourceChanged));

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public string Description
        {
            get { return (string)GetValue(DescriptionProperty); }
            set { SetValue(DescriptionProperty, value); }
        }

        public IEnumerable ItemsSource
        {
            get { return (IEnumerable)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public DataGrid DataGrid => MainDataGrid;

        public event EventHandler RefreshRequested;
        public event EventHandler ExportRequested;

        private CollectionViewSource _collectionViewSource;

        public ModernDataGridView()
        {
            InitializeComponent();
            
            // Настройка обработчиков для placeholder
            SearchTextBox.TextChanged += SearchTextBox_TextChanged;
            SearchTextBox.GotFocus += (s, e) => SearchPlaceholder.Visibility = Visibility.Collapsed;
            SearchTextBox.LostFocus += (s, e) => {
                if (string.IsNullOrEmpty(SearchTextBox.Text))
                    SearchPlaceholder.Visibility = Visibility.Visible;
            };
        }

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ModernDataGridView control)
            {
                control.TitleText.Text = e.NewValue?.ToString() ?? "Данные";
            }
        }

        private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ModernDataGridView control)
            {
                control.DescriptionText.Text = e.NewValue?.ToString() ?? "Описание данных";
            }
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ModernDataGridView control)
            {
                control.UpdateItemsSource(e.NewValue as IEnumerable);
            }
        }

        private void UpdateItemsSource(IEnumerable source)
        {
            if (source == null)
            {
                MainDataGrid.ItemsSource = null;
                CountText.Text = "0 записей";
                return;
            }

            _collectionViewSource = new CollectionViewSource { Source = source };
            MainDataGrid.ItemsSource = _collectionViewSource.View;
            
            UpdateCount();
        }

        private void UpdateCount()
        {
            if (_collectionViewSource?.View != null)
            {
                var count = _collectionViewSource.View.Cast<object>().Count();
                CountText.Text = $"{count} {GetRecordWord(count)}";
            }
        }

        private string GetRecordWord(int count)
        {
            if (count % 100 >= 11 && count % 100 <= 19)
                return "записей";
            
            var lastDigit = count % 10;
            return lastDigit switch
            {
                1 => "запись",
                2 or 3 or 4 => "записи",
                _ => "записей"
            };
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchTextBox.Text) ? 
                Visibility.Visible : Visibility.Collapsed;
                
            ClearSearchButton.Visibility = string.IsNullOrEmpty(SearchTextBox.Text) ? 
                Visibility.Collapsed : Visibility.Visible;

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_collectionViewSource?.View == null)
                return;

            var searchText = SearchTextBox.Text?.ToLower() ?? "";
            
            if (string.IsNullOrEmpty(searchText))
            {
                _collectionViewSource.View.Filter = null;
            }
            else
            {
                _collectionViewSource.View.Filter = item => 
                {
                    if (item == null) return false;
                    
                    // Поиск по всем строковым свойствам объекта
                    var properties = item.GetType().GetProperties()
                        .Where(p => p.PropertyType == typeof(string) || 
                                   p.PropertyType == typeof(int) || 
                                   p.PropertyType == typeof(int?) ||
                                   p.PropertyType == typeof(DateTime) ||
                                   p.PropertyType == typeof(DateTime?) ||
                                   p.PropertyType == typeof(DateOnly) ||
                                   p.PropertyType == typeof(DateOnly?));
                    
                    foreach (var property in properties)
                    {
                        try
                        {
                            var value = property.GetValue(item)?.ToString()?.ToLower();
                            if (!string.IsNullOrEmpty(value) && value.Contains(searchText))
                                return true;
                        }
                        catch
                        {
                            // Игнорируем ошибки при получении значений свойств
                        }
                    }
                    
                    return false;
                };
            }
            
            UpdateCount();
        }

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
            SearchTextBox.Focus();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            ExportRequested?.Invoke(this, EventArgs.Empty);
        }
    }
} 