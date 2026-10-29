using System.Security.Cryptography;
using System.Text;

namespace SelfStorageSystem.Infrastructure.Services;

/// <summary>
/// Provides reversible symmetric encryption (AES-256) for hardware door keypad PINs
/// stored in AccessCredential.DisplayHint.
/// Protects keypad PINs from database leaks while enabling retrieval for customers
/// at physical keypad locks, with transparent backward compatibility for legacy plaintext records.
/// </summary>
public static class PinProtectionHelper
{
    private const string EncryptedPrefix = "ENC:";
    
    // Key derived from environment variable or cluster fallback key
    private static readonly byte[] Key = SHA256.HashData(Encoding.UTF8.GetBytes(
        Environment.GetEnvironmentVariable("PIN_PROTECTION_KEY") ?? "SelfStorageSystem.KeypadPin.ProtectionSecretKey.v1.2026!"));

    /// <summary>
    /// Encrypts a plaintext PIN into a Base64-encoded AES-256-CBC string prefixed with "ENC:".
    /// </summary>
    public static string Encrypt(string? pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            return string.Empty;
        }

        var trimmed = pin.Trim();
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var plainBytes = Encoding.UTF8.GetBytes(trimmed);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Payload format: [16 bytes IV] + [cipherBytes]
        var payload = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, payload, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, aes.IV.Length, cipherBytes.Length);

        return $"{EncryptedPrefix}{Convert.ToBase64String(payload)}";
    }

    /// <summary>
    /// Decrypts a stored PIN hint. If the value is a legacy unencrypted PIN, returns it as-is.
    /// </summary>
    public static string Decrypt(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return string.Empty;
        }

        var trimmed = storedValue.Trim();

        // Backward compatibility: If not starting with ENC:, return legacy plaintext value
        if (!trimmed.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
        {
            return trimmed;
        }

        try
        {
            var base64Part = trimmed.Substring(EncryptedPrefix.Length);
            var payload = Convert.FromBase64String(base64Part);

            if (payload.Length < 17) // 16 bytes IV + at least 1 block of cipher
            {
                return trimmed;
            }

            var iv = new byte[16];
            Buffer.BlockCopy(payload, 0, iv, 0, 16);

            var cipherLength = payload.Length - 16;
            var cipherBytes = new byte[cipherLength];
            Buffer.BlockCopy(payload, 16, cipherBytes, 0, cipherLength);

            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // Fallback gracefully to raw string if decryption fails
            return trimmed;
        }
    }
}
