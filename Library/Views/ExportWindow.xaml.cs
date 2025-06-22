using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Library.Services;

namespace Library.Views
{
    public partial class ExportWindow : Window
    {
        private readonly ImportExportService _importExportService;
        private string _outputPath;

        public ExportWindow()
        {
            InitializeComponent();
            _importExportService = new ImportExportService();
            _outputPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            OutputPathTextBox.Text = _outputPath;
            
            BooksCheckBox.Checked += UpdatePreview;
            BooksCheckBox.Unchecked += UpdatePreview;
            AuthorsCheckBox.Checked += UpdatePreview;
            AuthorsCheckBox.Unchecked += UpdatePreview;
            ReadersCheckBox.Checked += UpdatePreview;
            ReadersCheckBox.Unchecked += UpdatePreview;
            LoansCheckBox.Checked += UpdatePreview;
            LoansCheckBox.Unchecked += UpdatePreview;
            PublishersCheckBox.Checked += UpdatePreview;
            PublishersCheckBox.Unchecked += UpdatePreview;
            GenresCheckBox.Checked += UpdatePreview;
            GenresCheckBox.Unchecked += UpdatePreview;
            
            FormatComboBox.SelectionChanged += UpdatePreview;
            
            UpdatePreview(null, null);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void UpdatePreview(object sender, RoutedEventArgs e)
        {
            var selectedItems = new List<string>();
            
            if (BooksCheckBox.IsChecked == true) selectedItems.Add("Книги");
            if (AuthorsCheckBox.IsChecked == true) selectedItems.Add("Авторы");
            if (ReadersCheckBox.IsChecked == true) selectedItems.Add("Читатели");
            if (LoansCheckBox.IsChecked == true) selectedItems.Add("Выдачи");
            if (PublishersCheckBox.IsChecked == true) selectedItems.Add("Издательства");
            if (GenresCheckBox.IsChecked == true) selectedItems.Add("Жанры");

            if (selectedItems.Count == 0)
            {
                PreviewTextBlock.Text = "Выберите данные для экспорта";
                ExportButton.IsEnabled = false;
                return;
            }

            var format = ((ComboBoxItem)FormatComboBox.SelectedItem)?.Content?.ToString() ?? "JSON";
            var extension = GetFileExtension(format);
            
            PreviewTextBlock.Text = $"Будет экспортировано: {string.Join(", ", selectedItems)}\n" +
                                   $"Формат: {format}\n" +
                                   $"Папка: {_outputPath}\n" +
                                   $"Файлы: *{extension}";
            
            ExportButton.IsEnabled = true;
        }

        private string GetFileExtension(string format)
        {
            return format.ToLower() switch
            {
                "json" => ".json",
                "xml" => ".xml",
                "csv" => ".csv",
                "txt" => ".txt",
                _ => ".json"
            };
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            BooksCheckBox.IsChecked = true;
            AuthorsCheckBox.IsChecked = true;
            ReadersCheckBox.IsChecked = true;
            LoansCheckBox.IsChecked = true;
            PublishersCheckBox.IsChecked = true;
            GenresCheckBox.IsChecked = true;
        }

        private void DeselectAllButton_Click(object sender, RoutedEventArgs e)
        {
            BooksCheckBox.IsChecked = false;
            AuthorsCheckBox.IsChecked = false;
            ReadersCheckBox.IsChecked = false;
            LoansCheckBox.IsChecked = false;
            PublishersCheckBox.IsChecked = false;
            GenresCheckBox.IsChecked = false;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog()
            {
                Title = "Выберите папку для сохранения экспортированных файлов",
                FileName = "Выберите папку", // Placeholder filename
                DefaultExt = "",
                Filter = ""
            };

            // Устанавливаем начальную директорию
            dialog.InitialDirectory = _outputPath;

            if (dialog.ShowDialog() == true)
            {
                // Получаем папку из выбранного файла
                _outputPath = System.IO.Path.GetDirectoryName(dialog.FileName) ?? _outputPath;
                OutputPathTextBox.Text = _outputPath;
                UpdatePreview(null, null);
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ExportButton.IsEnabled = false;
                ProgressPanel.Visibility = Visibility.Visible;
                StatusTextBlock.Text = "Начинаем экспорт...";

                var format = GetExportFormat();
                var extension = GetFileExtension(((ComboBoxItem)FormatComboBox.SelectedItem)?.Content?.ToString() ?? "JSON");
                
                var exportTasks = new List<Task<(bool Success, string Message)>>();
                var taskNames = new List<string>();

                // Подготавливаем задачи экспорта
                if (BooksCheckBox.IsChecked == true)
                {
                    var fileName = CreateSubfoldersCheckBox.IsChecked == true 
                        ? Path.Combine(_outputPath, "Книги", $"books{extension}")
                        : Path.Combine(_outputPath, $"books{extension}");
                    Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                    exportTasks.Add(_importExportService.ExportBooksAsync(fileName, format));
                    taskNames.Add("Книги");
                }

                if (AuthorsCheckBox.IsChecked == true)
                {
                    var fileName = CreateSubfoldersCheckBox.IsChecked == true 
                        ? Path.Combine(_outputPath, "Авторы", $"authors{extension}")
                        : Path.Combine(_outputPath, $"authors{extension}");
                    Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                    exportTasks.Add(_importExportService.ExportAuthorsAsync(fileName, format));
                    taskNames.Add("Авторы");
                }

                if (ReadersCheckBox.IsChecked == true)
                {
                    var fileName = CreateSubfoldersCheckBox.IsChecked == true 
                        ? Path.Combine(_outputPath, "Читатели", $"readers{extension}")
                        : Path.Combine(_outputPath, $"readers{extension}");
                    Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                    exportTasks.Add(_importExportService.ExportReadersAsync(fileName, format));
                    taskNames.Add("Читатели");
                }

                if (LoansCheckBox.IsChecked == true)
                {
                    var fileName = CreateSubfoldersCheckBox.IsChecked == true 
                        ? Path.Combine(_outputPath, "Выдачи", $"loans{extension}")
                        : Path.Combine(_outputPath, $"loans{extension}");
                    Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                    exportTasks.Add(_importExportService.ExportLoansAsync(fileName, format));
                    taskNames.Add("Выдачи");
                }

                if (PublishersCheckBox.IsChecked == true)
                {
                    var fileName = CreateSubfoldersCheckBox.IsChecked == true 
                        ? Path.Combine(_outputPath, "Издательства", $"publishers{extension}")
                        : Path.Combine(_outputPath, $"publishers{extension}");
                    Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                    exportTasks.Add(_importExportService.ExportPublishersAsync(fileName, format));
                    taskNames.Add("Издательства");
                }

                if (GenresCheckBox.IsChecked == true)
                {
                    var fileName = CreateSubfoldersCheckBox.IsChecked == true 
                        ? Path.Combine(_outputPath, "Жанры", $"genres{extension}")
                        : Path.Combine(_outputPath, $"genres{extension}");
                    Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                    exportTasks.Add(_importExportService.ExportGenresAsync(fileName, format));
                    taskNames.Add("Жанры");
                }

                // Выполняем экспорт с отображением прогресса
                var successCount = 0;
                var totalTasks = exportTasks.Count;

                for (int i = 0; i < exportTasks.Count; i++)
                {
                    StatusTextBlock.Text = $"Экспортируем {taskNames[i]}...";
                    ProgressTextBlock.Text = $"Экспорт {taskNames[i]} ({i + 1} из {totalTasks})";
                    
                    var result = await exportTasks[i];
                    
                    if (result.Success)
                        successCount++;

                    var progress = ((double)(i + 1) / totalTasks) * 100;
                    ExportProgressBar.Value = progress;
                    ProgressPercentTextBlock.Text = $"{progress:F0}%";

                    // Небольшая задержка для визуального эффекта
                    await Task.Delay(200);
                }

                // Показываем результат
                StatusTextBlock.Text = $"Экспорт завершен: {successCount}/{totalTasks} успешно";
                
                if (successCount == totalTasks)
                {
                    MessageBox.Show($"Экспорт успешно завершен!\nВсе {successCount} типов данных экспортированы.",
                                  "Экспорт завершен", MessageBoxButton.OK, MessageBoxImage.Information);

                    if (OpenAfterExportCheckBox.IsChecked == true)
                    {
                        System.Diagnostics.Process.Start("explorer.exe", _outputPath);
                    }
                }
                else
                {
                    MessageBox.Show($"Экспорт завершен с ошибками.\nУспешно: {successCount}/{totalTasks}",
                                  "Экспорт завершен", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}", 
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Ошибка экспорта";
            }
            finally
            {
                ExportButton.IsEnabled = true;
                ProgressPanel.Visibility = Visibility.Collapsed;
                ExportProgressBar.Value = 0;
            }
        }

        private ExportFormat GetExportFormat()
        {
            var formatText = ((ComboBoxItem)FormatComboBox.SelectedItem)?.Content?.ToString() ?? "JSON";
            return formatText.ToUpper() switch
            {
                "JSON" => ExportFormat.JSON,
                "XML" => ExportFormat.XML,
                "CSV" => ExportFormat.CSV,
                "TXT" => ExportFormat.TXT,
                _ => ExportFormat.JSON
            };
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
} 