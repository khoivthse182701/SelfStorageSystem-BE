using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Admin;
using SelfStorageSystem.Contracts.Auth;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;
using Xunit;

namespace SelfStorageSystem.Tests;

public class LoginHistoryFlowTests
{
    private const string Ip = "10.0.0.5";
    private const string Ua = "xunit-agent";

    private static SelfStorageDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<SelfStorageDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static AuthService CreateAuthService(SelfStorageDbContext db)
    {
        var jwt = new Mock<IJwtTokenService>();
        jwt.Setup(j => j.GenerateToken(It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
           .Returns("token");

        return new AuthService(
            db,
            jwt.Object,
            Mock.Of<IEmailService>(),
            Mock.Of<IOtpService>(),
            Mock.Of<IGoogleAuthService>(),
            Options.Create(new OtpSettings()),
            Options.Create(new JwtSettings()),
            NullLogger<AuthService>.Instance);
    }

    private static async Task SeedUsersAsync(SelfStorageDbContext db)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("Pass@123");
        db.Users.AddRange(
            new User { Id = 1, Email = "active@test.com", PasswordHash = hash, Status = UserStatusConstants.Active },
            new User { Id = 2, Email = "locked@test.com", PasswordHash = hash, Status = UserStatusConstants.Locked },
            new User { Id = 3, Email = "disabled@test.com", PasswordHash = hash, Status = UserStatusConstants.Disabled });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task LoginAttempts_AreRecorded_AndReturnedByAdminReport()
    {
        await using var db = CreateDb();
        await SeedUsersAsync(db);
        var auth = CreateAuthService(db);

        // 1 success
        await auth.LoginAsync(new LoginRequest { Email = "Active@Test.com", Password = "Pass@123" }, default, Ip, Ua);
        // 3 wrong passwords from same IP -> brute force
        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAnyAsync<Exception>(() =>
                auth.LoginAsync(new LoginRequest { Email = "active@test.com", Password = "wrong" }, default, Ip, Ua));
        }
        // unknown email, locked, disabled
        await Assert.ThrowsAnyAsync<Exception>(() =>
            auth.LoginAsync(new LoginRequest { Email = "ghost@test.com", Password = "x" }, default, "10.0.0.9", Ua));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            auth.LoginAsync(new LoginRequest { Email = "locked@test.com", Password = "Pass@123" }, default, "10.0.0.9", Ua));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            auth.LoginAsync(new LoginRequest { Email = "disabled@test.com", Password = "Pass@123" }, default, "10.0.0.9", Ua));

        var admin = new AdminManagementService(db, NullLogger<AdminManagementService>.Instance);

        // Get all (no filters)
        var all = await admin.GetLoginHistoriesAsync(new AdminLoginHistoryQueryParameters());
        Assert.Equal(7, all.Histories.TotalCount);
        Assert.Equal(7, all.Summary.TotalAttempts);
        Assert.Equal(1, all.Summary.SucceededAttempts);
        Assert.Equal(4, all.Summary.FailedAttempts);
        Assert.Equal(1, all.Summary.LockedAttempts);
        Assert.Equal(1, all.Summary.BlockedAttempts);
        // 3 brute-force failures + locked + blocked
        Assert.Equal(5, all.Summary.SuspiciousAttemptsCount);

        var success = Assert.Single(all.Histories.Items, h => h.Result == LoginHistoryResultConstants.Succeeded);
        Assert.Equal(1, success.UserId);
        Assert.Equal("active@test.com", success.AttemptedEmail);
        Assert.Equal(Ip, success.IpAddress);
        Assert.Equal(Ua, success.UserAgent);

        var ghost = Assert.Single(all.Histories.Items, h => h.AttemptedEmail == "ghost@test.com");
        Assert.Null(ghost.UserId);

        // Filter by user
        var byUser = await admin.GetLoginHistoriesAsync(new AdminLoginHistoryQueryParameters { UserId = 1 });
        Assert.Equal(4, byUser.Histories.TotalCount);

        // Suspicious only, filtered before paging
        var suspicious = await admin.GetLoginHistoriesAsync(new AdminLoginHistoryQueryParameters { IsSuspiciousOnly = true, PageSize = 2 });
        Assert.Equal(5, suspicious.Histories.TotalCount);
        Assert.Equal(2, suspicious.Histories.Items.Count);
        Assert.All(suspicious.Histories.Items, h => Assert.True(h.IsSuspicious));
    }
}
