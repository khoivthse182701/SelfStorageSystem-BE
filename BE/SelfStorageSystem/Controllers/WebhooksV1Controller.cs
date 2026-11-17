using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Payments;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Route("api/v1/webhooks")]
public sealed class WebhooksV1Controller(
    ICustomerPaymentService paymentService,
    IOptions<PaymentSettings> paymentOptions,
    ILogger<WebhooksV1Controller> logger) : ControllerBase
{
    private readonly PaymentSettings _paymentSettings = paymentOptions.Value;

    [HttpPost("sepay")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SePayWebhookResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SePayWebhook([FromBody] SePayWebhookPayload payload, CancellationToken ct)
    {
        var expectedKey = _paymentSettings.SePay?.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(expectedKey) ||
            expectedKey.StartsWith("__SET_", StringComparison.OrdinalIgnoreCase) ||
            expectedKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogCritical("SePay v1 Webhook rejected: API key is not configured on server.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse.Fail("SePay API key not configured.", "Payment.WebhookNotConfigured"));
        }

        if (!IsValidSePayKey(expectedKey))
        {
            logger.LogWarning("SePay v1 Webhook rejected: Invalid or missing API key.");
            return Unauthorized(ApiResponse.Fail("Invalid SePay API key.", "Payment.InvalidWebhookApiKey"));
        }

        var result = await paymentService.ProcessSePayWebhookWithResultAsync(payload, ct);
        return Ok(ApiResponse<SePayWebhookResultDto>.Ok(result, $"Webhook processed with action '{result.Action}'."));
    }

    private bool IsValidSePayKey(string expectedKey)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);

        var apiKeyHeader = Request.Headers["X-Api-Key"].ToString().Trim();
        if (!string.IsNullOrEmpty(apiKeyHeader))
        {
            var actualBytes = Encoding.UTF8.GetBytes(apiKeyHeader);
            if (actualBytes.Length == expectedBytes.Length && CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes))
                return true;
        }

        var authHeader = Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrWhiteSpace(authHeader))
        {
            var token = authHeader
                .Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Apikey ", "", StringComparison.OrdinalIgnoreCase)
                .Trim();
            var actualBytes = Encoding.UTF8.GetBytes(token);
            if (actualBytes.Length == expectedBytes.Length && CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes))
                return true;
        }

        return false;
    }
}
