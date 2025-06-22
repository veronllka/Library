using System;
using System.Security.Cryptography;
using System.Text;

namespace Library.Services
{
    public class PasswordHasher
    {
        // Количество итераций для PBKDF2
        private const int Iterations = 10000;
        
        // Размер соли в байтах
        private const int SaltSize = 16;
        
        // Размер хеша в байтах
        private const int HashSize = 32;
        
        /// <summary>
        /// Хеширует пароль с использованием PBKDF2
        /// </summary>
        /// <param name="password">Пароль для хеширования</param>
        /// <returns>Строка в формате Base64, содержащая соль и хеш</returns>
        public static string HashPassword(string password)
        {
            // Создаем случайную соль
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            
            // Создаем хеш с использованием PBKDF2
            byte[] hash = GetPbkdf2Bytes(password, salt, Iterations, HashSize);
            
            // Комбинируем соль и хеш
            byte[] hashBytes = new byte[SaltSize + HashSize];
            Array.Copy(salt, 0, hashBytes, 0, SaltSize);
            Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);
            
            // Конвертируем в Base64 для хранения
            return Convert.ToBase64String(hashBytes);
        }
        
        /// <summary>
        /// Проверяет соответствие пароля хешу
        /// </summary>
        /// <param name="password">Пароль для проверки</param>
        /// <param name="hashedPassword">Хешированный пароль из базы данных</param>
        /// <returns>True, если пароль соответствует хешу</returns>
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            // Конвертируем хеш из Base64
            byte[] hashBytes = Convert.FromBase64String(hashedPassword);
            
            // Извлекаем соль (первые SaltSize байт)
            byte[] salt = new byte[SaltSize];
            Array.Copy(hashBytes, 0, salt, 0, SaltSize);
            
            // Извлекаем сохраненный хеш (оставшиеся байты)
            byte[] storedHash = new byte[HashSize];
            Array.Copy(hashBytes, SaltSize, storedHash, 0, HashSize);
            
            // Вычисляем хеш введенного пароля с той же солью
            byte[] computedHash = GetPbkdf2Bytes(password, salt, Iterations, HashSize);
            
            // Сравниваем хеши с постоянным временем выполнения для защиты от timing-атак
            return SlowEquals(storedHash, computedHash);
        }
        
        /// <summary>
        /// Сравнивает два массива байт с постоянным временем выполнения
        /// </summary>
        private static bool SlowEquals(byte[] a, byte[] b)
        {
            uint diff = (uint)a.Length ^ (uint)b.Length;
            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                diff |= (uint)(a[i] ^ b[i]);
            }
            return diff == 0;
        }
        
        /// <summary>
        /// Вычисляет PBKDF2 хеш для пароля
        /// </summary>
        private static byte[] GetPbkdf2Bytes(string password, byte[] salt, int iterations, int outputBytes)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(outputBytes);
            }
        }
    }
} 