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
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateCheckout(
        [FromBody] CreateCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        try
        {
            var response = await _paymentService.CreateCheckoutAsync(customerId, request, cancellationToken);
            return Ok(ApiResponse<CheckoutResponse>.Ok(response, "Checkout payment request initialized successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating checkout request: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("A system error occurred while generating payment request."));
        }
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
        var expectedKey = _paymentSettings.SePay?.ApiKey;

        if (!string.IsNullOrWhiteSpace(expectedKey))
        {
            var isValidKey = false;
            if (!string.IsNullOrEmpty(authHeader))
            {
                var token = authHeader.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase)
                                      .Replace("Apikey ", "", StringComparison.OrdinalIgnoreCase).Trim();
                isValidKey = string.Equals(token, expectedKey.Trim(), StringComparison.Ordinal);
            }

            if (!isValidKey)
            {
                _logger.LogWarning("SePay Webhook: Access denied due to missing or invalid API Key.");
                return Unauthorized(new { success = false, message = "Invalid SePay API Key or signature." });
            }
        }

        try
        {
            var processed = await _paymentService.ProcessSePayWebhookAsync(payload, cancellationToken);
            if (processed)
            {
                return Ok(new { success = true, message = "Payment matched and processed successfully." });
            }

            return Ok(new { success = false, message = "Transaction did not match any open invoice or was already processed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SePay Webhook: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Webhook internal system error." });
        }
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

        try
        {
            var history = await _paymentService.GetMyPaymentHistoryAsync(customerId, cancellationToken);
            return Ok(ApiResponse<List<PaymentHistoryDto>>.Ok(history, "Retrieved payment history successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment history for customer {CustomerId}: {Message}", customerId, ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("A system error occurred while retrieving payment history."));
        }
    }

    private bool TryGetCustomerId(out long customerId)
    {
        customerId = 0;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(userIdClaim) && long.TryParse(userIdClaim, out customerId);
    }
}
