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
    private static readonly object SyncLock = new();
    private static byte[]? _key;

    // Primary legacy fallback key used when records were initially encrypted
    private static readonly byte[] FallbackKeyPrimary = SHA256.HashData(
        Encoding.UTF8.GetBytes("SelfStorageSystem.KeypadPin.ProtectionSecretKey.v1.2026!"));

    // Secondary dev fallback key
    private static readonly byte[] FallbackKeySecondary = SHA256.HashData(
        Encoding.UTF8.GetBytes("SelfStorageSystem.DevKeypadPin.ProtectionKey.2026!"));

    /// <summary>
    /// Explicitly initializes the encryption key from configuration or environment.
    /// </summary>
    public static void Initialize(string? configuredKey)
    {
        if (!string.IsNullOrWhiteSpace(configuredKey) && configuredKey != "__SET_VIA_ENVIRONMENT_OR_SECRETS__")
        {
            lock (SyncLock)
            {
                _key = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey.Trim()));
            }
        }
    }

    private static byte[] GetKey()
    {
        if (_key != null) return _key;

        lock (SyncLock)
        {
            if (_key != null) return _key;

            var envKey = Environment.GetEnvironmentVariable("PIN_PROTECTION_KEY");
            if (!string.IsNullOrWhiteSpace(envKey) && envKey != "__SET_VIA_ENVIRONMENT_OR_SECRETS__")
            {
                _key = SHA256.HashData(Encoding.UTF8.GetBytes(envKey.Trim()));
                return _key;
            }

            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                      ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

            if (string.Equals(env, "Production", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "PIN protection encryption key is missing or not configured for Production environment. " +
                    "Set the PIN_PROTECTION_KEY environment variable or configure SecuritySettings:PinProtectionKey.");
            }

            // Safe development / unit test fallback key for non-production environments
            _key = FallbackKeyPrimary;
            return _key;
        }
    }

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
        aes.Key = GetKey();
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
    /// Supports multi-key fallback for backward compatibility across key rotations.
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
                return string.Empty;
            }

            var iv = new byte[16];
            Buffer.BlockCopy(payload, 0, iv, 0, 16);

            var cipherLength = payload.Length - 16;
            var cipherBytes = new byte[cipherLength];
            Buffer.BlockCopy(payload, 16, cipherBytes, 0, cipherLength);

            // Candidate keys: active key first, followed by known fallback keys
            var candidateKeys = new List<byte[]> { GetKey(), FallbackKeyPrimary, FallbackKeySecondary };

            foreach (var candidateKey in candidateKeys)
            {
                try
                {
                    using var aes = Aes.Create();
                    aes.Key = candidateKey;
                    aes.IV = iv;

                    using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                    var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                    var decrypted = Encoding.UTF8.GetString(plainBytes);
                    if (!string.IsNullOrEmpty(decrypted))
                    {
                        return decrypted;
                    }
                }
                catch (CryptographicException)
                {
                    // Continue to next candidate key
                }
            }

            // If decryption fails across all keys, do not leak raw ciphertext to UI
            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
