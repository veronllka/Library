using System;
using System.Windows;

namespace Library.Views
{
    public partial class CreateLoginDialog : Window
    {
        public string LoginName { get; private set; }
        public string Password { get; private set; }
        public string DefaultDatabase { get; private set; }
        public bool IsServerAdmin { get; private set; }

        public CreateLoginDialog()
        {
            InitializeComponent();
            LoginNameTextBox.Focus();
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            var loginName = LoginNameTextBox.Text.Trim();
            var password = PasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;
            var defaultDb = DefaultDatabaseComboBox.Text.Trim();

            // Валидация имени логина
            if (string.IsNullOrEmpty(loginName))
            {
                MessageBox.Show("Введите имя логина.", "Внимание", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                LoginNameTextBox.Focus();
                return;
            }

            if (loginName.Length > 128)
            {
                MessageBox.Show("Имя логина не может быть длиннее 128 символов.", "Внимание", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                LoginNameTextBox.Focus();
                return;
            }

            // Проверка на недопустимые символы в имени логина
            if (loginName.Contains("\\") || loginName.Contains("/") || 
                loginName.Contains("?") || loginName.Contains(":") || loginName.Contains("*") || 
                loginName.Contains("\"") || loginName.Contains("<") || loginName.Contains(">") || 
                loginName.Contains("|") || loginName.Contains("[") || loginName.Contains("]"))
            {
                MessageBox.Show("Имя логина содержит недопустимые символы.", "Внимание", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                LoginNameTextBox.Focus();
                return;
            }

            // Валидация пароля
            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Введите пароль.", "Внимание", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Focus();
                return;
            }

            if (password.Length < 6)
            {
                MessageBox.Show("Пароль должен содержать минимум 6 символов.", "Внимание", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Focus();
                return;
            }

            if (password != confirmPassword)
            {
                MessageBox.Show("Пароли не совпадают.", "Внимание", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                ConfirmPasswordBox.Focus();
                return;
            }

            // Валидация базы данных по умолчанию
            if (string.IsNullOrEmpty(defaultDb))
            {
                MessageBox.Show("Укажите базу данных по умолчанию.", "Внимание", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                DefaultDatabaseComboBox.Focus();
                return;
            }

            // Подтверждение для администратора сервера
            var isAdmin = IsServerAdminCheckBox.IsChecked ?? false;
            if (isAdmin)
            {
                var result = MessageBox.Show(
                    $"Вы создаёте логин '{loginName}' с правами администратора сервера.\n" +
                    "Это даёт полный контроль над SQL Server. Продолжить?", 
                    "Подтверждение", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            LoginName = loginName;
            Password = password;
            DefaultDatabase = defaultDb;
            IsServerAdmin = isAdmin;

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