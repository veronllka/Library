using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace Library.Services
{
    public class CaptchaService
    {
        private string _currentCaptchaText = string.Empty;
        private readonly Random _random;

        public CaptchaService()
        {
            _random = new Random();
        }

        public string GenerateNewCaptcha()
        {
            // Генерируем случайную строку из 5 символов
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            _currentCaptchaText = "";
            
            for (int i = 0; i < 5; i++)
            {
                _currentCaptchaText += chars[_random.Next(chars.Length)];
            }

            return _currentCaptchaText;
        }

        public BitmapImage GenerateCaptchaImage(string text, int width = 200, int height = 80)
        {
            using (var bitmap = new Bitmap(width, height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                // Фон
                graphics.Clear(Color.White);

                // Добавляем шум
                for (int i = 0; i < 100; i++)
                {
                    int x = _random.Next(width);
                    int y = _random.Next(height);
                    bitmap.SetPixel(x, y, Color.FromArgb(_random.Next(256), _random.Next(256), _random.Next(256)));
                }

                 for (int i = 0; i < 5; i++)
                {
                    var pen = new Pen(Color.FromArgb(_random.Next(100, 200), _random.Next(256), _random.Next(256), _random.Next(256)), 2);
                    graphics.DrawLine(pen, _random.Next(width), _random.Next(height), _random.Next(width), _random.Next(height));
                }

                 var font = new Font("Arial", 20, FontStyle.Bold);
                var brush = new SolidBrush(Color.FromArgb(_random.Next(100, 200), 0, 0));
                
                for (int i = 0; i < text.Length; i++)
                {
                    float x = 20 + i * 30;
                    float y = 20 + _random.Next(-10, 10);
                    
                    // Поворачиваем каждый символ
                    graphics.TranslateTransform(x, y);
                    graphics.RotateTransform(_random.Next(-15, 15));
                    graphics.DrawString(text[i].ToString(), font, brush, 0, 0);
                    graphics.ResetTransform();
                }

                // Добавляем дополнительные помехи поверх текста
                for (int i = 0; i < 20; i++)
                {
                    var pen = new Pen(Color.FromArgb(_random.Next(50, 150), _random.Next(256), _random.Next(256), _random.Next(256)), 1);
                    graphics.DrawEllipse(pen, _random.Next(width), _random.Next(height), _random.Next(10), _random.Next(10));
                }

                // Конвертируем в BitmapImage для WPF
                using (var memory = new MemoryStream())
                {
                    bitmap.Save(memory, ImageFormat.Png);
                    memory.Position = 0;
                    
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memory;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    
                    return bitmapImage;
                }
            }
        }

        public bool ValidateCaptcha(string userInput)
        {
            if (string.IsNullOrEmpty(userInput) || string.IsNullOrEmpty(_currentCaptchaText))
                return false;

            return string.Equals(userInput.Trim(), _currentCaptchaText, StringComparison.OrdinalIgnoreCase);
        }

        public void ResetCaptcha()
        {
            _currentCaptchaText = string.Empty;
        }
    }
} 