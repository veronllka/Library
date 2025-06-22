using System;
using System.Windows;
using Library.Services;

namespace Library.Views
{
    public partial class TestAdministrationWindow : Window
    {
        public TestAdministrationWindow()
        {
            try
            {
                InitializeComponent();
                ServerInfoText.Text = "Окно успешно инициализировано";
                ConnectionStatusText.Text = "Ожидание команд пользователя";
                StatusText.Text = "Готов к работе";
            }
            catch (Exception ex)
            {
                if (StatusText != null)
                    StatusText.Text = $"Ошибка: {ex.Message}";
            }
        }

        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Тестирование подключения...";
                TestResultText.Text = "Проверка подключения к SQL Server...";
                
                var (success, message) = await DatabaseService.TestDatabaseConnectionAsync();
                
                if (success)
                {
                    ConnectionStatusText.Text = "✅ Подключение успешно";
                    TestResultText.Text = "Подключение к SQL Server установлено успешно!";
                    StatusText.Text = "Подключение установлено";
                }
                else
                {
                    ConnectionStatusText.Text = "❌ Ошибка подключения";
                    TestResultText.Text = $"Ошибка подключения:\n{message}\n\nВозможные причины:\n• SQL Server не запущен\n• Неверные параметры подключения\n• Проблемы с сетью";
                    StatusText.Text = "Ошибка подключения";
                }
            }
            catch (Exception ex)
            {
                ConnectionStatusText.Text = "❌ Критическая ошибка";
                TestResultText.Text = $"Критическая ошибка:\n{ex.Message}";
                StatusText.Text = "Критическая ошибка";
            }
        }
    }
} 