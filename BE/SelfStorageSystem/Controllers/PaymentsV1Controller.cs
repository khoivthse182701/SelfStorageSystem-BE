using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Payments;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
public sealed class PaymentsV1Controller(ICustomerPaymentService paymentService) : ControllerBase
{
    [HttpPost("api/v1/payments/checkout")]
    [ProducesResponseType(typeof(ApiResponse<CheckoutResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateCheckout([FromBody] CreateCheckoutRequest request, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var response = await paymentService.CreateCheckoutAsync(customerId, request, ct);
        return Ok(ApiResponse<CheckoutResponse>.Ok(response, "Checkout payment request initialized successfully."));
    }

    [HttpGet("api/v1/payments/{paymentId:long}/status")]
    [ProducesResponseType(typeof(ApiResponse<PaymentStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPaymentStatus(long paymentId, CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var status = await paymentService.GetPaymentStatusAsync(customerId, paymentId, ct);
        return Ok(ApiResponse<PaymentStatusDto>.Ok(status, "Payment status retrieved successfully."));
    }

    [HttpGet("api/v1/payments/my-history")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PaymentHistoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPaymentHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var result = await paymentService.GetMyPaymentHistoryPagedAsync(customerId, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<PaymentHistoryDto>>.Ok(result, "Retrieved payment history successfully."));
    }

    [HttpGet("api/v1/invoices/unpaid")]
    [ProducesResponseType(typeof(ApiResponse<List<UnpaidInvoiceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnpaidInvoices(CancellationToken ct)
    {
        if (!TryGetCustomerId(out var customerId))
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token.", "Auth.UserTokenInvalid"));

        var invoices = await paymentService.GetMyUnpaidInvoicesAsync(customerId, ct);
        return Ok(ApiResponse<List<UnpaidInvoiceDto>>.Ok(invoices, "Retrieved unpaid invoices successfully."));
    }

    private bool TryGetCustomerId(out long customerId)
    {
        customerId = 0;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(claim) && long.TryParse(claim, out customerId);
    }
}
