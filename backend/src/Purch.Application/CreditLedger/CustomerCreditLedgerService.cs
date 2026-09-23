using System.Text.Json;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.CreditLedger;

public sealed class CustomerCreditLedgerService(
    ICustomerCreditLedgerRepository creditLedgerRepository,
    ITenantRepository tenantRepository,
    IAuditLogRepository auditLogRepository,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : ICustomerCreditLedgerService
{
    public async Task<CustomerCreditLedgerDto> CreateAsync(CreateCustomerCreditLedgerRequest request, CancellationToken cancellationToken = default)
    {
        await RequireCreditLedgerEnabledAsync(cancellationToken);
        Validate(request);

        var ledger = new CustomerCreditLedger
        {
            TenantId = CurrentTenantId,
            CustomerFullName = request.CustomerFullName.Trim(),
            CustomerPhoneNumber = request.CustomerPhoneNumber.Trim(),
            CustomerAddress = string.IsNullOrWhiteSpace(request.CustomerAddress) ? null : request.CustomerAddress.Trim(),
            Balance = 0,
            CreditLimit = request.CreditLimit,
            DueDate = request.DueDate,
            IsActive = true,
        };

        creditLedgerRepository.Add(ledger);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(ledger);
    }

    public async Task<IReadOnlyList<CustomerCreditLedgerDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var ledgers = await creditLedgerRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        return [.. ledgers.Select(ToDto)];
    }

    public async Task<CustomerCreditLedgerDto> RecordPaymentAsync(Guid ledgerId, RecordCreditPaymentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            throw new ValidationException(nameof(request.Amount), "Payment amount must be greater than zero.");
        }

        var ledger = await creditLedgerRepository.GetByIdAsync(ledgerId, cancellationToken);
        if (ledger is null || ledger.TenantId != CurrentTenantId)
        {
            throw new NotFoundException("Customer credit account", ledgerId);
        }

        if (request.Amount > ledger.Balance)
        {
            throw new ValidationException(nameof(request.Amount), "Payment amount can't exceed the customer's outstanding balance.");
        }

        ledger.Balance -= request.Amount;
        creditLedgerRepository.AddTransaction(new CreditTransaction
        {
            TenantId = CurrentTenantId,
            CustomerCreditLedgerId = ledger.Id,
            Amount = -request.Amount,
            Note = request.Note,
        });

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(ledger);
    }

    public async Task<IReadOnlyList<CreditReminderDto>> ListRemindersAsync(int withinDays, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var lookahead = today.AddDays(Math.Max(withinDays, 0));

        var ledgers = await creditLedgerRepository.ListDueOnOrBeforeAsync(CurrentTenantId, lookahead, cancellationToken);

        return [.. ledgers
            .Where(ledger => ledger.Balance > 0 && ledger.DueDate is not null)
            .Select(ledger => new CreditReminderDto(
                ledger.Id,
                ledger.CustomerFullName,
                ledger.CustomerPhoneNumber,
                ledger.Balance,
                ledger.DueDate!.Value,
                ledger.DueDate!.Value < today))
            .OrderBy(dto => dto.DueDate)];
    }

    private async Task RequireCreditLedgerEnabledAsync(CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(CurrentTenantId, cancellationToken);
        if (tenant is null || !tenant.CreditLedgerEnabled)
        {
            throw new ValidationException("creditLedgerEnabled", "Utang/credit sales aren't enabled for this business yet.");
        }
    }

    private static void Validate(CreateCustomerCreditLedgerRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.CustomerFullName))
        {
            errors[nameof(request.CustomerFullName)] = ["Customer name is required."];
        }

        if (string.IsNullOrWhiteSpace(request.CustomerPhoneNumber))
        {
            errors[nameof(request.CustomerPhoneNumber)] = ["Customer phone number is required."];
        }

        if (request.CreditLimit < 0)
        {
            errors[nameof(request.CreditLimit)] = ["Credit limit can't be negative."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private static CustomerCreditLedgerDto ToDto(CustomerCreditLedger ledger)
    {
        return new CustomerCreditLedgerDto(
            ledger.Id,
            ledger.CustomerFullName,
            ledger.CustomerPhoneNumber,
            ledger.CustomerAddress,
            ledger.Balance,
            ledger.CreditLimit,
            ledger.DueDate,
            ledger.IsActive);
    }

    public async Task<CustomerCreditLedgerDto> UpdateCreditLimitAsync(Guid ledgerId, UpdateCreditLimitRequest request, CancellationToken cancellationToken = default)
    {
        if (request.CreditLimit < 0)
        {
            throw new ValidationException(nameof(request.CreditLimit), "Credit limit can't be negative.");
        }

        var ledger = await creditLedgerRepository.GetByIdAsync(ledgerId, cancellationToken);
        if (ledger is null || ledger.TenantId != CurrentTenantId)
        {
            throw new NotFoundException("Customer credit account", ledgerId);
        }

        if (ledger.CreditLimit != request.CreditLimit)
        {
            auditLogRepository.Add(new AuditLog
            {
                TenantId = CurrentTenantId,
                ActorUserId = CurrentUserId,
                ActionType = AuditActionType.CreditLimitOverride,
                TargetEntityType = nameof(CustomerCreditLedger),
                TargetEntityId = ledger.Id,
                BeforeStateJson = JsonSerializer.Serialize(new { creditLimit = ledger.CreditLimit }),
                AfterStateJson = JsonSerializer.Serialize(new { creditLimit = request.CreditLimit, reason = request.Reason }),
            });
        }

        ledger.CreditLimit = request.CreditLimit;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(ledger);
    }

    public async Task<CustomerCreditLedgerDto> AnonymizeCustomerAsync(Guid ledgerId, CancellationToken cancellationToken = default)
    {
        var ledger = await creditLedgerRepository.GetByIdAsync(ledgerId, cancellationToken);
        if (ledger is null || ledger.TenantId != CurrentTenantId)
        {
            throw new NotFoundException("Customer credit account", ledgerId);
        }

        if (ledger.Balance != 0)
        {
            throw new ValidationException(nameof(ledger.Balance), "Cannot anonymize customer with an outstanding balance. Balance must be zero before erasure.");
        }

        auditLogRepository.Add(new AuditLog
        {
            TenantId = CurrentTenantId,
            ActorUserId = CurrentUserId,
            ActionType = AuditActionType.CustomerAnonymized,
            TargetEntityType = nameof(CustomerCreditLedger),
            TargetEntityId = ledger.Id,
            BeforeStateJson = JsonSerializer.Serialize(new { name = ledger.CustomerFullName, phone = ledger.CustomerPhoneNumber, address = ledger.CustomerAddress }),
            AfterStateJson = JsonSerializer.Serialize(new { name = "[ANONYMIZED]", phone = "00000000000", address = (string?)null, isActive = false }),
        });

        ledger.CustomerFullName = "[ANONYMIZED]";
        ledger.CustomerPhoneNumber = "00000000000";
        ledger.CustomerAddress = null;
        ledger.IsActive = false;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(ledger);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("The credit ledger requires an authenticated tenant context.");

    private Guid CurrentUserId => currentActorProvider.UserId
        ?? throw new InvalidOperationException("The credit ledger requires an authenticated user.");
}
