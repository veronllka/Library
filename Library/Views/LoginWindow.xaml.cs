using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Library.Services;
using Library.Models;
using MahApps.Metro.IconPacks;

namespace Library.Views
{
    public partial class LoginWindow : Window
    {
        private readonly DatabaseService _databaseService;
        private readonly CaptchaService _captchaService;
        private bool _isPasswordVisible = false;
        private int _failedLoginAttempts = 0;
        private const int CAPTCHA_THRESHOLD = 3;

        public LoginWindow()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            _captchaService = new CaptchaService();
            
            // Добавляем возможность перетаскивания окна
            this.MouseDown += (sender, e) => { if (e.LeftButton == MouseButtonState.Pressed) this.DragMove(); };
            
            // Обработка Enter для входа
            this.KeyDown += LoginWindow_KeyDown;
            
            // Установим фокус на поле логина и инициализируем плейсхолдеры
            this.Loaded += (s, e) => {
                LoginTextBox.Focus();
                // Показываем плейсхолдеры в начале
                LoginPlaceholder.Visibility = Visibility.Visible;
                PasswordPlaceholder.Visibility = Visibility.Visible;
            };
            
            // Обработчики для placeholder текста
            LoginTextBox.TextChanged += LoginTextBox_TextChanged;
            LoginTextBox.GotFocus += (s, e) => LoginPlaceholder.Visibility = Visibility.Collapsed;
            LoginTextBox.LostFocus += (s, e) => {
                if (string.IsNullOrEmpty(LoginTextBox.Text))
                    LoginPlaceholder.Visibility = Visibility.Visible;
            };
            
            PasswordBox.PasswordChanged += PasswordBox_PasswordChanged;
            PasswordBox.GotFocus += (s, e) => {
                if (!_isPasswordVisible)
                    PasswordPlaceholder.Visibility = Visibility.Collapsed;
            };
            PasswordBox.LostFocus += (s, e) => {
                if (!_isPasswordVisible && string.IsNullOrEmpty(PasswordBox.Password))
                    PasswordPlaceholder.Visibility = Visibility.Visible;
            };
            
            PasswordTextBox.TextChanged += PasswordTextBox_TextChanged;
            PasswordTextBox.GotFocus += (s, e) => {
                if (_isPasswordVisible)
                    PasswordPlaceholder.Visibility = Visibility.Collapsed;
            };
            PasswordTextBox.LostFocus += (s, e) => {
                if (_isPasswordVisible && string.IsNullOrEmpty(PasswordTextBox.Text))
                    PasswordPlaceholder.Visibility = Visibility.Visible;
            };
            
            // Обработчики для капчи
            CaptchaTextBox.TextChanged += CaptchaTextBox_TextChanged;
            CaptchaTextBox.GotFocus += (s, e) => CaptchaPlaceholder.Visibility = Visibility.Collapsed;
            CaptchaTextBox.LostFocus += (s, e) => {
                if (string.IsNullOrEmpty(CaptchaTextBox.Text))
                    CaptchaPlaceholder.Visibility = Visibility.Visible;
            };
        }

        private void LoginWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoginButton_Click(sender, e);
            }
        }

        private void ShowPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;

            if (_isPasswordVisible)
            {
                // Показать пароль как текст
                PasswordTextBox.Text = PasswordBox.Password;
                PasswordBox.Visibility = Visibility.Collapsed;
                PasswordTextBox.Visibility = Visibility.Visible;
                PasswordTextBox.Focus();
                PasswordTextBox.CaretIndex = PasswordTextBox.Text.Length;
                
                // Изменить иконку на "скрыть"
                EyeIcon.Kind = PackIconMaterialKind.EyeOff;
                
                // Обновить placeholder
                PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                // Скрыть пароль
                PasswordBox.Password = PasswordTextBox.Text;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                PasswordBox.Visibility = Visibility.Visible;
                PasswordBox.Focus();
                
                // Изменить иконку на "показать"
                EyeIcon.Kind = PackIconMaterialKind.Eye;
                
                // Обновить placeholder
                PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string login = LoginTextBox.Text.Trim();
                string password = _isPasswordVisible ? PasswordTextBox.Text : PasswordBox.Password;

                if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Пожалуйста, введите логин и пароль.", "Внимание", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка капчи если необходимо
                if (_failedLoginAttempts >= CAPTCHA_THRESHOLD)
                {
                    string captchaInput = CaptchaTextBox.Text.Trim();
                    if (string.IsNullOrEmpty(captchaInput))
                    {
                        MessageBox.Show("Пожалуйста, введите код с картинки.", "Внимание", 
                                      MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (!_captchaService.ValidateCaptcha(captchaInput))
                    {
                        MessageBox.Show("Неверный код с картинки. Попробуйте еще раз.", "Ошибка", 
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                        RefreshCaptcha();
                        CaptchaTextBox.Text = "";
                        CaptchaTextBox.Focus();
                        return;
                    }
                }

                // Отключаем кнопку во время проверки
                LoginButton.IsEnabled = false;
                LoginButton.Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children = {
                        new MahApps.Metro.IconPacks.PackIconMaterial { Kind = PackIconMaterialKind.Loading, Width = 20, Height = 20, Margin = new Thickness(0,0,10,0) },
                        new TextBlock { Text = "Проверка..." }
                    }
                };

                try
                {
                    User? user = DatabaseService.ValidateUser(login, password);

                    if (user != null)
                    {
                        // Успешный вход - сбрасываем состояние формы
                        ResetLoginForm();
                        
                        MainWindow mainWindow = new MainWindow(user);
                        mainWindow.Show();
                        this.Close();
                    }
                    else
                    {
                        // Неудачная попытка входа
                        _failedLoginAttempts++;
                        
                        MessageBox.Show($"Неверный логин или пароль. Попытка {_failedLoginAttempts}.", "Ошибка", 
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                        
                        // Показать капчу после 3 неудачных попыток
                        if (_failedLoginAttempts >= CAPTCHA_THRESHOLD)
                        {
                            ShowCaptcha();
                        }
                        
                        // Очистить поля при неудачной попытке
                        if (_isPasswordVisible)
                        {
                            PasswordTextBox.Text = "";
                            PasswordTextBox.Focus();
                        }
                        else
                        {
                            PasswordBox.Password = "";
                            PasswordBox.Focus();
                        }
                        
                        // Очистить капчу если она видна
                        if (CaptchaPanel.Visibility == Visibility.Visible)
                        {
                            CaptchaTextBox.Text = "";
                            RefreshCaptcha();
                        }
                    }
                }
                catch (Exception dbEx)
                {
                    MessageBox.Show($"Ошибка при проверке учетных данных:\n{dbEx.Message}", 
                                  "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения к базе данных:\n{ex.Message}", 
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Возвращаем кнопку в исходное состояние
                LoginButton.IsEnabled = true;
                LoginButton.Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children = {
                        new MahApps.Metro.IconPacks.PackIconMaterial { Kind = PackIconMaterialKind.Login, Width = 20, Height = 20, Margin = new Thickness(0,0,10,0) },
                        new TextBlock { Text = "Войти в систему" }
                    }
                };
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Сбрасываем капчу при закрытии
            _captchaService.ResetCaptcha();
            Application.Current.Shutdown();
        }

        private void LoginTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoginPlaceholder.Visibility = string.IsNullOrEmpty(LoginTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_isPasswordVisible)
            {
                PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isPasswordVisible)
            {
                PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void RefreshCaptchaButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshCaptcha();
        }

        private void ShowCaptcha()
        {
            CaptchaPanel.Visibility = Visibility.Visible;
            RefreshCaptcha();
            CaptchaTextBox.Text = "";
            CaptchaPlaceholder.Visibility = Visibility.Visible;
        }

        private void RefreshCaptcha()
        {
            try
            {
                string captchaText = _captchaService.GenerateNewCaptcha();
                var captchaImage = _captchaService.GenerateCaptchaImage(captchaText);
                CaptchaImage.Source = captchaImage;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при генерации капчи: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CaptchaTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CaptchaPlaceholder.Visibility = string.IsNullOrEmpty(CaptchaTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HideCaptcha()
        {
            CaptchaPanel.Visibility = Visibility.Collapsed;
            CaptchaTextBox.Text = "";
            CaptchaPlaceholder.Visibility = Visibility.Visible;
            _captchaService.ResetCaptcha();
        }

        private void ResetLoginForm()
        {
            _failedLoginAttempts = 0;
            HideCaptcha();
            LoginTextBox.Text = "";
            if (_isPasswordVisible)
                PasswordTextBox.Text = "";
            else
                PasswordBox.Password = "";
        }

        // Метод для демонстрации регистрации нового пользователя
        private async void RegisterNewUser(string login, string password, string fullName, int roleId)
        {
            try
            {
                var result = await DatabaseService.CreateUserAsync(fullName, login, password, roleId);
                
                if (result.Success)
                {
                    MessageBox.Show(result.Message, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(result.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при регистрации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Метод для демонстрации изменения пароля
        private async void ChangeUserPassword(int userId, string newPassword)
        {
            try
            {
                var result = await DatabaseService.ChangePasswordAsync(userId, newPassword);
                
                if (result.Success)
                {
                    MessageBox.Show(result.Message, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(result.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при изменении пароля: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 