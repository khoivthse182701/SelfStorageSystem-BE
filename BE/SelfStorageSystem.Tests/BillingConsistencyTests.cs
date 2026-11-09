using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Customer.Payments;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;

namespace SelfStorageSystem.Tests;

public class BillingConsistencyTests
{
    private static SelfStorageDbContext CreateDb() => new(new DbContextOptionsBuilder<SelfStorageDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CustomerPaymentService CreateService(SelfStorageDbContext db) => new(
        db, Mock.Of<IEmailService>(), Options.Create(new PaymentSettings()),
        NullLogger<CustomerPaymentService>.Instance);

    [Theory]
    [InlineData("cancelled", 10, false)]
    [InlineData("expired", 10, false)]
    [InlineData("converted", 10, false)]
    [InlineData("confirmed", 10, false)]
    [InlineData("pending", -10, false)]
    [InlineData("pending", 10, true)]
    [InlineData("awaiting_deposit", 10, true)]
    public async Task UnpaidInvoices_RespectsCurrentBookingState(string status, int minutes, bool payable)
    {
        using var db = CreateDb();
        var reservation = new Reservation
        {
            Id = 1, CustomerId = 7, ReservationCode = "RSV-CONSISTENCY",
            Status = status, HoldUntil = DateTimeOffset.UtcNow.AddMinutes(minutes)
        };
        db.Reservations.Add(reservation);
        db.Invoices.Add(new Invoice
        {
            Id = 1, InvoiceNo = "INV-CONSISTENCY", CustomerId = 7,
            Reservation = reservation, Status = "overdue", Currency = "VND", TotalAmount = 4000
        });
        await db.SaveChangesAsync();

        var rows = await CreateService(db).GetMyUnpaidInvoicesAsync(7);
        Assert.Equal(payable ? 1 : 0, rows.Count);
        if (payable)
        {
            Assert.Equal(status, rows[0].ReservationStatus);
            Assert.Equal(reservation.HoldUntil, rows[0].HoldUntil);
        }
        Assert.Empty(await CreateService(db).GetMyUnpaidInvoicesAsync(8));
        if (!payable)
        {
            await Assert.ThrowsAsync<AppConflictException>(() => CreateService(db).CreateCheckoutAsync(7,
                new CreateCheckoutRequest { InvoiceId = 1 }));
        }
    }

    [Fact]
    public async Task IndependentOverdueInvoice_CanCreateCheckoutForRemainingBalance()
    {
        using var db = CreateDb();
        db.Invoices.Add(new Invoice
        {
            Id = 1, InvoiceNo = "INV-OVERDUE", CustomerId = 7, Status = "overdue",
            Currency = "VND", TotalAmount = 4000, PaidAmount = 1000
        });
        await db.SaveChangesAsync();

        var checkout = await CreateService(db).CreateCheckoutAsync(7, new CreateCheckoutRequest { InvoiceId = 1 });
        Assert.Equal(3000m, checkout.Amount);
        Assert.True(checkout.ExpiresInSeconds > 0);
    }
}
