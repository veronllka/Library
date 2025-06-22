using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Library.Services;

namespace Library.Views
{
    public partial class ImportWindow : Window
    {
        private readonly ImportExportService _importExportService;
        private string _selectedFilePath;
        private bool _isFileValid;

        public ImportWindow()
        {
            InitializeComponent();
            _importExportService = new ImportExportService();
            UpdateStatus("Выберите файл для импорта");
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        #region Drag and Drop

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    LoadFile(files[0]);
                }
            }
        }

        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                DropZone.Background = new SolidColorBrush(Color.FromArgb(80, 76, 175, 80)); // Зеленоватый
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void DropZone_DragLeave(object sender, DragEventArgs e)
        {
            DropZone.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)); // Обратно к прозрачному
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            DropZone.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
            
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    LoadFile(files[0]);
                }
            }
        }

        #endregion

        private void BrowseFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog()
            {
                Title = "Выберите файл для импорта",
                Filter = "Все поддерживаемые|*.json;*.xml;*.csv|" +
                        "JSON файлы|*.json|" +
                        "XML файлы|*.xml|" +
                        "CSV файлы|*.csv|" +
                        "Все файлы|*.*",
                FilterIndex = 1
            };

            if (dialog.ShowDialog() == true)
            {
                LoadFile(dialog.FileName);
            }
        }

        private async void LoadFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    MessageBox.Show("Файл не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _selectedFilePath = filePath;
                FilePathTextBox.Text = filePath;

                // Определяем формат файла по расширению
                var extension = Path.GetExtension(filePath).ToLower();
                switch (extension)
                {
                    case ".json":
                        FormatComboBox.SelectedIndex = 0;
                        break;
                    case ".xml":
                        FormatComboBox.SelectedIndex = 1;
                        break;
                    case ".csv":
                        FormatComboBox.SelectedIndex = 2;
                        break;
                    default:
                        FormatComboBox.SelectedIndex = 0; // По умолчанию JSON
                        break;
                }

                // Показываем информацию о файле
                var fileInfo = new FileInfo(filePath);
                FileInfoTextBlock.Text = $"Имя: {fileInfo.Name}\n" +
                                        $"Размер: {fileInfo.Length / 1024.0:F1} КБ\n" +
                                        $"Изменен: {fileInfo.LastWriteTime:dd.MM.yyyy HH:mm}";

                ValidateFileButton.IsEnabled = true;
                await ValidateFile();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке файла: {ex.Message}", 
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("Ошибка загрузки файла");
            }
        }

        private async void ValidateFileButton_Click(object sender, RoutedEventArgs e)
        {
            await ValidateFile();
        }

        private async Task ValidateFile()
        {
            if (string.IsNullOrEmpty(_selectedFilePath))
                return;

            try
            {
                ValidateFileButton.IsEnabled = false;
                UpdateStatus("Проверяем файл...");

                var content = await File.ReadAllTextAsync(_selectedFilePath);
                
                // Показываем превью содержимого (первые 1000 символов)
                var preview = content.Length > 1000 ? content.Substring(0, 1000) + "..." : content;
                PreviewTextBlock.Text = preview;

                // Простая валидация формата
                var format = ((ComboBoxItem)FormatComboBox.SelectedItem)?.Content?.ToString();
                
                bool isValid = format switch
                {
                    "JSON" => ValidateJson(content),
                    "XML" => ValidateXml(content),
                    "CSV" => ValidateCsv(content),
                    _ => false
                };

                if (isValid)
                {
                    _isFileValid = true;
                    ImportButton.IsEnabled = true;
                    UpdateStatus("Файл готов к импорту");
                    
                    // Подсчитываем количество записей для превью
                    var recordCount = EstimateRecordCount(content, format);
                    TotalCountTextBlock.Text = recordCount.ToString();
                }
                else
                {
                    _isFileValid = false;
                    ImportButton.IsEnabled = false;
                    UpdateStatus("Ошибка формата файла");
                    MessageBox.Show($"Файл имеет неверный формат {format}", 
                                  "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _isFileValid = false;
                ImportButton.IsEnabled = false;
                UpdateStatus("Ошибка проверки файла");
                MessageBox.Show($"Ошибка при проверке файла: {ex.Message}", 
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ValidateFileButton.IsEnabled = true;
            }
        }

        private bool ValidateJson(string content)
        {
            try
            {
                System.Text.Json.JsonDocument.Parse(content);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ValidateXml(string content)
        {
            try
            {
                System.Xml.Linq.XDocument.Parse(content);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ValidateCsv(string content)
        {
            try
            {
                var lines = content.Split('\n');
                return lines.Length > 1; // Минимум заголовок + одна строка данных
            }
            catch
            {
                return false;
            }
        }

        private int EstimateRecordCount(string content, string format)
        {
            try
            {
                return format switch
                {
                    "JSON" => EstimateJsonRecords(content),
                    "XML" => EstimateXmlRecords(content),
                    "CSV" => EstimateCsvRecords(content),
                    _ => 0
                };
            }
            catch
            {
                return 0;
            }
        }

        private int EstimateJsonRecords(string content)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                return doc.RootElement.GetArrayLength();
            }
            return 1;
        }

        private int EstimateXmlRecords(string content)
        {
            var doc = System.Xml.Linq.XDocument.Parse(content);
            return doc.Root?.Elements().Count() ?? 0;
        }

        private int EstimateCsvRecords(string content)
        {
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return Math.Max(0, lines.Length - 1); // Исключаем заголовок
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isFileValid || string.IsNullOrEmpty(_selectedFilePath))
            {
                MessageBox.Show("Сначала выберите и проверьте файл", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Показываем прогресс
                ImportButton.IsEnabled = false;
                ProgressPanel.Visibility = Visibility.Visible;
                ResultsPanel.Visibility = Visibility.Visible;
                
                UpdateStatus("Начинаем импорт...");
                ImportProgressBar.Value = 25;
                ProgressPercentTextBlock.Text = "25%";

                var format = GetImportFormat();
                var dataType = ((ComboBoxItem)DataTypeComboBox.SelectedItem)?.Content?.ToString();

                // В данной версии поддерживаем только импорт книг
                if (dataType != "Книги")
                {
                    MessageBox.Show("В данной версии поддерживается только импорт книг", 
                                  "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                ProgressTextBlock.Text = "Импортируем книги...";
                ImportProgressBar.Value = 50;
                ProgressPercentTextBlock.Text = "50%";

                // Выполняем импорт
                var result = await _importExportService.ImportBooksAsync(_selectedFilePath, format);

                ImportProgressBar.Value = 100;
                ProgressPercentTextBlock.Text = "100%";

                // Показываем результаты
                if (result.Success)
                {
                    SuccessCountTextBlock.Text = result.ImportedCount.ToString();
                    ErrorCountTextBlock.Text = "0";
                    TotalCountTextBlock.Text = result.ImportedCount.ToString();
                    
                    UpdateStatus($"Импорт завершен успешно: {result.ImportedCount} записей");
                    
                    MessageBox.Show($"Импорт успешно завершен!\n{result.Message}", 
                                  "Импорт завершен", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    SuccessCountTextBlock.Text = result.ImportedCount.ToString();
                    ErrorCountTextBlock.Text = "1";
                    TotalCountTextBlock.Text = result.ImportedCount.ToString();
                    
                    UpdateStatus("Ошибка импорта");
                    
                    MessageBox.Show($"Ошибка импорта:\n{result.Message}", 
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("Ошибка импорта");
                MessageBox.Show($"Ошибка при импорте: {ex.Message}", 
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ImportButton.IsEnabled = true;
                ProgressPanel.Visibility = Visibility.Collapsed;
                ImportProgressBar.Value = 0;
            }
        }

        private ImportFormat GetImportFormat()
        {
            var formatText = ((ComboBoxItem)FormatComboBox.SelectedItem)?.Content?.ToString() ?? "JSON";
            return formatText.ToUpper() switch
            {
                "JSON" => ImportFormat.JSON,
                "XML" => ImportFormat.XML,
                "CSV" => ImportFormat.CSV,
                _ => ImportFormat.JSON
            };
        }

        private void UpdateStatus(string message)
        {
            StatusTextBlock.Text = message;
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedFilePath = string.Empty;
            _isFileValid = false;
            
            FilePathTextBox.Text = string.Empty;
            FileInfoTextBlock.Text = "Файл не выбран";
            PreviewTextBlock.Text = "Выберите файл для предварительного просмотра";
            
            ValidateFileButton.IsEnabled = false;
            ImportButton.IsEnabled = false;
            
            ResultsPanel.Visibility = Visibility.Collapsed;
            
            SuccessCountTextBlock.Text = "0";
            ErrorCountTextBlock.Text = "0";
            TotalCountTextBlock.Text = "0";
            
            UpdateStatus("Готов к импорту");
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