using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.UnitTests.Inventory;

public sealed class StockTakeServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Branch = Guid.NewGuid();

    private readonly List<Item> items = [];
    private readonly List<InventoryItem> ingredients = [];
    private readonly FakeTakes takes = new();
    private readonly FakeMovements movements = new();
    private readonly FakeAudit audit = new();
    private readonly FakeApprover approver = new();
    private readonly FakeActor actor = new();

    private StockTakeService Service() => new(
        takes, new FakeItems(items), new FakeIngredients(ingredients), new FakeStock(), movements, audit, approver,
        new AllowAllBranches(), new FakeTenant(), actor, new FakeUnitOfWork());

    private Item AddItem(string name, decimal stock, Guid? categoryId = null, decimal? cost = 2m)
    {
        var item = new Item { TenantId = Tenant, Name = name, StockOnHand = stock, CategoryId = categoryId, CostPrice = cost, PricingType = PricingType.Unit };
        items.Add(item);
        return item;
    }

    private static async Task<StockTakeDto> CountingTakeAsync(StockTakeService service, bool blind = false)
    {
        var take = await service.CreateAsync(new CreateStockTakeRequest("Monthly", StockTakeScopeKind.All, blind, Branch));
        return await service.StartAsync(take.Id);
    }

    private static Guid LineOf(StockTakeDto take, Item item) => take.Lines.Single(l => l.ItemId == item.Id).Id;

    [Fact]
    public async Task Counting_snapshots_expected_at_the_moment_of_entry()
    {
        var rice = AddItem("Rice", 10m);
        var service = Service();
        var take = await CountingTakeAsync(service);

        var counted = await service.CountLineAsync(take.Id, LineOf(take, rice), 8m, false);

        var line = counted.Lines.Single();
        Assert.Equal(StockTakeLineState.Counted, line.State);
        Assert.Equal(10m, line.ExpectedQty);
        Assert.Equal(8m, line.CountedQty);
        Assert.Equal(-2m, line.VarianceQty);
    }

    [Fact]
    public async Task Recounting_re_snapshots_expected()
    {
        var rice = AddItem("Rice", 10m);
        var service = Service();
        var take = await CountingTakeAsync(service);
        _ = await service.CountLineAsync(take.Id, LineOf(take, rice), 8m, false);

        rice.StockOnHand = 12m; // a delivery landed between the two counts
        var recounted = await service.CountLineAsync(take.Id, LineOf(take, rice), 8m, false);

        Assert.Equal(12m, recounted.Lines.Single().ExpectedQty);
        Assert.Equal(-4m, recounted.Lines.Single().VarianceQty);
    }

    [Fact]
    public async Task A_sale_after_the_count_survives_apply_which_posts_only_the_variance()
    {
        var rice = AddItem("Rice", 10m);
        var service = Service();
        var take = await CountingTakeAsync(service);
        _ = await service.CountLineAsync(take.Id, LineOf(take, rice), 8m, false);

        rice.StockOnHand -= 3m; // sold three after the shelf was counted
        _ = await service.ToReviewAsync(take.Id);
        var applied = await service.ApplyAsync(take.Id, "1234");

        var movement = Assert.Single(movements.Added);
        Assert.Equal(MovementType.Adjustment, movement.Type);
        Assert.Equal(-2m, movement.Quantity);
        Assert.Equal(Branch, movement.BranchId);
        Assert.Equal(10m - 3m - 2m, rice.StockOnHand);
        Assert.Equal(StockTakeStatus.Applied, applied.Status);
        Assert.Equal(approver.Approver.Id, applied.ApprovedByUserId);
        Assert.Equal(movement.Id, applied.Lines.Single().MovementId);
    }

    [Fact]
    public async Task Uncounted_and_skipped_lines_post_nothing_and_are_listed_in_the_audit_entry()
    {
        var counted = AddItem("Counted", 10m);
        var skipped = AddItem("Skipped", 10m);
        var untouched = AddItem("Untouched", 10m);
        var service = Service();
        var take = await CountingTakeAsync(service);
        _ = await service.CountLineAsync(take.Id, LineOf(take, counted), 7m, false);
        _ = await service.CountLineAsync(take.Id, LineOf(take, skipped), null, true);
        _ = await service.ToReviewAsync(take.Id);

        _ = await service.ApplyAsync(take.Id, "1234");

        Assert.Equal(counted.Id, Assert.Single(movements.Added).ItemId);
        Assert.Equal(10m, skipped.StockOnHand);
        Assert.Equal(10m, untouched.StockOnHand);
        var entry = Assert.Single(audit.Added);
        Assert.Equal(AuditActionType.StockTakeApplied, entry.ActionType);
        Assert.Equal(approver.Approver.Id, entry.ApprovedByUserId);
        Assert.Contains("Untouched", entry.AfterStateJson);
        Assert.Contains("Skipped", entry.AfterStateJson);
    }

    [Fact]
    public async Task Apply_is_blocked_when_no_line_is_counted()
    {
        _ = AddItem("Rice", 10m);
        var service = Service();
        var take = await CountingTakeAsync(service);
        _ = await service.ToReviewAsync(take.Id);

        _ = await Assert.ThrowsAsync<ValidationException>(() => service.ApplyAsync(take.Id, "1234"));
        Assert.Equal(0, approver.Calls);
        Assert.Empty(movements.Added);
    }

    [Fact]
    public async Task A_second_apply_returns_the_first_result_without_posting_again()
    {
        var rice = AddItem("Rice", 10m);
        var service = Service();
        var take = await CountingTakeAsync(service);
        _ = await service.CountLineAsync(take.Id, LineOf(take, rice), 8m, false);
        _ = await service.ToReviewAsync(take.Id);

        var first = await service.ApplyAsync(take.Id, "1234");
        var second = await service.ApplyAsync(take.Id, "1234");

        _ = Assert.Single(movements.Added);
        Assert.Equal(8m, rice.StockOnHand);
        Assert.Equal(first.AppliedAt, second.AppliedAt);
        Assert.Equal(first.Lines.Single().MovementId, second.Lines.Single().MovementId);
        Assert.Equal(1, approver.Calls);
    }

    [Fact]
    public async Task Blind_mode_hides_expected_only_while_counting()
    {
        var rice = AddItem("Rice", 10m);
        var service = Service();
        var take = await CountingTakeAsync(service, blind: true);

        var counting = await service.CountLineAsync(take.Id, LineOf(take, rice), 8m, false);
        Assert.Null(counting.Lines.Single().ExpectedQty);
        Assert.Null(counting.Lines.Single().VarianceQty);
        Assert.Equal(8m, counting.Lines.Single().CountedQty);

        var review = await service.ToReviewAsync(take.Id);
        Assert.Equal(10m, review.Lines.Single().ExpectedQty);
        Assert.Equal(-2m, review.Lines.Single().VarianceQty);
    }

    [Fact]
    public async Task Categories_scope_materializes_only_those_categories()
    {
        var drinks = Guid.NewGuid();
        var snacks = Guid.NewGuid();
        var cola = AddItem("Cola", 5m, drinks);
        _ = AddItem("Chips", 5m, snacks);
        _ = AddItem("Loose", 5m);
        ingredients.Add(new InventoryItem { TenantId = Tenant, Name = "Syrup", QuantityOnHand = 3m, CategoryId = drinks });
        ingredients.Add(new InventoryItem { TenantId = Tenant, Name = "Salt", QuantityOnHand = 3m, CategoryId = snacks });

        var take = await Service().CreateAsync(new CreateStockTakeRequest("Drinks", StockTakeScopeKind.Categories, false, Branch, [drinks]));

        Assert.Equal(StockTakeStatus.Draft, take.Status);
        Assert.Equal(["Cola", "Syrup"], take.Lines.Select(l => l.Name).Order().ToList());
        Assert.Contains(take.Lines, l => l.ItemId == cola.Id);
        Assert.All(take.Lines, l => Assert.Null(l.ExpectedQty));
    }

    [Fact]
    public async Task Costs_are_hidden_from_roles_that_cannot_see_them()
    {
        var rice = AddItem("Rice", 10m, cost: 5m);
        var service = Service();
        var take = await CountingTakeAsync(service);
        _ = await service.CountLineAsync(take.Id, LineOf(take, rice), 8m, false);

        Assert.Equal(-10m, (await service.GetAsync(take.Id)).Lines.Single().VarianceValue);
        actor.CurrentRole = Role.Cashier;
        var line = (await service.GetAsync(take.Id)).Lines.Single();
        Assert.Null(line.UnitCostSnapshot);
        Assert.Null(line.VarianceValue);
    }

    private void Sell(Item item, decimal quantity)
    {
        item.StockOnHand -= quantity;
        takes.History.Add(new InventoryMovement { TenantId = Tenant, ItemId = item.Id, BranchId = Branch, Type = MovementType.Sale, Quantity = quantity, CreatedAt = DateTimeOffset.UtcNow.AddSeconds(1) });
    }

    [Fact]
    public async Task A_sale_after_the_count_shows_as_movements_since_count_and_flags_a_recount_but_apply_posts_only_the_variance()
    {
        var rice = AddItem("Rice", 10m);
        var service = Service();
        var take = await CountingTakeAsync(service);
        _ = await service.CountLineAsync(take.Id, LineOf(take, rice), 8m, false);

        Sell(rice, 3m);
        var review = await service.ToReviewAsync(take.Id);

        var line = review.Lines.Single();
        Assert.Equal(-3m, line.MovementsSinceCount);
        Assert.True(line.NeedsRecount);
        _ = await service.ApplyAsync(take.Id, "1234");
        Assert.Equal(-2m, Assert.Single(movements.Added).Quantity);
        Assert.Equal(5m, rice.StockOnHand);
    }

    [Fact]
    public async Task Movement_within_the_threshold_is_shown_but_not_flagged()
    {
        var rice = AddItem("Rice", 10m);
        var service = Service();
        var take = await CountingTakeAsync(service);
        _ = await service.CountLineAsync(take.Id, LineOf(take, rice), 8m, false);

        Sell(rice, 1m); // max(5% of 10, 1) = 1, and only movement beyond it flags
        var line = (await service.ToReviewAsync(take.Id)).Lines.Single();

        Assert.Equal(-1m, line.MovementsSinceCount);
        Assert.False(line.NeedsRecount);
    }

    [Fact]
    public async Task Movements_since_count_stay_hidden_while_a_blind_take_is_counting()
    {
        var rice = AddItem("Rice", 10m);
        var service = Service();
        var take = await CountingTakeAsync(service, blind: true);
        _ = await service.CountLineAsync(take.Id, LineOf(take, rice), 8m, false);
        Sell(rice, 3m);

        var line = (await service.GetAsync(take.Id)).Lines.Single();
        Assert.Null(line.MovementsSinceCount);
        Assert.False(line.NeedsRecount);
    }

    [Fact]
    public async Task Only_a_manager_or_admin_may_change_counts_on_a_blind_take_in_review()
    {
        var rice = AddItem("Rice", 10m);
        var service = Service();
        var take = await CountingTakeAsync(service, blind: true);
        _ = await service.CountLineAsync(take.Id, LineOf(take, rice), 8m, false);
        _ = await service.ToReviewAsync(take.Id);

        actor.CurrentRole = Role.Warehouse;
        _ = await Assert.ThrowsAsync<ForbiddenException>(() => service.CountLineAsync(take.Id, LineOf(take, rice), 10m, false));
        actor.CurrentRole = Role.Manager;
        Assert.Equal(10m, (await service.CountLineAsync(take.Id, LineOf(take, rice), 10m, false)).Lines.Single().CountedQty);
    }

    [Fact]
    public async Task Counts_are_refused_once_the_take_is_applied_or_the_item_is_deleted()
    {
        var rice = AddItem("Rice", 10m);
        var gone = AddItem("Gone", 4m);
        var service = Service();
        var take = await CountingTakeAsync(service);
        gone.IsDeleted = true;
        _ = await Assert.ThrowsAsync<ValidationException>(() => service.CountLineAsync(take.Id, LineOf(take, gone), 1m, false));

        _ = await service.CountLineAsync(take.Id, LineOf(take, rice), 8m, false);
        _ = await service.ToReviewAsync(take.Id);
        _ = await service.ApplyAsync(take.Id, "1234");
        _ = await Assert.ThrowsAsync<ConflictException>(() => service.CountLineAsync(take.Id, LineOf(take, rice), 3m, false));
    }

    [Fact]
    public async Task Apply_lists_lines_whose_stock_ends_negative_without_refusing()
    {
        var rice = AddItem("Rice", 5m);
        var service = Service();
        var take = await CountingTakeAsync(service);
        _ = await service.CountLineAsync(take.Id, LineOf(take, rice), 1m, false); // variance -4
        Sell(rice, 3m); // 2 left, apply takes it to -2
        _ = await service.ToReviewAsync(take.Id);

        _ = await service.ApplyAsync(take.Id, "1234");

        Assert.Equal(-2m, rice.StockOnHand);
        Assert.Contains("\"negativeStockLines\":[{", Assert.Single(audit.Added).AfterStateJson);
    }

    private sealed class FakeTenant : ICurrentTenantProvider
    {
        public Guid? TenantId => Tenant;
    }

    private sealed class FakeActor : ICurrentActorProvider
    {
        public Role CurrentRole { get; set; } = Domain.Enums.Role.Manager;
        public Guid? UserId { get; } = Guid.NewGuid();
        public Guid? DeviceId { get; } = Guid.NewGuid();
        public Guid? BranchId => Branch;
        public ScopeType? ScopeType => Domain.Enums.ScopeType.Tenant;
        public Guid? ScopeId => null;
        public Role? Role => CurrentRole;
    }

    private sealed class AllowAllBranches : IBranchScopeGuard
    {
        public Task EnsureAllowedAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task EnsureAnyAllowedAsync(IReadOnlyCollection<Guid> branchIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeApprover : IApproverAuthorizationService
    {
        public User Approver { get; } = new() { TenantId = Tenant, Name = "Mae", Role = Role.Manager };
        public int Calls { get; private set; }

        public Task<User> AuthorizeAsync(string? pin, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Approver);
        }
    }

    private sealed class FakeTakes : IStockTakeRepository
    {
        private readonly List<StockTake> stored = [];

        public List<InventoryMovement> History { get; } = [];

        public Task<StockTake?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(stored.FirstOrDefault(t => t.Id == id));

        public Task<StockTake?> GetUntrackedAsync(Guid id, CancellationToken cancellationToken = default) => GetAsync(id, cancellationToken);

        public Task<IReadOnlyList<StockTakeSummaryDto>> ListSummariesAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<InventoryMovement>> ListMovementsSinceAsync(Guid tenantId, IReadOnlyCollection<Guid> itemIds, IReadOnlyCollection<Guid> inventoryItemIds, DateTimeOffset since, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InventoryMovement>>(History.Where(m => m.CreatedAt > since
                && ((m.ItemId is { } i && itemIds.Contains(i)) || (m.InventoryItemId is { } g && inventoryItemIds.Contains(g)))).ToList());

        public Task<IReadOnlyList<(Guid? ItemId, Guid? InventoryItemId)>> ListOpenLineTargetsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<(Guid?, Guid?)>>(stored
                .Where(t => t.Status is StockTakeStatus.Draft or StockTakeStatus.Counting or StockTakeStatus.Review)
                .SelectMany(t => t.Lines).Select(l => (l.ItemId, l.InventoryItemId)).ToList());

        public void Add(StockTake stockTake) => stored.Add(stockTake);
    }

    private sealed class FakeMovements : IInventoryMovementRepository
    {
        public List<InventoryMovement> Added { get; } = [];

        public Task<IReadOnlyList<InventoryMovement>> ListAsync(Guid tenantId, Guid? itemId, Guid? branchId, MovementType? type, DateTimeOffset? before = null, int? limit = null, Guid? beforeId = null, Guid? inventoryItemId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(InventoryMovement movement) => Added.Add(movement);
    }

    private sealed class FakeAudit : IAuditLogRepository
    {
        public List<AuditLog> Added { get; } = [];

        public void Add(AuditLog log) => Added.Add(log);

        public Task<IReadOnlyList<AuditLog>> QueryAsync(Guid tenantId, AuditLogQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<AuditLog>> ListApprovalsAsync(Guid tenantId, DateTimeOffset fromUtc, DateTimeOffset toExclusiveUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeStock : IItemStockService
    {
        public Task<Guid?> AdjustAsync(Item item, decimal delta, CancellationToken cancellationToken = default)
        {
            item.StockOnHand += delta;
            return Task.FromResult<Guid?>(null);
        }

        public Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandAsync(IReadOnlyCollection<Item> items, Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(items.ToDictionary(i => i.Id, i => i.StockOnHand));
    }

    private sealed class FakeItems(List<Item> items) : IItemRepository
    {
        public Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(items.FirstOrDefault(i => i.Id == id));

        public Task<IReadOnlyList<Item>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Item>>(items.Where(i => ids.Contains(i.Id)).ToList());

        public Task<IReadOnlyList<Item>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Item>>(items);

        public Task<bool> BarcodeExistsAsync(Guid tenantId, string barcode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string?> GetDeletedNameByBarcodeAsync(Guid tenantId, string barcode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Add(Item item) => throw new NotSupportedException();
    }

    private sealed class FakeIngredients(List<InventoryItem> ingredients) : IInventoryItemRepository
    {
        public Task<IReadOnlyList<InventoryItem>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventoryItem>>(ingredients);

        public Task<InventoryItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(ingredients.FirstOrDefault(i => i.Id == id));

        public Task<IReadOnlyList<InventoryItem>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventoryItem>>(ingredients.Where(i => ids.Contains(i.Id)).ToList());

        public Task<InventoryItem?> GetByLinkedItemIdAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<InventoryItem>> ListByCategoryTrackedAsync(Guid categoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Add(InventoryItem inventoryItem) => throw new NotSupportedException();
    }
}
