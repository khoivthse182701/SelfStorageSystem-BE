using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Rentals;
using SelfStorageSystem.Contracts.Manager;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Common;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public partial class FacilityManagerService : IFacilityManagerService
{
    // ==========================================

    // 3. Phê Duyệt Tài Chính

    // ==========================================



    public async Task<IReadOnlyList<ManagerChargeProposalDto>> GetChargeProposalsAsync(

        long facilityId,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);



        var proposals = await _dbContext.TicketChargeProposals

            .AsNoTracking()

            .Include(p => p.Ticket)

                .ThenInclude(t => t.Customer)

            .Include(p => p.Ticket)

                .ThenInclude(t => t.Agreement)

            .Include(p => p.ProposedByNavigation)

            .Include(p => p.TicketChargeApproval)

            .Where(p => p.Ticket.FacilityId == facilityId)

            .OrderByDescending(p => p.CreatedAt)

            .Select(p => new ManagerChargeProposalDto

            {

                Id = p.Id,

                TicketId = p.TicketId,

                TicketTitle = p.Ticket.Subject,

                CustomerId = p.Ticket.CustomerId,

                CustomerName = p.Ticket.Customer.FullName,

                AgreementId = p.Ticket.AgreementId,

                AgreementNo = p.Ticket.Agreement != null ? p.Ticket.Agreement.AgreementNo : null,

                ProposedBy = p.ProposedBy,

                ProposedByStaffName = p.ProposedByNavigation.FullName,

                Description = p.Description,

                Amount = p.Amount,

                Status = p.Status,

                CreatedAt = p.CreatedAt,

                Decision = p.TicketChargeApproval != null ? p.TicketChargeApproval.Decision : null,

                DecisionReason = p.TicketChargeApproval != null ? p.TicketChargeApproval.Reason : null,

                DecidedAt = p.TicketChargeApproval != null ? p.TicketChargeApproval.DecidedAt : null

            })

            .ToListAsync(cancellationToken);



        return proposals;

    }



    public async Task<ManagerChargeProposalDecisionDto> DecideChargeProposalAsync(

        long id,

        ManagerDecideChargeProposalRequest request,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var proposal = await _dbContext.TicketChargeProposals

            .Include(p => p.Ticket)

            .Include(p => p.TicketChargeApproval)

            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);



        if (proposal == null)

        {

            throw AppException.FromError(ManagerErrors.ChargeProposalNotFound);

        }



        await EnsureFacilityAccessAsync(proposal.Ticket.FacilityId, currentUserId, isSystemAdmin, cancellationToken);



        if (proposal.Status != ProposalStatusConstants.Pending)

        {

            throw AppException.FromError(ManagerErrors.ChargeProposalAlreadyReviewed);

        }



        var decision = request.Decision.Trim().ToLowerInvariant();

        if (!ApprovalDecisionConstants.All.Contains(decision))

        {

            throw AppException.FromError(ManagerErrors.InvalidDecision);

        }



        var managerEmployeeId = await GetManagerEmployeeIdAsync(currentUserId, cancellationToken);

        var now = DateTimeOffset.UtcNow;



        var approval = new TicketChargeApproval

        {

            ProposalId = proposal.Id,

            Decision = decision,

            DecidedBy = managerEmployeeId,

            Reason = request.Reason?.Trim(),

            DecidedAt = now

        };

        _dbContext.TicketChargeApprovals.Add(approval);



        string? generatedInvoiceNo = null;

        long? generatedInvoiceId = null;



        if (decision == ApprovalDecisionConstants.Approved)

        {

            proposal.Status = ProposalStatusConstants.Approved;



            // Generate automatic invoice for damage charge

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            generatedInvoiceNo = $"{ManagerConstants.DefaultInvoicePrefix}{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";



            var invoice = new Invoice

            {

                InvoiceNo = generatedInvoiceNo,

                CustomerId = proposal.Ticket.CustomerId,

                AgreementId = proposal.Ticket.AgreementId,

                TicketChargeProposalId = proposal.Id,

                IssueDate = today,

                DueDate = today.AddDays(ManagerConstants.DefaultInvoiceDueDays),

                Currency = PaymentConstants.CurrencyVnd,

                SubtotalAmount = proposal.Amount,

                TotalAmount = proposal.Amount,

                PaidAmount = 0m,

                Status = InvoiceStatusConstants.Open,

                CreatedAt = now,

                UpdatedAt = now

            };



            var invoiceLine = new InvoiceLine

            {

                Invoice = invoice,

                LineType = InvoiceLineTypeConstants.Rent == "damage" ? "damage" : "damage",

                Description = proposal.Description,

                Quantity = 1,

                UnitPrice = proposal.Amount,

                Metadata = ManagerConstants.DefaultActionDetails

            };



            invoice.InvoiceLines.Add(invoiceLine);

            _dbContext.Invoices.Add(invoice);



            await _dbContext.SaveChangesAsync(cancellationToken);

            generatedInvoiceId = invoice.Id;

        }

        else

        {

            proposal.Status = ProposalStatusConstants.Rejected;

            await _dbContext.SaveChangesAsync(cancellationToken);

        }



        return new ManagerChargeProposalDecisionDto

        {

            ProposalId = proposal.Id,

            Decision = decision,

            GeneratedInvoiceNo = generatedInvoiceNo,

            GeneratedInvoiceId = generatedInvoiceId,

            Amount = proposal.Amount,

            DecidedAt = now

        };

    }



    public async Task<IReadOnlyList<ManagerRefundApprovalDto>> GetRefundApprovalsAsync(

        long facilityId,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        await EnsureFacilityAccessAsync(facilityId, currentUserId, isSystemAdmin, cancellationToken);



        var refunds = await _dbContext.Refunds

            .AsNoTracking()

            .Include(r => r.Agreement)

                .ThenInclude(a => a!.Customer)

            .Include(r => r.Agreement)

                .ThenInclude(a => a!.Invoices)

                    .ThenInclude(i => i.InvoiceLines)

            .Include(r => r.RefundApproval)

            .Where(r => r.Agreement != null && r.Agreement.FacilityId == facilityId)

            .OrderByDescending(r => r.CreatedAt)

            .ToListAsync(cancellationToken);



        var result = refunds.Select(r =>

        {

            var deposit = r.Agreement?.DepositSnapshot ?? 0m;

            var unpaidDamages = r.Agreement?.Invoices

                .Where(i => i.Status != InvoiceStatusConstants.Paid)

                .SelectMany(i => i.InvoiceLines)

                .Where(l => l.LineType == "damage" || l.LineType == "penalty")

                .Sum(l => l.UnitPrice * l.Quantity) ?? 0m;



            var overdueDebt = r.Agreement?.Invoices

                .Where(i => i.Status == InvoiceStatusConstants.Overdue)

                .Sum(i => i.TotalAmount - i.PaidAmount) ?? 0m;



            var estimatedDeductions = unpaidDamages + overdueDebt;

            var recommendedRefund = Math.Max(0m, deposit - estimatedDeductions);

            var (bankName, accNumber, holder, _) = ExtractBankInfo(r.Reason);



            return new ManagerRefundApprovalDto

            {

                RefundId = r.Id,

                PaymentId = r.PaymentId,

                AgreementId = r.AgreementId,

                AgreementNo = r.Agreement?.AgreementNo,

                CustomerId = r.Agreement?.CustomerId ?? 0,

                CustomerName = r.Agreement?.Customer.FullName ?? string.Empty,

                Amount = r.Amount,

                Reason = r.Reason,

                Status = r.Status,

                CreatedAt = r.CreatedAt,

                Decision = r.RefundApproval?.Decision,

                DecisionReason = r.RefundApproval?.Reason,

                DecidedAt = r.RefundApproval?.DecidedAt,

                OriginalDeposit = deposit,

                EstimatedDeductions = estimatedDeductions,

                RecommendedRefundAmount = recommendedRefund,

                BankName = bankName,

                BankAccountNumber = accNumber,

                BankAccountHolder = holder,

                PayoutMethod = r.Provider

            };

        }).ToList();



        return result;

    }



    public async Task<ManagerRefundDecisionDto> DecideRefundApprovalAsync(

        long id,

        ManagerDecideRefundApprovalRequest request,

        long currentUserId,

        bool isSystemAdmin,

        CancellationToken cancellationToken = default)

    {

        var refund = await _dbContext.Refunds

            .Include(r => r.Agreement)

            .Include(r => r.Agreement)

                .ThenInclude(a => a!.Invoices)

                    .ThenInclude(i => i.InvoiceLines)

            .Include(r => r.RefundApproval)

            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);



        if (refund == null)

        {

            throw AppException.FromError(ManagerErrors.RefundNotFound);

        }



        if (refund.Agreement != null)

        {

            await EnsureFacilityAccessAsync(refund.Agreement.FacilityId, currentUserId, isSystemAdmin, cancellationToken);

        }



        if (refund.RefundApproval != null)

        {

            throw AppException.FromError(ManagerErrors.RefundAlreadyReviewed);

        }



        var decision = request.Decision.Trim().ToLowerInvariant();

        if (!ApprovalDecisionConstants.All.Contains(decision))

        {

            throw AppException.FromError(ManagerErrors.InvalidDecision);

        }



        var managerEmployeeId = await GetManagerEmployeeIdAsync(currentUserId, cancellationToken);

        var now = DateTimeOffset.UtcNow;



        decimal approvedAmount = 0m;

        if (decision == ApprovalDecisionConstants.Approved)

        {

            if (request.OverrideAmount.HasValue && request.OverrideAmount.Value >= 0)

            {

                approvedAmount = request.OverrideAmount.Value;

            }

            else

            {

                // Formula: Deposit - Damages - Overdue

                var deposit = refund.Agreement?.DepositSnapshot ?? refund.Amount;

                var unpaidDamages = refund.Agreement?.Invoices

                    .Where(i => i.Status != InvoiceStatusConstants.Paid)

                    .SelectMany(i => i.InvoiceLines)

                    .Where(l => l.LineType == "damage" || l.LineType == "penalty")

                    .Sum(l => l.UnitPrice * l.Quantity) ?? 0m;



                var overdueDebt = refund.Agreement?.Invoices

                    .Where(i => i.Status == InvoiceStatusConstants.Overdue)

                    .Sum(i => i.TotalAmount - i.PaidAmount) ?? 0m;



                approvedAmount = Math.Max(0m, deposit - (unpaidDamages + overdueDebt));

            }



            refund.Amount = approvedAmount;

            refund.Status = RefundStatusConstants.Approved;

        }

        else

        {

            refund.Status = RefundStatusConstants.Rejected;

        }



        var approval = new RefundApproval

        {

            RefundId = refund.Id,

            Decision = decision,

            DecidedBy = managerEmployeeId,

            Reason = request.Reason?.Trim(),

            DecidedAt = now

        };

        _dbContext.RefundApprovals.Add(approval);



        await _dbContext.SaveChangesAsync(cancellationToken);



        return new ManagerRefundDecisionDto

        {

            RefundId = refund.Id,

            Decision = decision,

            ApprovedAmount = approvedAmount,

            DecidedAt = now

        };

    }
}
