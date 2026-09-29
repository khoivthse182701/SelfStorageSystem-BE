using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Payments;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Route("api/customer/payments")]
public class CustomerPaymentsController : ControllerBase
{
    private readonly ICustomerPaymentService _paymentService;
    private readonly PaymentSettings _paymentSettings;
    private readonly ILogger<CustomerPaymentsController> _logger;

    public CustomerPaymentsController(
        ICustomerPaymentService paymentService,
        Microsoft.Extensions.Options.IOptions<PaymentSettings> paymentOptions,
        ILogger<CustomerPaymentsController> logger)
    {
        _paymentService = paymentService;
        _paymentSettings = paymentOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Creates checkout payment information and dynamic VietQR details processed via SePay.
    /// </summary>
    [HttpPost("create-checkout")]
    [Authorize(Roles = "storage_customer")]
    [ProducesResponseType(typeof(ApiResponse<CheckoutResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCheckout(
        [FromBody] CreateCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var response = await _paymentService.CreateCheckoutAsync(customerId, request, cancellationToken);
        return Ok(ApiResponse<CheckoutResponse>.Ok(response, "Checkout payment request initialized successfully."));
    }

    /// <summary>
    /// Automatic Webhook from SePay on bank balance credit updates via VietQR.
    /// </summary>
    [HttpPost("sepay-webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> SePayWebhook(
        [FromBody] SePayWebhookPayload payload,
        CancellationToken cancellationToken)
    {
        // Verify API Key / Bearer Token from SePay
        var authHeader = Request.Headers["Authorization"].ToString();
        var apiKeyHeader = Request.Headers["X-Api-Key"].ToString();
        var expectedKey = _paymentSettings.SePay?.ApiKey;

        if (!string.IsNullOrWhiteSpace(expectedKey))
        {
            var isValidKey = false;
            var expectedTrimmed = expectedKey.Trim();

            if (!string.IsNullOrWhiteSpace(apiKeyHeader) && string.Equals(apiKeyHeader.Trim(), expectedTrimmed, StringComparison.Ordinal))
            {
                isValidKey = true;
            }
            else if (!string.IsNullOrWhiteSpace(authHeader))
            {
                var token = authHeader.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase)
                                      .Replace("Apikey ", "", StringComparison.OrdinalIgnoreCase).Trim();
                isValidKey = string.Equals(token, expectedTrimmed, StringComparison.Ordinal);
            }

            if (!isValidKey)
            {
                _logger.LogWarning("SePay Webhook: Access denied due to missing or invalid API Key.");
                return Unauthorized(new { success = false, message = "Invalid SePay API Key or signature." });
            }
        }

        var processed = await _paymentService.ProcessSePayWebhookAsync(payload, cancellationToken);
        if (processed)
        {
            return Ok(new { success = true, message = "Payment matched and processed successfully." });
        }

        return Ok(new { success = false, message = "Transaction did not match any open invoice or was already processed." });
    }

    /// <summary>
    /// Retrieves payment history and receipts for current authenticated customer.
    /// </summary>
    [HttpGet("my-history")]
    [Authorize(Roles = "storage_customer")]
    [ProducesResponseType(typeof(ApiResponse<List<PaymentHistoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPaymentHistory(CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var history = await _paymentService.GetMyPaymentHistoryAsync(customerId, cancellationToken);
        return Ok(ApiResponse<List<PaymentHistoryDto>>.Ok(history, "Retrieved payment history successfully."));
    }

    private bool TryGetCustomerId(out long customerId)
    {
        customerId = 0;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(userIdClaim) && long.TryParse(userIdClaim, out customerId);
    }
}
