using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Customer.Payments;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Infrastructure.BackgroundJobs;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;

namespace SelfStorageSystem.Tests;

public class TransferWebhookRegressionTests
{
    [Theory]
    [InlineData(6067, false)]
    [InlineData(1000, false)]
    [InlineData(6067, true)]
    public async Task VoidedTransferWebhook_RecordsMoneyForReconciliation_WithoutReopeningTransfer(decimal amount, bool pendingExists)
    {
        using var db = new SelfStorageDbContext(new DbContextOptionsBuilder<SelfStorageDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CustomerProfiles.Add(new CustomerProfile
        {
            UserId = 7, FullName = "Webhook Test",
            User = new User { Id = 7, Email = "webhook@example.com", PasswordHash = "test", Status = "active" }
        });
        var invoice = new Invoice
        {
            Id = 49332, InvoiceNo = "INV-TRF-TEST", CustomerId = 7,
            Status = "voided", Currency = "VND", TotalAmount = 6067, BillingPeriod = "TRANSFER:8"
        };
        db.Invoices.Add(invoice);
        db.UnitTransferRequests.Add(new UnitTransferRequest { Id = 8, AgreementId = 1, Reason = "Transfer", Status = "cancelled" });
        if (pendingExists)
        {
            db.Payments.Add(new Payment
            {
                CustomerId = 7, TargetInvoiceId = invoice.Id, Amount = amount,
                Currency = "VND", Method = "bank_transfer", Provider = "SePay",
                Status = "pending", IdempotencyKey = "existing", Metadata = "{}",
                ProviderTransactionId = "FT-REGRESSION"
            });
        }
        await db.SaveChangesAsync();
        var service = new CustomerPaymentService(db, Mock.Of<IEmailService>(),
            Options.Create(new PaymentSettings { SePay = new SePayConfig { InvoicePrefix = "DH" } }),
            NullLogger<CustomerPaymentService>.Instance);
        var payload = new SePayWebhookPayload
        {
            Id = 97619134, TransferAmount = amount, TransferType = "in",
            Content = "DH49332", ReferenceCode = "FT-REGRESSION"
        };

        Assert.True(await service.ProcessSePayWebhookAsync(payload));
        Assert.True(await service.ProcessSePayWebhookAsync(payload));
        var payment = Assert.Single(await db.Payments.ToListAsync());
        Assert.Equal("succeeded", payment.Status);
        Assert.Equal(amount, payment.Amount);
        Assert.NotNull(payment.PaidAt);
        Assert.True(JsonDocument.Parse(payment.Metadata).RootElement.GetProperty("requiresReconciliation").GetBoolean());
        Assert.Empty(await db.PaymentAllocations.ToListAsync());
        Assert.Equal("voided", invoice.Status);
        Assert.Equal(0, invoice.PaidAmount);
        Assert.Equal("cancelled", (await db.UnitTransferRequests.FindAsync(8L))!.Status);
        Assert.True(Assert.Single(await service.GetMyPaymentHistoryAsync(7)).RequiresReconciliation);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Lifecycle_DoesNotExpireFreshTransferHold_BasedOnPreviousUtcDate(bool hasBillingMarker)
    {
        var services = new ServiceCollection();
        var name = Guid.NewGuid().ToString();
        services.AddDbContext<SelfStorageDbContext>(options => options.UseInMemoryDatabase(name));
        using var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
            db.Invoices.Add(new Invoice
            {
                Id = 1, InvoiceNo = "INV-TRF-TEST", CustomerId = 7, Status = "open", Currency = "VND",
                TotalAmount = 6067, DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
                BillingPeriod = hasBillingMarker ? "TRANSFER:8" : null, CreatedAt = DateTimeOffset.UtcNow
            });
            db.Invoices.Add(new Invoice
            {
                Id = 2, InvoiceNo = "INV-NORMAL", CustomerId = 7, Status = "open", Currency = "VND",
                TotalAmount = 1000, DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1)
            });
            db.UnitTransferRequests.Add(new UnitTransferRequest
            {
                Id = 8, AgreementId = 1, Reason = "Transfer", Status = "pending_payment", CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }
        var factory = provider.GetRequiredService<IServiceScopeFactory>();
        var worker = new StorageLifecycleBackgroundService(factory, new ConfigurationBuilder().Build(),
            NullLogger<StorageLifecycleBackgroundService>.Instance);
        await (Task)typeof(StorageLifecycleBackgroundService).GetMethod("ProcessLifecycleAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(worker, new object[] { CancellationToken.None })!;

        var expiry = new TransferExpiryWorker(factory, Options.Create(new RentalSettings()), NullLogger<TransferExpiryWorker>.Instance);
        await (Task)typeof(TransferExpiryWorker).GetMethod("SweepAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(expiry, new object[] { CancellationToken.None })!;
        using var verification = provider.CreateScope();
        var result = verification.ServiceProvider.GetRequiredService<SelfStorageDbContext>();
        Assert.Equal("open", (await result.Invoices.FindAsync(1L))!.Status);
        Assert.Equal("overdue", (await result.Invoices.FindAsync(2L))!.Status);
        Assert.Equal("pending_payment", (await result.UnitTransferRequests.FindAsync(8L))!.Status);
    }
}
