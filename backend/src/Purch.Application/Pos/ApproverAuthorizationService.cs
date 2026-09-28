using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Pos;

public sealed class ApproverAuthorizationService(
    IDeviceRepository deviceRepository,
    IUserRepository userRepository,
    IPinHasher pinHasher,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : IApproverAuthorizationService
{
    private static readonly HashSet<Role> ApproverRoles = [Role.Admin, Role.Manager];

    /// <summary>Wrong PINs allowed at one terminal before it locks — enough that a genuine mistyped PIN
    /// isn't punished, tight enough that guessing is not a viable attack.</summary>
    private const int MaxFailedAttempts = 5;

    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    public async Task<User> AuthorizeAsync(string? pin, CancellationToken cancellationToken = default)
    {
        var deviceId = currentActorProvider.DeviceId
            ?? throw new ForbiddenException("Approving this requires an authenticated device.");
        var device = await deviceRepository.GetByIdAsync(deviceId, cancellationToken)
            ?? throw new ForbiddenException("Approving this requires an authenticated device.");

        var now = DateTimeOffset.UtcNow;
        if (device.ApproverPinLockedUntil is { } lockedUntil && lockedUntil > now)
        {
            var minutesLeft = Math.Max(1, (int)Math.Ceiling((lockedUntil - now).TotalMinutes));
            throw new ForbiddenException(
                $"Too many incorrect approver PINs at this terminal. Try again in about {minutesLeft} minute{(minutesLeft == 1 ? "" : "s")}.");
        }

        if (string.IsNullOrWhiteSpace(pin))
        {
            throw new ValidationException("approverPin", "An Admin or Manager PIN is required to approve this.");
        }

        var requesterId = currentActorProvider.UserId;
        var activeUsers = await userRepository.GetActiveUsersByTenantAsync(currentTenantProvider.TenantId!.Value, cancellationToken);
        var eligibleApprovers = activeUsers.Where(user => ApproverRoles.Contains(user.Role)).ToList();

        // A different account approves whenever one exists — that is the whole point of requiring a PIN at
        // all. A tenant with exactly one Admin/Manager account (a single-owner shop, or a fresh tenant that
        // hasn't hired a second manager yet) has nobody else who ever could, so that one account may approve
        // its own request rather than being unable to void or refund anything at all.
        var otherApprovers = eligibleApprovers.Where(user => user.Id != requesterId).ToList();
        var candidates = otherApprovers.Count > 0 ? otherApprovers : eligibleApprovers;
        var approver = candidates.FirstOrDefault(user => pinHasher.Verify(pin, user.PinHash));

        if (approver is null)
        {
            await RecordFailedAttemptAsync(device, now, cancellationToken);
            throw new ValidationException("approverPin", "That PIN doesn't match a different active manager or admin.");
        }

        device.ApproverPinFailedAttempts = 0;
        device.ApproverPinLockedUntil = null;
        return approver;
    }

    /// <summary>
    /// Persisted on its own, separately from whatever the caller's own action was doing, so a failed PIN is
    /// never lost to a later rollback or exception — the whole point of counting attempts is that they
    /// survive the failure they came from.
    /// </summary>
    private async Task RecordFailedAttemptAsync(Device device, DateTimeOffset now, CancellationToken cancellationToken)
    {
        device.ApproverPinFailedAttempts += 1;
        if (device.ApproverPinFailedAttempts >= MaxFailedAttempts)
        {
            device.ApproverPinLockedUntil = now + LockoutDuration;
            device.ApproverPinFailedAttempts = 0;
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
