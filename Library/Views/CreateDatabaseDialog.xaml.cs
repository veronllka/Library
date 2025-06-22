using System;
using System.Windows;

namespace Library.Views
{
    public partial class CreateDatabaseDialog : Window
    {
        public string DatabaseName { get; private set; }
        public int InitialSize { get; private set; }
        public int GrowthSize { get; private set; }
        public bool CreateSampleData { get; private set; }

        public CreateDatabaseDialog()
        {
            InitializeComponent();
            DatabaseNameTextBox.Focus();
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            var dbName = DatabaseNameTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(dbName))
            {
                MessageBox.Show("Введите название базы данных.", "Внимание", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                DatabaseNameTextBox.Focus();
                return;
            }

            if (dbName.Length > 128)
            {
                MessageBox.Show("Название базы данных не может быть длиннее 128 символов.", "Внимание", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                DatabaseNameTextBox.Focus();
                return;
            }

            // Проверка на недопустимые символы
            if (dbName.Contains(" ") || dbName.Contains("\\") || dbName.Contains("/") || 
                dbName.Contains("?") || dbName.Contains(":") || dbName.Contains("*") || 
                dbName.Contains("\"") || dbName.Contains("<") || dbName.Contains(">") || 
                dbName.Contains("|"))
            {
                MessageBox.Show("Название базы данных содержит недопустимые символы.", "Внимание", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                DatabaseNameTextBox.Focus();
                return;
            }

            DatabaseName = dbName;
            InitialSize = (int)(InitialSizeNumeric.Value ?? 100);
            GrowthSize = (int)(GrowthSizeNumeric.Value ?? 10);
            CreateSampleData = CreateSampleDataCheckBox.IsChecked ?? false;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
} 