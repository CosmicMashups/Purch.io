using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Pos;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Shifts;

/// <summary>D7 — opening/closing a device's cash-drawer session and reconciling
/// the actual count against what cash sales say should be in the drawer.</summary>
public sealed class ShiftService(
    IShiftRepository shiftRepository,
    IPaymentRepository paymentRepository,
    IUserRepository userRepository,
    IPinHasher pinHasher,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : IShiftService
{
    private static readonly HashSet<Role> ApproverRoles = [Role.Admin, Role.Manager];

    public async Task<ShiftDto?> GetCurrentShiftAsync(CancellationToken cancellationToken = default)
    {
        var shift = await shiftRepository.GetOpenByDeviceAsync(CurrentDeviceId, cancellationToken);
        return shift is null ? null : await ToDtoAsync(shift, cancellationToken);
    }

    public async Task<ShiftDto> OpenShiftAsync(OpenShiftRequest request, CancellationToken cancellationToken = default)
    {
        if (request.OpeningCashAmount < 0)
        {
            throw new ValidationException(nameof(request.OpeningCashAmount), "Opening cash amount cannot be negative.");
        }

        var deviceId = CurrentDeviceId;
        var existing = await shiftRepository.GetOpenByDeviceAsync(deviceId, cancellationToken);
        if (existing is not null)
        {
            throw new ValidationException(nameof(request.OpeningCashAmount), "This device already has a shift open — close it before opening a new one.");
        }

        var shift = new Shift
        {
            TenantId = CurrentTenantId,
            BranchId = CurrentBranchId,
            DeviceId = deviceId,
            Status = ShiftStatus.Open,
            OpenedByUserId = CurrentUserId,
            OpeningCashAmount = request.OpeningCashAmount,
            OpenedAt = DateTimeOffset.UtcNow,
        };

        shiftRepository.Add(shift);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(shift, cancellationToken);
    }

    public async Task<ShiftDto> CloseShiftAsync(CloseShiftRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ClosingCashAmount < 0)
        {
            throw new ValidationException(nameof(request.ClosingCashAmount), "Closing cash amount cannot be negative.");
        }

        var deviceId = CurrentDeviceId;
        var shift = await shiftRepository.GetOpenByDeviceAsync(deviceId, cancellationToken)
            ?? throw new NotFoundException("Open shift", deviceId);

        var cashCollected = await paymentRepository.SumCashCollectedByDeviceSinceAsync(deviceId, shift.OpenedAt, cancellationToken);
        var expectedCashAmount = shift.OpeningCashAmount + cashCollected;
        var varianceAmount = request.ClosingCashAmount - expectedCashAmount;

        Guid? approvedByUserId = null;
        if (varianceAmount != 0)
        {
            if (string.IsNullOrWhiteSpace(request.ApproverPin))
            {
                throw new ValidationException(
                    nameof(request.ApproverPin),
                    "The cash count doesn't match the expected amount — a manager's PIN is required to approve the discrepancy.");
            }

            var activeUsers = await userRepository.GetActiveUsersByTenantAsync(CurrentTenantId, cancellationToken);
            var approver = activeUsers.FirstOrDefault(user => ApproverRoles.Contains(user.Role) && pinHasher.Verify(request.ApproverPin, user.PinHash))
                ?? throw new ValidationException(nameof(request.ApproverPin), "That PIN doesn't match an active manager or admin.");

            approvedByUserId = approver.Id;
        }

        shift.Status = ShiftStatus.Closed;
        shift.ClosedByUserId = CurrentUserId;
        shift.ClosingCashAmount = request.ClosingCashAmount;
        shift.ExpectedCashAmount = expectedCashAmount;
        shift.VarianceAmount = varianceAmount;
        shift.HandoverNotes = request.HandoverNotes;
        shift.ApprovedByUserId = approvedByUserId;
        shift.ClosedAt = DateTimeOffset.UtcNow;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(shift, cancellationToken);
    }

    private async Task<ShiftDto> ToDtoAsync(Shift shift, CancellationToken cancellationToken)
    {
        var openedByUser = await userRepository.GetByIdAsync(shift.OpenedByUserId, cancellationToken);
        var closedByUser = shift.ClosedByUserId is { } closedByUserId
            ? await userRepository.GetByIdAsync(closedByUserId, cancellationToken)
            : null;
        var approvedByUser = shift.ApprovedByUserId is { } approvedByUserId
            ? await userRepository.GetByIdAsync(approvedByUserId, cancellationToken)
            : null;

        return new ShiftDto(
            shift.Id,
            shift.BranchId,
            shift.DeviceId,
            shift.Status,
            shift.OpenedByUserId,
            openedByUser?.Name ?? "(removed user)",
            shift.OpeningCashAmount,
            shift.OpenedAt,
            shift.ClosedByUserId,
            closedByUser?.Name,
            shift.ClosingCashAmount,
            shift.ExpectedCashAmount,
            shift.VarianceAmount,
            shift.HandoverNotes,
            shift.ApprovedByUserId,
            approvedByUser?.Name,
            shift.ClosedAt);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Shifts require an authenticated tenant context.");

    private Guid CurrentDeviceId => currentActorProvider.DeviceId
        ?? throw new InvalidOperationException("Shifts require an authenticated device context.");

    private Guid CurrentBranchId => currentActorProvider.BranchId
        ?? throw new InvalidOperationException("Shifts require an authenticated device's branch.");

    private Guid CurrentUserId => currentActorProvider.UserId
        ?? throw new InvalidOperationException("Shifts require an authenticated staff user.");
}
