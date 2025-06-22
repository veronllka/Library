using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Library.Services;
using Microsoft.Win32;

namespace Library.Views
{
    public partial class DatabaseBackupWindow : Window, INotifyPropertyChanged
    {
        private readonly DatabaseService _databaseService;
        private string _backupPath = string.Empty;
        private string _restorePath = string.Empty;
        
        public event PropertyChangedEventHandler? PropertyChanged;

        public string BackupPath
        {
            get => _backupPath;
            set
            {
                if (_backupPath != value)
                {
                    _backupPath = value;
                    OnPropertyChanged(nameof(BackupPath));
                }
            }
        }

        public string RestorePath
        {
            get => _restorePath;
            set
            {
                if (_restorePath != value)
                {
                    _restorePath = value;
                    OnPropertyChanged(nameof(RestorePath));
                }
            }
        }

        public DatabaseBackupWindow()
        {
            InitializeComponent();
            DataContext = this;
            _databaseService = new DatabaseService();
            
            // Устанавливаем путь по умолчанию в фиксированной директории
            string backupDirectory = @"C:\SQLBackups";
            
            // Создаем директорию, если она не существует
            if (!Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }
                
            string defaultFileName = $"Library_Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.bak";
            BackupPath = Path.Combine(backupDirectory, defaultFileName);
            
            AddToLog("Система резервного копирования готова к работе");
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void BrowseBackupButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "SQL Server backup files (*.bak)|*.bak|All files (*.*)|*.*",
                Title = "Выберите имя файла для резервной копии",
                FileName = Path.GetFileName(BackupPath),
                InitialDirectory = @"C:\SQLBackups"
            };

            if (dialog.ShowDialog() == true)
            {
                // Используем только имя файла и сохраняем в фиксированную директорию
                string fileName = Path.GetFileName(dialog.FileName);
                BackupPath = Path.Combine(@"C:\SQLBackups", fileName);
                
                StatusText.Text = $"Выбрано имя файла для резервной копии: {BackupPath}";
            }
        }

        private void BrowseRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "SQL Server backup files (*.bak)|*.bak|All files (*.*)|*.*",
                Title = "Выберите файл резервной копии для восстановления",
                InitialDirectory = @"C:\SQLBackups"
            };

            if (dialog.ShowDialog() == true)
            {
                RestorePath = dialog.FileName;
                
                // Проверяем, существует ли выбранный файл
                if (!File.Exists(RestorePath))
                {
                    MessageBox.Show($"Файл не найден: {RestorePath}", 
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                StatusText.Text = $"Выбран файл резервной копии: {RestorePath}";
            }
        }

        private async void CreateBackupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Создание резервной копии...";
                AddToLog($"Начато создание резервной копии по адресу: {BackupPath}");
                
                var result = await _databaseService.BackupDatabaseAsync(BackupPath);
                
                if (result.Success)
                {
                    StatusText.Text = "Резервная копия успешно создана";
                    AddToLog($"Успех: {result.Message}");
                    MessageBox.Show(result.Message, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusText.Text = "Ошибка при создании резервной копии";
                    AddToLog($"Ошибка: {result.Message}");
                    MessageBox.Show(result.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка при создании резервной копии";
                AddToLog($"Исключение: {ex.Message}");
                MessageBox.Show($"Ошибка при создании резервной копии: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(RestorePath))
            {
                MessageBox.Show("Выберите файл резервной копии для восстановления",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(RestorePath))
            {
                MessageBox.Show($"Файл не найден: {RestorePath}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var result = MessageBox.Show(
                "ВНИМАНИЕ! Восстановление базы данных приведет к перезаписи всех данных и может занять некоторое время.\n\n" +
                "Все существующие данные будут заменены данными из резервной копии.\n\n" +
                "Вы действительно хотите продолжить?",
                "Подтверждение восстановления",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                StatusText.Text = "Восстановление базы данных...";
                AddToLog($"Начато восстановление из резервной копии: {RestorePath}");
                
                var restoreResult = await _databaseService.RestoreDatabaseAsync(RestorePath);
                
                if (restoreResult.Success)
                {
                    StatusText.Text = "База данных успешно восстановлена";
                    AddToLog($"Успех: {restoreResult.Message}");
                    
                    MessageBox.Show(restoreResult.Message + "\n\nПриложение будет перезапущено для применения изменений.", 
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Перезапускаем приложение после восстановления
                    System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
                    Application.Current.Shutdown();
                }
                else
                {
                    StatusText.Text = "Ошибка при восстановлении базы данных";
                    AddToLog($"Ошибка: {restoreResult.Message}");
                    MessageBox.Show(restoreResult.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка при восстановлении базы данных";
                AddToLog($"Исключение: {ex.Message}");
                MessageBox.Show($"Ошибка при восстановлении базы данных: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AddToLog(string message)
        {
            LogTextBlock.Text += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 