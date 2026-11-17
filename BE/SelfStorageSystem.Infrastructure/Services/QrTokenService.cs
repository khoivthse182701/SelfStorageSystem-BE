using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SelfStorageSystem.Application.Interfaces;

namespace SelfStorageSystem.Infrastructure.Services;

public sealed class QrTokenService : IQrTokenService
{
    private const string VersionPrefix = "v1";
    private readonly byte[] _signingKey;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public QrTokenService(IConfiguration configuration)
    {
        var rawKey = configuration["SecuritySettings:QrSigningKey"]
            ?? configuration["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("SecuritySettings:QrSigningKey or JwtSettings:Secret must be configured.");

        if (rawKey.Length < 32)
            throw new InvalidOperationException("QR signing key must be at least 32 characters long.");

        _signingKey = Encoding.UTF8.GetBytes(rawKey);
    }

    public QrTokenService(string signingSecret)
    {
        if (string.IsNullOrWhiteSpace(signingSecret) || signingSecret.Length < 32)
            throw new ArgumentException("QR signing key must be at least 32 characters long.", nameof(signingSecret));
        _signingKey = Encoding.UTF8.GetBytes(signingSecret);
    }

    public string IssueToken(string typ, long? rsvId, long? agrId, long customerId, long facilityId, TimeSpan? ttl = null)
    {
        var now = DateTimeOffset.UtcNow;
        var exp = now.Add(ttl ?? TimeSpan.FromDays(30));

        var payload = new QrTokenPayload(
            typ.Trim().ToLowerInvariant(),
            rsvId,
            agrId,
            customerId,
            facilityId,
            now.ToUnixTimeSeconds(),
            exp.ToUnixTimeSeconds());

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var encodedPayload = Base64UrlEncode(payloadBytes);
        var signingInput = $"{VersionPrefix}.{encodedPayload}";
        var sigBytes = ComputeHmac(signingInput);
        return $"{signingInput}.{Base64UrlEncode(sigBytes)}";
    }

    public bool TryVerifyToken(string? token, string expectedType, out QrTokenPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(token)) return false;

        var parts = token.Trim().Split('.');
        if (parts.Length != 3 || !string.Equals(parts[0], VersionPrefix, StringComparison.Ordinal))
            return false;

        try
        {
            var expectedSig = ComputeHmac($"{parts[0]}.{parts[1]}");
            var actualSig = Base64UrlDecode(parts[2]);
            if (actualSig.Length != expectedSig.Length || !CryptographicOperations.FixedTimeEquals(actualSig, expectedSig))
                return false;

            var payloadBytes = Base64UrlDecode(parts[1]);
            var parsed = JsonSerializer.Deserialize<QrTokenPayload>(payloadBytes, JsonOptions);
            if (parsed is null ||
                !string.Equals(parsed.Typ, expectedType, StringComparison.OrdinalIgnoreCase) ||
                parsed.Exp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                return false;
            }

            payload = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private byte[] ComputeHmac(string data)
    {
        using var hmac = new HMACSHA256(_signingKey);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
