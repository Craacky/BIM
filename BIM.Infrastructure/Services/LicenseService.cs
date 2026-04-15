using System;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using BIM.Application.Common.Interfaces;

namespace BIM.Infrastructure.Services
{
    public class LicenseService : ILicenseService
    {
        private const string MasterSecret = "BIM_PROJECT_MASTER_SECRET_KEY_2026_SECURE_V1";
        private const string AbsoluteLicenseKey = "ABSOLUTE_LICENSE_PERMANENT_ACCESS"; // Special key for permanent access

        public bool ValidateLicense(string token, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(token))
            {
                errorMessage = "Лицензионный ключ не найден.";
                return false;
            }

            // Check for absolute/permanent license key
            if (token == AbsoluteLicenseKey)
            {
                return true; // Absolute license is always valid
            }

            string[] parts = token.Split('.');
            if (parts.Length != 3)
            {
                errorMessage = "Неверный формат ключа.";
                return false;
            }

            string header = parts[0];
            string payload = parts[1];
            string signature = parts[2];

            // 1. Verify Signature
            string computedSignature = ComputeSignature(header, payload, MasterSecret);
            if (signature != computedSignature)
            {
                errorMessage = "Недействительная подпись ключа.";
                return false;
            }

            // 2. Parse Payload
            try
            {
                string jsonPayload = DecodeBase64Url(payload);
                var claims = JsonSerializer.Deserialize<LicenseClaims>(jsonPayload);

                if (claims == null)
                {
                    errorMessage = "Не удалось прочитать данные лицензии.";
                    return false;
                }

                long currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                if (currentUnixTime < claims.nbf)
                {
                    errorMessage = "Срок действия лицензии еще не наступил.";
                    return false;
                }

                if (currentUnixTime > claims.exp)
                {
                    errorMessage = "Срок действия лицензии истек.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Ошибка проверки лицензии: {ex.Message}";
                return false;
            }
        }

        private string ComputeSignature(string header, string payload, string secret)
        {
            string data = $"{header}.{payload}";
            var secretBytes = Encoding.UTF8.GetBytes(secret);
            var dataBytes = Encoding.UTF8.GetBytes(data);

            using (var hmac = new HMACSHA256(secretBytes))
            {
                var hashBytes = hmac.ComputeHash(dataBytes);
                return EncodeBase64Url(hashBytes);
            }
        }

        private string EncodeBase64Url(byte[] data)
        {
            string base64 = Convert.ToBase64String(data);
            return base64.Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        private string DecodeBase64Url(string input)
        {
            string base64 = input.Replace("-", "+").Replace("_", "/");
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            byte[] bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }

        private class LicenseClaims
        {
            public long nbf { get; set; } // Not Before
            public long exp { get; set; } // Expiry
        }
    }
}
