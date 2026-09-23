using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

public sealed class StaffService(
    IUserRepository userRepository,
    IPinHasher pinHasher,
    IRefreshTokenService refreshTokenService,
    IAuditLogRepository auditLogRepository,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : IStaffService
{
    public async Task<IReadOnlyList<StaffDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var users = await userRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        return [.. users.Select(ToDto)];
    }

    public async Task<StaffDto> CreateAsync(CreateStaffRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Name is required.");
        }

        if (PinPolicy.Validate(request.Pin) is { } pinError)
        {
            throw new ValidationException(nameof(request.Pin), pinError);
        }

        // Login finds a user by PIN alone (the device already narrows it to one tenant), so two
        // active staff sharing a PIN would be indistinguishable — one could sign in as the other.
        var activeUsers = await userRepository.GetActiveUsersByTenantAsync(CurrentTenantId, cancellationToken);
        if (activeUsers.Any(existing => pinHasher.Verify(request.Pin, existing.PinHash)))
        {
            throw new ValidationException(nameof(request.Pin), "That PIN is already in use by another staff member. Choose a different one.");
        }

        var user = new User
        {
            TenantId = CurrentTenantId,
            Name = request.Name.Trim(),
            Role = request.Role,
            ScopeType = request.ScopeType,
            ScopeId = request.ScopeId,
            BranchId = request.BranchId,
            PinHash = pinHasher.Hash(request.Pin),
            IsActive = true,
        };

        userRepository.Add(user);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(user);
    }

    public async Task<StaffDto> UpdateAsync(Guid staffId, UpdateStaffRequest request, CancellationToken cancellationToken = default)
    {
        // GetByIdAsync goes through PurchDbContext's tenant query filter, so a staffId
        // belonging to another tenant naturally comes back null here — not a 403, a 404,
        // since from this tenant's perspective that id simply doesn't exist.
        var user = await userRepository.GetByIdAsync(staffId, cancellationToken)
            ?? throw new NotFoundException("Staff member", staffId);

        // Access tokens carry the role and scope they were issued with, so a session opened before this
        // change would keep its old authority. Ending the refresh tokens limits that to the access
        // token's remaining lifetime (30 minutes) instead of the refresh token's (30 days).
        var authorityChanged = user.Role != request.Role
            || user.ScopeType != request.ScopeType
            || user.ScopeId != request.ScopeId
            || user.BranchId != request.BranchId
            || user.IsActive != request.IsActive;

        if (authorityChanged)
        {
            // Staged before the fields change, so Before/After capture the actual transition — who
            // could act as what, before and after this edit — for A6's audit log viewer.
            auditLogRepository.Add(new AuditLog
            {
                TenantId = CurrentTenantId,
                ActorUserId = CurrentUserId,
                ActionType = AuditActionType.StaffAccessChanged,
                TargetEntityType = nameof(User),
                TargetEntityId = user.Id,
                BeforeStateJson = JsonSerializer.Serialize(new
                {
                    role = user.Role.ToString(),
                    scopeType = user.ScopeType.ToString(),
                    scopeId = user.ScopeId,
                    branchId = user.BranchId,
                    isActive = user.IsActive,
                }),
                AfterStateJson = JsonSerializer.Serialize(new
                {
                    role = request.Role.ToString(),
                    scopeType = request.ScopeType.ToString(),
                    scopeId = request.ScopeId,
                    branchId = request.BranchId,
                    isActive = request.IsActive,
                }),
            });
        }

        user.Role = request.Role;
        user.ScopeType = request.ScopeType;
        user.ScopeId = request.ScopeId;
        user.BranchId = request.BranchId;
        user.IsActive = request.IsActive;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        if (authorityChanged)
        {
            await refreshTokenService.RevokeAllForUserAsync(user.Id, cancellationToken);
        }

        return ToDto(user);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Staff management requires an authenticated tenant context.");

    private Guid CurrentUserId => currentActorProvider.UserId
        ?? throw new InvalidOperationException("Staff management requires an authenticated staff user.");

    private static StaffDto ToDto(User user)
    {
        return new(user.Id, user.Name, user.Role, user.ScopeType, user.ScopeId, user.BranchId, user.IsActive);
    }
}
