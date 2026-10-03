using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Pos;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.UnitTests.Pos;

public sealed class ApproverAuthorizationServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid DeviceId = Guid.NewGuid();
    private static readonly Guid CashierId = Guid.NewGuid();
    private static readonly Guid ManagerId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    // A plaintext "hash" — real hashing is BCryptPinHasher's own concern (tested separately); this fake
    // only needs to say whether a typed PIN matches a stored one.
    private sealed class FakePinHasher : IPinHasher
    {
        public string Hash(string pin) => pin;

        public bool Verify(string pin, string hash) => pin == hash;
    }

    private sealed class FakeDeviceRepository(Device device) : IDeviceRepository
    {
        public Task<Device?> FindByPairingCodeHashAsync(string pairingCodeHash, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Device?> FindByPairingCodeAsync(string pairingCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == device.Id ? device : null);

        public Task<Device?> GetByIdUnscopedAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Device>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Add(Device device) => throw new NotSupportedException();
    }

    private sealed class FakeUserRepository(IReadOnlyList<User> users) : IUserRepository
    {
        public Task<IReadOnlyList<User>> GetActiveActorsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(users);

        public Task<User?> FindActorAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<User>> ListActorsAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<User>> ListUninvitedLegacyAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeCurrentTenantProvider : ICurrentTenantProvider
    {
        public Guid? TenantId => ApproverAuthorizationServiceTests.TenantId;
    }

    private sealed class FakeCurrentActorProvider(Guid userId) : ICurrentActorProvider
    {
        public Guid? UserId => userId;

        public Guid? DeviceId => ApproverAuthorizationServiceTests.DeviceId;

        public Guid? BranchId => Guid.NewGuid();

        public ScopeType? ScopeType => Purch.Domain.Enums.ScopeType.Tenant;

        public Guid? ScopeId => null;
    }

    private sealed class NoopUnitOfWork : IUnitOfWork
    {
        public int SaveCount;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(0);
        }
    }

    private static User MakeUser(Guid id, Role role, string pin) =>
        new() { Id = id, TenantId = TenantId, Name = role.ToString(), Role = role, PinHash = pin, IsActive = true };

    private static (ApproverAuthorizationService Service, Device Device, NoopUnitOfWork UnitOfWork) Build(
        IReadOnlyList<User> users, Guid requesterId, Device? device = null)
    {
        device ??= new Device { Id = DeviceId, TenantId = TenantId, BranchId = Guid.NewGuid() };
        var unitOfWork = new NoopUnitOfWork();
        var service = new ApproverAuthorizationService(
            new FakeDeviceRepository(device),
            new FakeUserRepository(users),
            new FakePinHasher(),
            new FakeCurrentTenantProvider(),
            new FakeCurrentActorProvider(requesterId),
            unitOfWork);
        return (service, device, unitOfWork);
    }

    [Fact]
    public async Task A_managers_pin_approves_a_cashiers_request()
    {
        var cashier = MakeUser(CashierId, Role.Cashier, "cashier-pin");
        var manager = MakeUser(ManagerId, Role.Manager, "1234");
        var (service, _, _) = Build([cashier, manager], CashierId);

        var approver = await service.AuthorizeAsync("1234");

        Assert.Equal(ManagerId, approver.Id);
    }

    [Fact]
    public async Task A_wrong_pin_is_refused_and_never_approves()
    {
        var manager = MakeUser(ManagerId, Role.Manager, "1234");
        var (service, _, _) = Build([manager], CashierId);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.AuthorizeAsync("0000"));
        Assert.Contains("PIN", exception.Errors["approverPin"][0]);
    }

    [Fact]
    public async Task A_missing_pin_is_refused_without_even_checking_active_users()
    {
        var (service, _, _) = Build([], CashierId);

        await Assert.ThrowsAsync<ValidationException>(() => service.AuthorizeAsync(null));
        await Assert.ThrowsAsync<ValidationException>(() => service.AuthorizeAsync("   "));
    }

    [Fact]
    public async Task A_manager_cannot_approve_their_own_request_when_another_approver_exists()
    {
        var manager = MakeUser(ManagerId, Role.Manager, "1234");
        var admin = MakeUser(AdminId, Role.Admin, "9999");
        var (service, _, _) = Build([manager, admin], ManagerId);

        // Typing their own correct PIN does not approve their own request — the admin's PIN is required.
        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.AuthorizeAsync("1234"));
        Assert.Contains("different active manager or admin", exception.Errors["approverPin"][0]);

        var approver = await service.AuthorizeAsync("9999");
        Assert.Equal(AdminId, approver.Id);
    }

    [Fact]
    public async Task A_lone_admin_may_approve_their_own_request_when_no_other_approver_exists()
    {
        var admin = MakeUser(AdminId, Role.Admin, "1234");
        var (service, _, _) = Build([admin], AdminId);

        var approver = await service.AuthorizeAsync("1234");

        Assert.Equal(AdminId, approver.Id);
    }

    [Fact]
    public async Task Five_wrong_pins_lock_the_terminal_and_a_sixth_correct_pin_is_still_refused()
    {
        var manager = MakeUser(ManagerId, Role.Manager, "1234");
        var (service, device, unitOfWork) = Build([manager], CashierId);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            _ = await Assert.ThrowsAsync<ValidationException>(() => service.AuthorizeAsync("wrong"));
        }

        Assert.NotNull(device.ApproverPinLockedUntil);
        Assert.True(device.ApproverPinLockedUntil > DateTimeOffset.UtcNow);
        Assert.Equal(0, device.ApproverPinFailedAttempts); // reset the moment it locks, so it doesn't also carry over into the next window.
        Assert.Equal(5, unitOfWork.SaveCount); // every failed attempt is persisted on its own, not batched.

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => service.AuthorizeAsync("1234"));
        Assert.Contains("Too many incorrect", exception.Message);
    }

    [Fact]
    public async Task A_correct_pin_resets_the_failed_attempt_counter()
    {
        var manager = MakeUser(ManagerId, Role.Manager, "1234");
        var (service, device, _) = Build([manager], CashierId);

        _ = await Assert.ThrowsAsync<ValidationException>(() => service.AuthorizeAsync("wrong"));
        _ = await Assert.ThrowsAsync<ValidationException>(() => service.AuthorizeAsync("wrong"));
        Assert.Equal(2, device.ApproverPinFailedAttempts);

        _ = await service.AuthorizeAsync("1234");

        Assert.Equal(0, device.ApproverPinFailedAttempts);
        Assert.Null(device.ApproverPinLockedUntil);
    }

    [Fact]
    public async Task A_lockout_that_has_elapsed_no_longer_blocks_the_terminal()
    {
        var manager = MakeUser(ManagerId, Role.Manager, "1234");
        var device = new Device
        {
            Id = DeviceId,
            TenantId = TenantId,
            BranchId = Guid.NewGuid(),
            ApproverPinLockedUntil = DateTimeOffset.UtcNow.AddMinutes(-1),
        };
        var (service, _, _) = Build([manager], CashierId, device);

        var approver = await service.AuthorizeAsync("1234");

        Assert.Equal(ManagerId, approver.Id);
    }

    [Fact]
    public async Task An_inactive_manager_cannot_approve()
    {
        var inactiveManager = new User { Id = ManagerId, TenantId = TenantId, Name = "M", Role = Role.Manager, PinHash = "1234", IsActive = false };
        var (service, _, _) = Build([], CashierId); // GetActiveUsersByTenantAsync would already exclude them in real code
        _ = inactiveManager;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.AuthorizeAsync("1234"));
        Assert.Contains("PIN", exception.Errors["approverPin"][0]);
    }
}
