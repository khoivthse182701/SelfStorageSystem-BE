using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Customer.Payments;
using SelfStorageSystem.Controllers;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;
using Xunit;

namespace SelfStorageSystem.Tests;

public class CustomerPaymentSecurityTests
{
    private static SelfStorageDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SelfStorageDbContext>()
            .UseInMemoryDatabase(databaseName: $"PaymentSecDb_{Guid.NewGuid()}")
            .Options;
        return new SelfStorageDbContext(options);
    }

    [Fact]
    public async Task SePayWebhook_UnconfiguredApiKey_ReturnsServiceUnavailable503()
    {
        // Arrange
        var paymentServiceMock = new Mock<ICustomerPaymentService>();
        var settings = Options.Create(new PaymentSettings
        {
            SePay = new SePayConfig { ApiKey = "" } // Empty / Unconfigured
        });

        var controller = new CustomerPaymentsController(
            paymentServiceMock.Object,
            settings,
            NullLogger<CustomerPaymentsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var payload = new SePayWebhookPayload
        {
            Id = 1234,
            Content = "DH100 Payment",
            TransferAmount = 100000m
        };

        // Act
        var result = await controller.SePayWebhook(payload, CancellationToken.None);

        // Assert: Fail-Closed 503 Service Unavailable
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }

    [Fact]
    public async Task SePayWebhook_InvalidApiKey_ReturnsUnauthorized401()
    {
        // Arrange
        var paymentServiceMock = new Mock<ICustomerPaymentService>();
        var settings = Options.Create(new PaymentSettings
        {
            SePay = new SePayConfig { ApiKey = "SecretKey123" }
        });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Api-Key"] = "WrongKey";

        var controller = new CustomerPaymentsController(
            paymentServiceMock.Object,
            settings,
            NullLogger<CustomerPaymentsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var payload = new SePayWebhookPayload
        {
            Id = 1235,
            Content = "DH100 Payment",
            TransferAmount = 100000m
        };

        // Act
        var result = await controller.SePayWebhook(payload, CancellationToken.None);

        // Assert: 401 Unauthorized
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task SePayWebhook_ValidApiKey_ProcessesSuccessfully()
    {
        // Arrange
        var paymentServiceMock = new Mock<ICustomerPaymentService>();
        paymentServiceMock.Setup(s => s.ProcessSePayWebhookAsync(It.IsAny<SePayWebhookPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var settings = Options.Create(new PaymentSettings
        {
            SePay = new SePayConfig { ApiKey = "SecretKey123" }
        });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = "Bearer SecretKey123";

        var controller = new CustomerPaymentsController(
            paymentServiceMock.Object,
            settings,
            NullLogger<CustomerPaymentsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var payload = new SePayWebhookPayload
        {
            Id = 1236,
            Content = "DH100 Payment",
            TransferAmount = 100000m
        };

        // Act
        var result = await controller.SePayWebhook(payload, CancellationToken.None);

        // Assert: 200 OK
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SePayWebhook_AmbiguousInvoiceWithoutIdentifier_ReturnsFalseAndDoesNotMatch()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var user = new User { Id = 1, Email = "victim@test.com", PasswordHash = "hash", Status = "active" };
        var customer = new CustomerProfile { UserId = 1, FullName = "Victim Customer", IdentityNumber = "001" };
        var invoice = new Invoice
        {
            Id = 555,
            InvoiceNo = "INV-202610-000555",
            CustomerId = 1,
            TotalAmount = 500000m,
            PaidAmount = 0m,
            Currency = PaymentConstants.CurrencyVnd,
            Status = InvoiceStatusConstants.Open,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5))
        };
        dbContext.Users.Add(user);
        dbContext.CustomerProfiles.Add(customer);
        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync();

        var paymentService = new CustomerPaymentService(
            dbContext,
            Mock.Of<IEmailService>(),
            Options.Create(new PaymentSettings
            {
                SePay = new SePayConfig { ApiKey = "Secret123", InvoicePrefix = "DH" }
            }),
            NullLogger<CustomerPaymentService>.Instance);

        // Act: Webhook received transfer of exactly 500,000 VND but memo has NO invoice/reservation/renewal ID
        var payload = new SePayWebhookPayload
        {
            Id = 8888,
            Content = "Tien an trua Nguyen Van B", // Random memo without identifiers
            TransferAmount = 500000m
        };

        var result = await paymentService.ProcessSePayWebhookAsync(payload);

        // Assert: Must return false and NOT touch victim's open invoice
        Assert.False(result);
        var unchangedInvoice = await dbContext.Invoices.FindAsync(555L);
        Assert.NotNull(unchangedInvoice);
        Assert.Equal(0m, unchangedInvoice.PaidAmount);
        Assert.Equal(InvoiceStatusConstants.Open, unchangedInvoice.Status);
    }

    [Fact]
    public void PinProtectionHelper_EncryptAndDecrypt_RoundTripSuccess()
    {
        // Arrange
        var originalPin = "839201";

        // Act
        var encrypted = PinProtectionHelper.Encrypt(originalPin);
        var decrypted = PinProtectionHelper.Decrypt(encrypted);

        // Assert
        Assert.StartsWith("ENC:", encrypted);
        Assert.NotEqual(originalPin, encrypted);
        Assert.Equal(originalPin, decrypted);
    }

    [Fact]
    public void PinProtectionHelper_DecryptLegacyPlaintext_ReturnsUnchanged()
    {
        // Arrange
        var legacyPin = "654321";

        // Act
        var decrypted = PinProtectionHelper.Decrypt(legacyPin);

        // Assert
        Assert.Equal(legacyPin, decrypted);
    }
}
