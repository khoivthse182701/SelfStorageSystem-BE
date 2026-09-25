using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Infrastructure.Services;
using Xunit;

namespace SelfStorageSystem.Tests;

public class OtpAntiSpamTests
{
    private readonly OtpService _otpService;
    private readonly OtpSettings _settings;

    public OtpAntiSpamTests()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        _settings = new OtpSettings
        {
            ExpirationMinutes = 10,
            CodeLength = 6,
            ResendCooldownSeconds = 60,
            MaxFailedAttempts = 5,
            MaxRequestsPerHour = 5,
            LockoutMinutes = 15
        };
        var options = Options.Create(_settings);
        _otpService = new OtpService(memoryCache, options);
    }

    [Fact]
    public void GenerateOtp_ShouldSetCooldown()
    {
        var email = "test@example.com";
        var code = _otpService.GenerateAndStoreOtp(email);

        Assert.NotNull(code);
        Assert.Equal(6, code.Length);
        Assert.True(_otpService.IsInCooldown(email));
        Assert.InRange(_otpService.GetRemainingCooldownSeconds(email), 1, 60);
    }

    [Fact]
    public void VerifyOtp_MultipleWrongAttempts_ShouldLockout()
    {
        var email = "bruteforce@example.com";
        _otpService.GenerateAndStoreOtp(email);

        for (int i = 0; i < 5; i++)
        {
            var isValid = _otpService.VerifyOtp(email, "000000");
            Assert.False(isValid);
            _otpService.RecordFailedAttempt(email);
        }

        Assert.True(_otpService.HasExceededMaxAttempts(email));
        Assert.True(_otpService.IsLockedOut(email));
        Assert.InRange(_otpService.GetRemainingLockoutMinutes(email), 1, 15);
    }

    [Fact]
    public void GenerateOtp_ExceedingHourlyLimit_ShouldBeDetected()
    {
        var email = "spammer@example.com";

        for (int i = 0; i < 5; i++)
        {
            _otpService.GenerateAndStoreOtp(email);
        }

        Assert.True(_otpService.HasExceededHourlyLimit(email));
    }

    [Fact]
    public void VerifyOtp_ValidCode_ShouldSucceed()
    {
        var email = "valid@example.com";
        var code = _otpService.GenerateAndStoreOtp(email);

        var isValid = _otpService.VerifyOtp(email, code);
        Assert.True(isValid);

        // Invalidate OTP upon successful verification
        _otpService.InvalidateOtp(email);
        Assert.False(_otpService.VerifyOtp(email, code));
    }

    [Fact]
    public void RecordFailedAttempt_ShouldTrackRemainingAttempts()
    {
        var email = "countdown@example.com";
        _otpService.GenerateAndStoreOtp(email);

        Assert.Equal(5, _otpService.GetRemainingAttempts(email));

        _otpService.RecordFailedAttempt(email);
        Assert.Equal(4, _otpService.GetRemainingAttempts(email));

        _otpService.RecordFailedAttempt(email);
        Assert.Equal(3, _otpService.GetRemainingAttempts(email));
    }
}
