using System.Text;
using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Inventory;
using Purch.Application.Inventory.Import;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.UnitTests.Inventory.Import;

public sealed class ImportServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Branch = Guid.NewGuid();
    private const string CatalogHeader = "type,sku,barcode,name,category,price,cost,reorder_threshold,unit,active,opening_stock";

    private readonly Store store = new();
    private readonly FakeApprover approver = new();
    private readonly FakeActor actor = new();
    private readonly FakeUnitOfWork unitOfWork;

    public ImportServiceTests()
    {
        unitOfWork = new FakeUnitOfWork(store);
    }

    private ImportService Service() => new(
        store.Batches,
        new FakeItems(store),
        new FakeIngredients(store),
        new FakeCategories(store),
        new FakeInventoryCategories(store),
        new FakeStock(),
        new FakeItemService(store),
        new FakeInventoryItemService(store),
        new FakeCategoryService(store),
        new FakeInventoryCategoryService(store),
        store.Movements,
        store.Audit,
        approver,
        new AllowAllBranches(),
        new FakeTenant(),
        actor,
        unitOfWork);

    private Item AddItem(string name, string sku, decimal stock = 0m, decimal price = 10m, Guid? categoryId = null)
    {
        var item = new Item { TenantId = Tenant, Name = name, Sku = sku, StockOnHand = stock, BasePrice = price, CostPrice = 4m, CategoryId = categoryId, PricingType = PricingType.Unit, IsActive = true };
        store.Items.Add(item);
        return item;
    }

    private Category AddCategory(string name)
    {
        var category = new Category { TenantId = Tenant, Name = name };
        store.Categories.Add(category);
        return category;
    }

    private static Task<ImportBatchDto> UploadAsync(ImportService service, string csv, ImportKind kind = ImportKind.Catalog, string? key = null) =>
        service.UploadAsync(new MemoryStream(Encoding.UTF8.GetBytes(csv)), kind, key ?? Guid.NewGuid().ToString());

    private static string Catalog(params string[] rows) => CatalogHeader + "\n" + string.Join("\n", rows);

    private static string Stock(params string[] rows) => "type,sku,barcode,name,counted_qty,note\n" + string.Join("\n", rows);

    [Fact]
    public async Task The_same_idempotency_key_returns_the_existing_batch()
    {
        var service = Service();
        var first = await UploadAsync(service, Catalog("item,N1,,Noodles,,5,,,,,"), key: "k-1");
        var second = await UploadAsync(service, Catalog("item,N1,,Noodles,,5,,,,,"), key: "k-1");

        Assert.Equal(first.Id, second.Id);
        _ = Assert.Single(store.Batches.Stored);
    }

    [Fact]
    public async Task Commit_is_blocked_until_every_decision_is_made_and_creations_are_confirmed()
    {
        _ = AddItem("Cola", "C1");
        var service = Service();
        var batch = await UploadAsync(service, Catalog("item,C2,,Colla,,5,,,,,"));
        var decision = Assert.Single(batch.Plan.NeedsDecision);
        Assert.True(batch.Blocked);

        _ = await Assert.ThrowsAsync<ValidationException>(() => service.CommitAsync(batch.Id, new CommitImportRequest("1234", batch.PlanHash, true)));

        var resolved = await service.SaveResolutionsAsync(batch.Id, new Dictionary<string, ImportResolutionDto> { [decision.Key] = new("create") });
        Assert.False(resolved.Blocked);
        _ = await Assert.ThrowsAsync<ValidationException>(() => service.CommitAsync(batch.Id, new CommitImportRequest("1234", resolved.PlanHash, false)));
        Assert.Equal(0, approver.Calls);
        Assert.Single(store.Items);

        var committed = await service.CommitAsync(batch.Id, new CommitImportRequest("1234", resolved.PlanHash, true));

        Assert.True(committed.Committed);
        Assert.Contains(store.Items, i => i.Name == "Colla" && i.Sku == "C2" && i.BasePrice == 5m);
    }

    [Fact]
    public async Task A_stale_plan_hash_aborts_with_nothing_applied_and_decisions_kept()
    {
        var cola = AddItem("Cola", "C1", price: 10m);
        _ = AddItem("Green Tea", "T1");
        var service = Service();
        var batch = await UploadAsync(service, Catalog("item,C1,,Cola,,12,,,,,", "item,T2,,Green Tee,,5,,,,,"));
        var key = Assert.Single(batch.Plan.NeedsDecision).Key;
        var resolved = await service.SaveResolutionsAsync(batch.Id, new Dictionary<string, ImportResolutionDto> { [key] = new("create") });

        cola.BasePrice = 11m; // someone edits the price between preview and commit
        var result = await service.CommitAsync(batch.Id, new CommitImportRequest("1234", resolved.PlanHash, true));

        Assert.False(result.Committed);
        Assert.Equal([2], result.ChangedLines);
        Assert.Equal(11m, cola.BasePrice);
        Assert.Equal(2, store.Items.Count);
        var reloaded = await service.GetAsync(batch.Id);
        Assert.Equal(ImportBatchStatus.Previewed, reloaded.Status);
        Assert.True(reloaded.Resolutions.ContainsKey(key));
        Assert.NotEqual(resolved.PlanHash, reloaded.PlanHash);
        Assert.Empty(store.Audit.Added);
    }

    [Fact]
    public async Task A_change_that_lands_after_the_pin_check_aborts_inside_the_transaction()
    {
        var cola = AddItem("Cola", "C1", price: 10m);
        var service = Service();
        var batch = await UploadAsync(service, Catalog("item,C1,,Cola,,12,,,,,"));
        approver.OnAuthorize = () => cola.BasePrice = 11m;

        var result = await service.CommitAsync(batch.Id, new CommitImportRequest("1234", batch.PlanHash, false));

        Assert.False(result.Committed);
        Assert.Equal([2], result.ChangedLines);
        Assert.Equal(11m, cola.BasePrice);
        Assert.Equal(ImportBatchStatus.Previewed, store.Batches.Stored.Single().Status);
        Assert.Empty(store.Audit.Added);
    }

    [Fact]
    public async Task A_second_commit_returns_the_first_result_without_applying_again()
    {
        var service = Service();
        var batch = await UploadAsync(service, Catalog("item,N1,,Noodles,,5,,,,,3"));

        var first = await service.CommitAsync(batch.Id, new CommitImportRequest("1234", batch.PlanHash, true));
        var second = await service.CommitAsync(batch.Id, new CommitImportRequest(null, "whatever", false));

        Assert.True(second.Committed);
        Assert.Equal(first.Result!.CreatedRecords.Single().Id, second.Result!.CreatedRecords.Single().Id);
        _ = Assert.Single(store.Items);
        _ = Assert.Single(store.Movements.Added);
        Assert.Equal(1, approver.Calls);
    }

    [Fact]
    public async Task An_expired_batch_is_rejected_and_marked_expired()
    {
        var service = Service();
        var batch = await UploadAsync(service, Catalog("item,N1,,Noodles,,5,,,,,"));
        store.Batches.Stored.Single().ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        _ = await Assert.ThrowsAsync<ValidationException>(() => service.CommitAsync(batch.Id, new CommitImportRequest("1234", batch.PlanHash, true)));

        Assert.Equal(ImportBatchStatus.Expired, store.Batches.Stored.Single().Status);
        Assert.Empty(store.Items);
        Assert.Equal(0, approver.Calls);
    }

    [Fact]
    public async Task The_audit_entry_lists_created_categories_and_records_with_approver_batch_and_hash()
    {
        var service = Service();
        var batch = await UploadAsync(service, Catalog("item,N1,,Noodles,Pantry,5,2,,,,", "ingredient,F1,,Flour,Dry Goods,,1,,kg,,"));

        var result = await service.CommitAsync(batch.Id, new CommitImportRequest("1234", batch.PlanHash, true));

        Assert.True(result.Committed);
        var entry = Assert.Single(store.Audit.Added);
        Assert.Equal(AuditActionType.InventoryImportCommitted, entry.ActionType);
        Assert.Equal(approver.Approver.Id, entry.ApprovedByUserId);
        Assert.Equal(batch.Id, entry.TargetEntityId);
        Assert.Contains("Pantry", entry.AfterStateJson);
        Assert.Contains("Dry Goods", entry.AfterStateJson);
        Assert.Contains("Noodles", entry.AfterStateJson);
        Assert.Contains("Flour", entry.AfterStateJson);
        Assert.Contains(batch.ContentHash, entry.AfterStateJson);
        Assert.Equal(store.Categories.Single(c => c.Name == "Pantry").Id, store.Items.Single().CategoryId);
        Assert.Equal(store.InventoryCategories.Single(c => c.Name == "Dry Goods").Id, store.Ingredients.Single().CategoryId);
        Assert.Equal(1m, store.Ingredients.Single().UnitCost);
    }

    [Fact]
    public async Task Stock_rows_post_the_delta_against_commit_time_stock_and_zero_deltas_post_nothing()
    {
        var rice = AddItem("Rice", "R1", stock: 10m);
        var salt = AddItem("Salt", "S1", stock: 4m);
        var service = Service();
        var batch = await UploadAsync(service, Stock("item,R1,,Rice,8,shelf A", "item,S1,,Salt,4,"), ImportKind.Stock);

        rice.StockOnHand -= 3m; // a sale between preview and commit
        var result = await service.CommitAsync(batch.Id, new CommitImportRequest("1234", batch.PlanHash, false));

        Assert.True(result.Committed);
        var movement = Assert.Single(store.Movements.Added);
        Assert.Equal(MovementType.Adjustment, movement.Type);
        Assert.Equal(1m, movement.Quantity);
        Assert.Equal(rice.Id, movement.ItemId);
        Assert.Contains($"import:{batch.Id}", movement.Note);
        Assert.Equal(8m, rice.StockOnHand);
        Assert.Equal(4m, salt.StockOnHand);
        Assert.Equal(movement.Id, result.Result!.Movements.Single().MovementId);
    }

    [Fact]
    public async Task A_wrong_pin_leaves_everything_unchanged_and_bumps_the_lockout_counter()
    {
        var service = Service();
        var batch = await UploadAsync(service, Catalog("item,N1,,Noodles,Pantry,5,,,,,3"));

        _ = await Assert.ThrowsAsync<ValidationException>(() => service.CommitAsync(batch.Id, new CommitImportRequest("0000", batch.PlanHash, true)));

        Assert.Equal(1, approver.FailedAttempts);
        Assert.Equal(0, unitOfWork.Transactions);
        Assert.Empty(store.Items);
        Assert.Empty(store.Categories);
        Assert.Empty(store.Movements.Added);
        Assert.Equal(ImportBatchStatus.Previewed, store.Batches.Stored.Single().Status);
    }

    [Fact]
    public async Task Link_create_and_rename_resolutions_produce_the_right_records_and_category_ids()
    {
        var drinks = AddCategory("Drinks");
        _ = AddCategory("Snacks");
        _ = AddItem("Cola", "C1");
        var service = Service();
        var batch = await UploadAsync(service, Catalog(
            "item,J1,,Juice,Drinkz,5,,,,,",
            "item,K1,,Chips,Snaks,5,,,,,",
            "item,C2,,Colla,,5,,,,,"));
        Assert.Equal(3, batch.Plan.NeedsDecision.Count);

        var resolved = await service.SaveResolutionsAsync(batch.Id, new Dictionary<string, ImportResolutionDto>
        {
            ["cat:item:drinkz"] = new("link", drinks.Id),
            ["cat:item:snaks"] = new("create"),
            ["row:4"] = new("rename", NewName: "Sparkling Water"),
        });
        Assert.False(resolved.Blocked);
        var result = await service.CommitAsync(batch.Id, new CommitImportRequest("1234", resolved.PlanHash, true));

        Assert.True(result.Committed);
        Assert.Equal(drinks.Id, store.Items.Single(i => i.Name == "Juice").CategoryId);
        var snaks = store.Categories.Single(c => c.Name == "Snaks");
        Assert.Equal(snaks.Id, store.Items.Single(i => i.Name == "Chips").CategoryId);
        Assert.Contains(store.Items, i => i.Name == "Sparkling Water" && i.Sku == "C2");
        Assert.Equal(3, store.Categories.Count);
    }

    [Fact]
    public async Task A_failure_on_a_later_row_rolls_back_everything_including_earlier_categories()
    {
        var service = Service();
        var batch = await UploadAsync(service, Catalog("item,N1,,Noodles,Pantry,5,,,,,", "item,B1,,Boom,Pantry,5,,,,,"));

        _ = await Assert.ThrowsAsync<ValidationException>(() => service.CommitAsync(batch.Id, new CommitImportRequest("1234", batch.PlanHash, true)));

        Assert.Empty(store.Categories);
        Assert.Empty(store.Items);
        Assert.Empty(store.Audit.Added);
        Assert.Equal(ImportBatchStatus.Previewed, store.Batches.Stored.Single().Status);
    }

    [Fact]
    public async Task Updates_change_only_the_listed_fields_and_never_touch_stock()
    {
        var cola = AddItem("Cola", "C1", stock: 5m, price: 10m);
        var service = Service();
        var batch = await UploadAsync(service, Catalog("item,C1,,Cola,,12,CLEAR,3,,false,99"));

        var result = await service.CommitAsync(batch.Id, new CommitImportRequest("1234", batch.PlanHash, false));

        Assert.True(result.Committed);
        Assert.Equal(1, result.Result!.UpdatedCount);
        Assert.Equal(12m, cola.BasePrice);
        Assert.Null(cola.CostPrice);
        Assert.Equal(3m, cola.LowStockThreshold);
        Assert.False(cola.IsActive);
        Assert.Equal(5m, cola.StockOnHand);
        Assert.Empty(store.Movements.Added);
    }

    // ---------------- fakes ----------------

    private sealed class Store
    {
        public List<Item> Items { get; } = [];
        public List<InventoryItem> Ingredients { get; } = [];
        public List<Category> Categories { get; } = [];
        public List<InventoryCategory> InventoryCategories { get; } = [];
        public FakeBatches Batches { get; } = new();
        public FakeMovements Movements { get; } = new();
        public FakeAudit Audit { get; } = new();
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

    /// <summary>Simulates a database transaction over the in-memory store: everything is restored if the operation throws.</summary>
    private sealed class FakeUnitOfWork(Store store) : IUnitOfWork
    {
        public int Transactions { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            Transactions++;
            var items = store.Items.Select(i => (i, i.Name, i.Sku, i.BasePrice, i.CostPrice, i.CategoryId, i.StockOnHand, i.IsActive, i.LowStockThreshold)).ToList();
            var ingredients = store.Ingredients.Select(i => (i, i.Name, i.QuantityOnHand, i.UnitCost, i.CategoryId)).ToList();
            var categories = store.Categories.ToList();
            var inventoryCategories = store.InventoryCategories.ToList();
            var movements = store.Movements.Added.ToList();
            var audits = store.Audit.Added.ToList();
            var batches = store.Batches.Stored.Select(b => (b, b.Status, b.PlanJson, b.ResultJson)).ToList();
            try
            {
                return await operation(cancellationToken);
            }
            catch
            {
                store.Items.Clear();
                foreach (var s in items)
                {
                    (s.i.Name, s.i.Sku, s.i.BasePrice, s.i.CostPrice, s.i.CategoryId, s.i.StockOnHand, s.i.IsActive, s.i.LowStockThreshold) =
                        (s.Name, s.Sku, s.BasePrice, s.CostPrice, s.CategoryId, s.StockOnHand, s.IsActive, s.LowStockThreshold);
                    store.Items.Add(s.i);
                }

                store.Ingredients.Clear();
                foreach (var s in ingredients)
                {
                    (s.i.Name, s.i.QuantityOnHand, s.i.UnitCost, s.i.CategoryId) = (s.Name, s.QuantityOnHand, s.UnitCost, s.CategoryId);
                    store.Ingredients.Add(s.i);
                }

                store.Categories.Clear();
                store.Categories.AddRange(categories);
                store.InventoryCategories.Clear();
                store.InventoryCategories.AddRange(inventoryCategories);
                store.Movements.Added.Clear();
                store.Movements.Added.AddRange(movements);
                store.Audit.Added.Clear();
                store.Audit.Added.AddRange(audits);
                foreach (var s in batches)
                {
                    (s.b.Status, s.b.PlanJson, s.b.ResultJson) = (s.Status, s.PlanJson, s.ResultJson);
                }

                throw;
            }
        }
    }

    private sealed class FakeApprover : IApproverAuthorizationService
    {
        public User Approver { get; } = new() { TenantId = Tenant, Name = "Mae", Role = Role.Manager };
        public int Calls { get; private set; }
        public int FailedAttempts { get; private set; }
        public Action? OnAuthorize { get; set; }

        public Task<User> AuthorizeAsync(string? pin, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (pin != "1234")
            {
                FailedAttempts++;
                throw new ValidationException("approverPin", "Wrong PIN.");
            }

            OnAuthorize?.Invoke();
            return Task.FromResult(Approver);
        }
    }

    private sealed class FakeBatches : IImportBatchRepository
    {
        public List<ImportBatch> Stored { get; } = [];

        public Task<ImportBatch?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Stored.FirstOrDefault(b => b.Id == id));

        public Task<ImportBatch?> GetUntrackedAsync(Guid id, CancellationToken cancellationToken = default) => GetAsync(id, cancellationToken);

        public Task<ImportBatch?> FindByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(Stored.FirstOrDefault(b => b.TenantId == tenantId && b.IdempotencyKey == idempotencyKey));

        public void Add(ImportBatch batch) => Stored.Add(batch);
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

    private sealed class FakeItems(Store store) : IItemRepository
    {
        public Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(store.Items.FirstOrDefault(i => i.Id == id));

        public Task<IReadOnlyList<Item>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Item>>(store.Items.Where(i => ids.Contains(i.Id)).ToList());

        public Task<IReadOnlyList<Item>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Item>>(store.Items.ToList());

        public Task<bool> BarcodeExistsAsync(Guid tenantId, string barcode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string?> GetDeletedNameByBarcodeAsync(Guid tenantId, string barcode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Add(Item item) => store.Items.Add(item);
    }

    private sealed class FakeIngredients(Store store) : IInventoryItemRepository
    {
        public Task<IReadOnlyList<InventoryItem>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventoryItem>>(store.Ingredients.ToList());

        public Task<InventoryItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(store.Ingredients.FirstOrDefault(i => i.Id == id));

        public Task<IReadOnlyList<InventoryItem>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventoryItem>>(store.Ingredients.Where(i => ids.Contains(i.Id)).ToList());

        public Task<InventoryItem?> GetByLinkedItemIdAsync(Guid tenantId, Guid itemId, CancellationToken cancellationToken = default) => Task.FromResult(store.Ingredients.FirstOrDefault(i => i.LinkedItemId == itemId));

        public Task<IReadOnlyList<InventoryItem>> ListByCategoryTrackedAsync(Guid categoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Add(InventoryItem inventoryItem) => store.Ingredients.Add(inventoryItem);
    }

    private sealed class FakeCategories(Store store) : ICategoryRepository
    {
        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(store.Categories.FirstOrDefault(c => c.Id == id));

        public Task<IReadOnlyList<Category>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Category>>(store.Categories.ToList());

        public void Add(Category category) => store.Categories.Add(category);
    }

    private sealed class FakeInventoryCategories(Store store) : IInventoryCategoryRepository
    {
        public Task<InventoryCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(store.InventoryCategories.FirstOrDefault(c => c.Id == id));

        public Task<IReadOnlyList<InventoryCategory>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventoryCategory>>(store.InventoryCategories.ToList());

        public void Add(InventoryCategory category) => store.InventoryCategories.Add(category);

        public void Remove(InventoryCategory category) => store.InventoryCategories.Remove(category);
    }

    private sealed class FakeCategoryService(Store store) : ICategoryService
    {
        public Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
        {
            var category = new Category { TenantId = Tenant, Name = request.Name.Trim(), SortOrder = request.SortOrder };
            store.Categories.Add(category);
            return Task.FromResult(new CategoryDto(category.Id, category.Name, category.SortOrder, null, true));
        }

        public Task<CategoryDto> UpdateAsync(Guid categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeInventoryCategoryService(Store store) : IInventoryCategoryService
    {
        public Task<IReadOnlyList<InventoryCategoryDto>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<InventoryCategoryDto> CreateAsync(CreateInventoryCategoryRequest request, CancellationToken cancellationToken = default)
        {
            var category = new InventoryCategory { TenantId = Tenant, Name = request.Name.Trim(), SortOrder = request.SortOrder };
            store.InventoryCategories.Add(category);
            return Task.FromResult(new InventoryCategoryDto(category.Id, category.Name, category.SortOrder));
        }

        public Task<InventoryCategoryDto> UpdateAsync(Guid id, UpdateInventoryCategoryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>Stands in for ItemService; a name of "Boom" fails like a validation error would.</summary>
    private sealed class FakeItemService(Store store) : IItemService
    {
        public Task<ItemDto> CreateAsync(CreateItemRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Name == "Boom")
            {
                throw new ValidationException("Barcode", "Boom.");
            }

            var item = new Item
            {
                TenantId = Tenant, Name = request.Name, Sku = request.Sku, Barcode = request.Barcode, CategoryId = request.CategoryId,
                BasePrice = request.BasePrice, PricingType = request.PricingType, CostPrice = request.CostPrice, Unit = request.Unit, IsActive = true,
            };
            store.Items.Add(item);
            return Task.FromResult(Dto(item));
        }

        public Task<ItemDto> UpdateAsync(Guid itemId, UpdateItemRequest request, CancellationToken cancellationToken = default)
        {
            var item = store.Items.Single(i => i.Id == itemId);
            item.Name = request.Name;
            item.Sku = request.Sku;
            item.Barcode = request.Barcode;
            item.CategoryId = request.CategoryId;
            item.BasePrice = request.BasePrice;
            item.IsActive = request.IsActive;
            item.Unit = request.Unit ?? item.Unit;
            item.CostPrice = request.ClearCostPrice ? null : request.CostPrice ?? item.CostPrice;
            return Task.FromResult(Dto(item));
        }

        public Task<ItemDto> UpdateLowStockThresholdAsync(Guid itemId, UpdateLowStockThresholdRequest request, CancellationToken cancellationToken = default)
        {
            var item = store.Items.Single(i => i.Id == itemId);
            item.LowStockThreshold = request.Threshold;
            return Task.FromResult(Dto(item));
        }

        private static ItemDto Dto(Item item) => (ItemDto)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ItemDto)) with { Id = item.Id };

        public Task<IReadOnlyList<ItemDto>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task ReorderAsync(ReorderItemsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ItemDto> UpdateTingiConfigAsync(Guid itemId, UpdateTingiConfigRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ItemDto> UpdateServiceDurationAsync(Guid itemId, UpdateServiceDurationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ItemDto> UpdateDepartmentAsync(Guid itemId, UpdateItemDepartmentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeInventoryItemService(Store store) : IInventoryItemService
    {
        public Task<InventoryItemDto> CreateAsync(CreateInventoryItemRequest request, CancellationToken cancellationToken = default)
        {
            var ingredient = new InventoryItem
            {
                TenantId = Tenant, Name = request.Name, Sku = request.Sku, BaseUnit = request.BaseUnit, PackagingUnit = request.PackagingUnit,
                PackagingSize = request.PackagingSize, LowStockThreshold = request.LowStockThreshold, CategoryId = request.CategoryId, UnitCost = request.UnitCost, IsActive = true,
            };
            store.Ingredients.Add(ingredient);
            return Task.FromResult(Dto(ingredient));
        }

        public Task<InventoryItemDto> UpdateAsync(Guid inventoryItemId, UpdateInventoryItemRequest request, CancellationToken cancellationToken = default)
        {
            var ingredient = store.Ingredients.Single(i => i.Id == inventoryItemId);
            ingredient.Name = request.Name;
            ingredient.Sku = request.Sku;
            ingredient.BaseUnit = request.BaseUnit;
            ingredient.LowStockThreshold = request.LowStockThreshold;
            ingredient.IsActive = request.IsActive;
            ingredient.CategoryId = request.CategoryId;
            ingredient.UnitCost = request.ClearUnitCost ? null : request.UnitCost ?? ingredient.UnitCost;
            return Task.FromResult(Dto(ingredient));
        }

        private static InventoryItemDto Dto(InventoryItem ingredient) =>
            (InventoryItemDto)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(InventoryItemDto)) with { Id = ingredient.Id };

        public Task<IReadOnlyList<InventoryItemDto>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task ReorderAsync(ReorderInventoryItemsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<InventoryItemDto> UpdatePhysicalCountAsync(Guid inventoryItemId, UpdatePhysicalCountRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<InventoryItemDto> ReceiveStockAsync(Guid inventoryItemId, ReceiveInventoryStockRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
