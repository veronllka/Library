using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Library.Views
{
    public partial class LogoutDialog : Window
    {
        public bool ShouldLogout { get; private set; } = false;

        public LogoutDialog()
        {
            InitializeComponent();
            
            // Поддержка клавиш
            this.KeyDown += LogoutDialog_KeyDown;
            
            // Фокус на кнопку отмены по умолчанию
            this.Loaded += (s, e) => CancelButton.Focus();
        }

        private void LogoutDialog_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    CancelButton_Click(sender, e);
                    break;
                case Key.Enter:
                    LogoutButton_Click(sender, e);
                    break;
            }
        }

        // Обработчик для перетаскивания окна
        private void Rectangle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            ShouldLogout = true;
            await PlayExitAnimation();
            DialogResult = true;
            Close();
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ShouldLogout = false;
            await PlayExitAnimation();
            DialogResult = false;
            Close();
        }

        private System.Threading.Tasks.Task PlayExitAnimation()
        {
            var storyboard = (Storyboard)FindResource("DialogExitAnimation");
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            
            storyboard.Completed += (s, e) => tcs.SetResult(true);
            storyboard.Begin(this);
            
            return tcs.Task;
        }
    }
} 