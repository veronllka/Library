using System.Configuration;
using System.Data;
using System.Windows;
using Library.Services;
using Library.Views;
using System.Threading.Tasks;

namespace Library
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Проверяем подключение к базе данных при запуске
            var (isConnected, errorMessage) = await DatabaseService.TestDatabaseConnectionAsync();
            
            if (isConnected)
            {
                MessageBox.Show("✅ Программа успешно подключена к базе данных!", 
                              "Статус подключения", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"❌ Не удалось подключиться к базе данных!\n\nОшибка: {errorMessage}\n\nПроверьте:\n• Работает ли SQL Server Express\n• Правильность имени сервера\n• Существование базы данных Library\n• Настройки брандмауэра", 
                              "Ошибка подключения", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }

            // Запускаем главное окно входа
            var loginWindow = new LoginWindow();
            loginWindow.Show();
        }
    }
}
