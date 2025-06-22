using System;
using System.Windows;

namespace Library.Views
{
    public partial class RestoreDatabaseDialog : Window
    {
        public string DatabaseName { get; private set; }
        public bool ReplaceExisting { get; private set; }

        public RestoreDatabaseDialog()
        {
            InitializeComponent();
            DatabaseNameTextBox.Focus();
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
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

            DatabaseName = dbName;
            ReplaceExisting = ReplaceExistingCheckBox.IsChecked ?? false;

            if (ReplaceExisting)
            {
                var result = MessageBox.Show(
                    $"Вы уверены, что хотите заменить существующую базу данных '{dbName}'?\n" +
                    "Все данные в ней будут безвозвратно утеряны!", 
                    "Подтверждение", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

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