using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.SupportTickets;
using SelfStorageSystem.Domain.Constants;

namespace SelfStorageSystem.Controllers;

[ApiController]
[Route("api/customer/support-tickets")]
[Authorize(Roles = RoleConstants.Customer)]
public class CustomerSupportTicketsController : ControllerBase
{
    private readonly ICustomerSupportTicketService _ticketService;

    public CustomerSupportTicketsController(ICustomerSupportTicketService ticketService)
    {
        _ticketService = ticketService;
    }

    /// <summary>
    /// Creates a new support ticket or incident report.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SupportTicketSummaryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateTicket(
        [FromBody] CreateSupportTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var ticket = await _ticketService.CreateTicketAsync(customerId, request, cancellationToken);
        return CreatedAtAction(nameof(GetTicketDetail), new { id = ticket.Id }, 
            ApiResponse<SupportTicketSummaryDto>.Ok(ticket, "Support ticket created successfully."));
    }

    /// <summary>
    /// Retrieves a list of support tickets for the authenticated customer.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SupportTicketSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyTickets(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var tickets = await _ticketService.GetCustomerTicketsAsync(customerId, status, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SupportTicketSummaryDto>>.Ok(tickets, "Retrieved support tickets successfully."));
    }

    /// <summary>
    /// Retrieves detailed information and message history for a specific support ticket.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<SupportTicketDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicketDetail(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var ticket = await _ticketService.GetTicketDetailAsync(customerId, id, cancellationToken);
        return Ok(ApiResponse<SupportTicketDetailDto>.Ok(ticket, "Retrieved support ticket details successfully."));
    }

    /// <summary>
    /// Adds a new message/reply to an existing support ticket.
    /// </summary>
    [HttpPost("{id:long}/messages")]
    [ProducesResponseType(typeof(ApiResponse<TicketMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMessage(
        [FromRoute] long id,
        [FromBody] AddTicketMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        var message = await _ticketService.AddMessageAsync(customerId, id, request, cancellationToken);
        return Ok(ApiResponse<TicketMessageDto>.Ok(message, "Message added successfully."));
    }

    /// <summary>
    /// Confirms resolution of a support ticket and provides a service rating.
    /// </summary>
    [HttpPost("{id:long}/confirm-and-rate")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmAndRateTicket(
        [FromRoute] long id,
        [FromBody] ConfirmAndRateTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(ApiResponse.Fail("Cannot identify customer identity from token."));
        }

        await _ticketService.ConfirmAndRateTicketAsync(customerId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, "Ticket confirmed and rated successfully."));
    }

    private bool TryGetCustomerId(out long customerId)
    {
        customerId = 0;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(userIdClaim) && long.TryParse(userIdClaim, out customerId);
    }
}
