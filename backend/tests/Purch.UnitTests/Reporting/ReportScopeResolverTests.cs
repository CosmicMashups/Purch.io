using Purch.Application.Common;
using Purch.Application.Onboarding;
using Purch.Application.Reporting;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.UnitTests.Reporting;

public sealed class ReportScopeResolverTests
{
    [Fact]
    public async Task A_tenant_scoped_actor_may_request_any_branch_or_none()
    {
        var actor = new FakeCurrentActorProvider(ScopeType.Tenant, null);
        var resolver = new ReportScopeResolver(actor, new FakeDepartmentRepository());

        var requestedBranchId = Guid.NewGuid();
        Assert.Equal(requestedBranchId, await resolver.ResolveBranchIdAsync(requestedBranchId));
        Assert.Null(await resolver.ResolveBranchIdAsync(null));
    }

    [Fact]
    public async Task A_branch_scoped_actor_is_always_restricted_to_their_own_branch()
    {
        var ownBranchId = Guid.NewGuid();
        var actor = new FakeCurrentActorProvider(ScopeType.Branch, ownBranchId);
        var resolver = new ReportScopeResolver(actor, new FakeDepartmentRepository());

        // Even a crafted request for a different branch is ignored.
        var resolved = await resolver.ResolveBranchIdAsync(Guid.NewGuid());

        Assert.Equal(ownBranchId, resolved);
    }

    [Fact]
    public async Task A_department_scoped_actor_resolves_to_their_departments_branch()
    {
        var departmentId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var actor = new FakeCurrentActorProvider(ScopeType.Department, departmentId);
        var departmentRepository = new FakeDepartmentRepository();
        departmentRepository.Departments[departmentId] = new Department { Id = departmentId, BranchId = branchId };
        var resolver = new ReportScopeResolver(actor, departmentRepository);

        var resolved = await resolver.ResolveBranchIdAsync(Guid.NewGuid());

        Assert.Equal(branchId, resolved);
    }

    private sealed class FakeCurrentActorProvider(ScopeType? scopeType, Guid? scopeId) : ICurrentActorProvider
    {
        public Guid? UserId => Guid.NewGuid();

        public Guid? DeviceId => Guid.NewGuid();

        public Guid? BranchId => null;

        public ScopeType? ScopeType => scopeType;

        public Guid? ScopeId => scopeId;
    }

    private sealed class FakeDepartmentRepository : IDepartmentRepository
    {
        public Dictionary<Guid, Department> Departments { get; } = [];

        public Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Departments.GetValueOrDefault(id));
        }

        public Task<IReadOnlyList<Department>> ListByBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Department>>([.. Departments.Values.Where(d => d.BranchId == branchId)]);
        }

        public Task<IReadOnlyList<Department>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Department>>([.. Departments.Values.Where(d => d.TenantId == tenantId)]);
        }

        public void Add(Department department)
        {
            Departments[department.Id] = department;
        }
    }
}
