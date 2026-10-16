using System.Security.Cryptography;
using System.Text.Json;
using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Inventory.Import;

/// <summary>
/// Two-step, all-or-nothing CSV import. Upload stores the parsed rows; every review and the commit rebuild the plan on
/// the server from those rows, the saved resolutions and the current catalog (the client never sends a plan). Commit
/// checks the approver PIN before the transaction, re-plans again inside it, and applies nothing unless the plan still
/// hashes to the preview the user confirmed. Records are created and changed through the same services as the UI uses,
/// and stock only ever moves through movements (StockIn for opening stock, signed Adjustment for counted - current).
/// </summary>
public sealed class ImportService(
    IImportBatchRepository batchRepository,
    IItemRepository itemRepository,
    IInventoryItemRepository inventoryItemRepository,
    ICategoryRepository categoryRepository,
    IInventoryCategoryRepository inventoryCategoryRepository,
    IItemStockService itemStockService,
    IItemService itemService,
    IInventoryItemService inventoryItemService,
    ICategoryService categoryService,
    IInventoryCategoryService inventoryCategoryService,
    IInventoryMovementRepository movementRepository,
    IAuditLogRepository auditLogRepository,
    IApproverAuthorizationService approverAuthorizationService,
    IBranchScopeGuard branchScopeGuard,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : IImportService
{
    private const int MaxIdempotencyKeyLength = 100;
    private const int MaxResolutions = 10_000;
    private const int MaxResolutionKeyLength = 300;
    private const int MaxNewNameLength = 200;
    private const int MaxMovementNoteLength = 500;

    /// <summary>Stock kind hashes its plan against this impossible on-hand figure, so stock that moves between preview
    /// and commit (a sale) never makes the preview stale: the delta is taken at commit time anyway.</summary>
    private const decimal StockSentinel = -1m;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.General);

    public async Task<ImportBatchDto> UploadAsync(Stream content, ImportKind kind, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        var key = idempotencyKey?.Trim() ?? string.Empty;
        if (key.Length == 0 || key.Length > MaxIdempotencyKeyLength)
        {
            throw new ValidationException("Idempotency-Key", $"An Idempotency-Key header of 1 to {MaxIdempotencyKeyLength} characters is required.");
        }

        if (kind is not (ImportKind.Catalog or ImportKind.Stock))
        {
            throw new ValidationException("kind", "Kind must be catalog or stock.");
        }

        var bytes = await ReadCappedAsync(content, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var existing = await batchRepository.FindByIdempotencyKeyAsync(CurrentTenantId, key, cancellationToken);
        if (existing is not null)
        {
            return existing.ContentHash == hash && existing.Kind == kind
                ? await ToDtoAsync(existing, cancellationToken)
                : throw new ConflictException("This Idempotency-Key was already used for a different file.");
        }

        ParsedImport parsed;
        try
        {
            parsed = CsvImportParser.Parse(new MemoryStream(bytes, writable: false), kind);
        }
        catch (ImportFormatException exception)
        {
            throw new ValidationException("file", exception.Message);
        }

        var now = DateTimeOffset.UtcNow;
        var batch = new ImportBatch
        {
            TenantId = CurrentTenantId,
            Kind = kind,
            Status = ImportBatchStatus.Previewed,
            IdempotencyKey = key,
            ContentHash = hash,
            ParsedJson = JsonSerializer.Serialize(parsed, Json),
            ResolutionsJson = "{}",
            CreatedBy = CurrentUserId,
            CreatedAt = now,
            ExpiresAt = now + ImportBatch.Lifetime,
        };
        var view = await BuildAsync(parsed, new Dictionary<string, ImportResolution>(), cancellationToken);
        batch.PlanJson = JsonSerializer.Serialize(view.HashPlan, Json);
        batchRepository.Add(batch);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(batch, view);
    }

    public async Task<ImportBatchDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var batch = await RequireAsync(id, tracked: true, cancellationToken);
        if (batch.Status != ImportBatchStatus.Previewed)
        {
            return await ToDtoAsync(batch, cancellationToken);
        }

        await EnsureNotExpiredAsync(batch, cancellationToken);
        // Read-only: the stale baseline stays the plan stored at upload / resolutions save.
        return ToDto(batch, await BuildAsync(Parsed(batch), Resolutions(batch), cancellationToken));
    }

    public async Task<ImportCommitResultDto?> TryReplayAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var batch = await RequireAsync(id, tracked: false, cancellationToken);
        return batch.Status == ImportBatchStatus.Committed ? Replay(batch) : null;
    }

    public async Task<ImportBatchDto> SaveResolutionsAsync(Guid id, IReadOnlyDictionary<string, ImportResolutionDto> resolutions, CancellationToken cancellationToken = default)
    {
        var converted = ToResolutions(resolutions);
        var batch = await RequireAsync(id, tracked: true, cancellationToken);
        if (batch.Status != ImportBatchStatus.Previewed)
        {
            throw new ConflictException($"This import is {batch.Status}; its decisions can no longer change.");
        }

        await EnsureNotExpiredAsync(batch, cancellationToken);
        batch.ResolutionsJson = JsonSerializer.Serialize(converted, Json);
        var view = await BuildAsync(Parsed(batch), converted, cancellationToken);
        batch.PlanJson = JsonSerializer.Serialize(view.HashPlan, Json);
        batch.UpdatedAt = DateTimeOffset.UtcNow;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(batch, view);
    }

    public async Task<ImportCommitResultDto> CommitAsync(Guid id, CommitImportRequest request, CancellationToken cancellationToken = default)
    {
        // A read-only look first: a committed batch replays its stored result without a PIN, and a request that would
        // be refused anyway (expired, blocked, unconfirmed, stale) never burns a PIN attempt.
        var preview = await RequireAsync(id, tracked: false, cancellationToken);
        if (preview.Status == ImportBatchStatus.Committed)
        {
            return Replay(preview);
        }

        if (preview.Status != ImportBatchStatus.Previewed && preview.Status != ImportBatchStatus.Expired)
        {
            throw new ConflictException($"This import is {preview.Status} and cannot be committed.");
        }

        await EnsureNotExpiredAsync(preview, cancellationToken);
        var view = await BuildAsync(Parsed(preview), Resolutions(preview), cancellationToken);
        if (!string.Equals(view.Hash, request.PlanHash, StringComparison.Ordinal))
        {
            return Stale(preview, view);
        }

        EnsureCommittable(view.HashPlan, request);

        // Branch for the stock movements: the request's, else the device's. Checked before the PIN is consumed.
        var branchId = request.BranchId ?? currentActorProvider.BranchId;
        if (branchId is null && PostsStock(view.Display))
        {
            throw new ValidationException("branchId", "Choose the branch the stock movements are for.");
        }

        if (branchId is { } chosen)
        {
            await branchScopeGuard.EnsureAllowedAsync(chosen, cancellationToken);
        }

        // The PIN is checked BEFORE the transaction opens: a wrong PIN records a failed attempt that must survive the
        // error. Everything is loaded and planned again inside the transaction, which may be retried from the start.
        var approver = await approverAuthorizationService.AuthorizeAsync(request.ApproverPin, cancellationToken);
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var batch = await RequireAsync(id, tracked: true, ct);
            if (batch.Status == ImportBatchStatus.Committed)
            {
                return Replay(batch);
            }

            if (batch.Status != ImportBatchStatus.Previewed)
            {
                throw new ConflictException($"This import is {batch.Status} and cannot be committed.");
            }

            // Rows and resolutions come from the tracked batch, so what is applied is exactly what is stored.
            var parsed = Parsed(batch);
            var fresh = await BuildAsync(parsed, Resolutions(batch), ct);
            if (fresh.HashPlan.Blocked || !string.Equals(fresh.Hash, request.PlanHash, StringComparison.Ordinal))
            {
                return Stale(batch, fresh);
            }

            var result = batch.Kind == ImportKind.Stock
                ? await ApplyStockAsync(batch, parsed, fresh.HashPlan, approver, branchId, ct)
                : await ApplyCatalogAsync(batch, parsed, fresh.HashPlan, approver, branchId, ct);

            batch.Status = ImportBatchStatus.Committed;
            batch.CommittedAt = result.CommittedAt;
            batch.CommittedBy = CurrentUserId;
            batch.ApprovedByUserId = approver.Id;
            batch.ResultJson = JsonSerializer.Serialize(result, Json);
            batch.PlanJson = JsonSerializer.Serialize(fresh.HashPlan, Json);
            batch.UpdatedAt = result.CommittedAt;

            auditLogRepository.Add(new AuditLog
            {
                TenantId = CurrentTenantId,
                ActorUserId = CurrentUserId,
                ActionType = AuditActionType.InventoryImportCommitted,
                TargetEntityType = nameof(ImportBatch),
                TargetEntityId = batch.Id,
                ApprovedByUserId = approver.Id,
                BeforeStateJson = JsonSerializer.Serialize(new { status = nameof(ImportBatchStatus.Previewed) }),
                AfterStateJson = JsonSerializer.Serialize(new
                {
                    batchId = batch.Id,
                    kind = batch.Kind.ToString(),
                    fileHash = batch.ContentHash,
                    approverUserId = approver.Id,
                    createdCategories = result.CreatedCategories.Select(c => new { c.Kind, c.Id, c.Name }),
                    createdRecords = result.CreatedRecords.Select(c => new { c.Kind, c.Id, c.Name }),
                    updatedCount = result.UpdatedCount,
                    stockAdjustmentCount = result.Movements.Count(m => m.Type == MovementType.Adjustment),
                    openingStockCount = result.Movements.Count(m => m.Type == MovementType.StockIn),
                }),
            });

            _ = await unitOfWork.SaveChangesAsync(ct);
            return new ImportCommitResultDto(true, result, null, [], null);
        }, cancellationToken);
    }

    // ---------------- apply ----------------

    private async Task<ImportResultDto> ApplyCatalogAsync(ImportBatch batch, ParsedImport parsed, ImportPlan plan, User approver, Guid? branchId, CancellationToken ct)
    {
        var rows = parsed.Rows.ToDictionary(r => r.Line);
        var createdCategories = new List<ImportCreatedDto>();
        var createdRecords = new List<ImportCreatedDto>();
        var movements = new List<ImportMovementDto>();
        var newCategoryIds = new Dictionary<string, Guid>(StringComparer.Ordinal);

        foreach (var create in plan.WillCreate.Where(c => c.Kind == PlannedCreateKind.Category))
        {
            var categoryId = create.Type == ImportRowType.Item
                ? (await categoryService.CreateAsync(new CreateCategoryRequest(create.Name, 0), ct)).Id
                : (await inventoryCategoryService.CreateAsync(new CreateInventoryCategoryRequest(create.Name, 0), ct)).Id;
            newCategoryIds[create.Key!] = categoryId;
            createdCategories.Add(new ImportCreatedDto(create.Type == ImportRowType.Item ? "itemCategory" : "ingredientCategory", categoryId, create.Name, create.Lines));
        }

        Guid? CategoryOf(Guid? resolved, string? newKey) =>
            resolved ?? (newKey is { } key ? newCategoryIds.TryGetValue(key, out var id) ? id : throw new InvalidOperationException($"Category {key} was not created.") : null);

        foreach (var create in plan.WillCreate.Where(c => c.Kind != PlannedCreateKind.Category))
        {
            var row = rows[create.Lines[0]];
            var categoryId = CategoryOf(create.ResolvedCategoryId, create.NewCategoryKey);
            Guid id;
            if (create.Kind == PlannedCreateKind.Item)
            {
                id = (await itemService.CreateAsync(new CreateItemRequest(
                    create.Name, row.Sku, row.Barcode, categoryId, row.Price!.Value, null, PricingType.Unit, CostPrice: row.Cost, Unit: row.Unit), ct)).Id;
                if (row.ReorderThreshold is { } threshold)
                {
                    _ = await itemService.UpdateLowStockThresholdAsync(id, new UpdateLowStockThresholdRequest(threshold), ct);
                }

                if (row.Active == false)
                {
                    await UpdateItemAsync(id, row, [new FieldChange("active", "true", "false")], null, ct);
                }
            }
            else
            {
                var unit = row.Unit ?? "pc";
                id = (await inventoryItemService.CreateAsync(new CreateInventoryItemRequest(
                    create.Name, row.Sku, unit, unit, 1m, row.ReorderThreshold, categoryId, row.Cost), ct)).Id;
                if (row.Active == false)
                {
                    await UpdateIngredientAsync(id, row, [new FieldChange("active", "true", "false")], null, ct);
                }
            }

            createdRecords.Add(new ImportCreatedDto(create.Kind == PlannedCreateKind.Item ? "item" : "ingredient", id, create.Name, create.Lines));
            if (create.OpeningStock is { } opening && opening > 0m)
            {
                movements.Add(await PostAsync(batch, branchId, row, create.Kind == PlannedCreateKind.Item ? id : null, create.Kind == PlannedCreateKind.Ingredient ? id : null, MovementType.StockIn, opening, ct));
            }
        }

        foreach (var update in plan.WillUpdate)
        {
            var row = rows[update.Line];
            var category = update.Changes.Any(c => c.Field == "category") ? CategoryOf(update.ResolvedCategoryId, update.NewCategoryKey) : null;
            if (update.Type == ImportRowType.Item)
            {
                await UpdateItemAsync(update.ExistingId, row, update.Changes, category, ct);
            }
            else
            {
                await UpdateIngredientAsync(update.ExistingId, row, update.Changes, category, ct);
            }
        }

        return new ImportResultDto(batch.Id, createdCategories, createdRecords, plan.WillUpdate.Count, movements, approver.Id, DateTimeOffset.UtcNow);
    }

    /// <summary>Posts counted - current for every matched row, with current read now, inside the transaction. Never
    /// sets stock to the counted figure, so a sale that lands before the commit is absorbed into the delta.</summary>
    private async Task<ImportResultDto> ApplyStockAsync(ImportBatch batch, ParsedImport parsed, ImportPlan plan, User approver, Guid? branchId, CancellationToken ct)
    {
        var rows = parsed.Rows.ToDictionary(r => r.Line);
        var movements = new List<ImportMovementDto>();
        foreach (var update in plan.WillUpdate)
        {
            var row = rows[update.Line];
            decimal current;
            if (update.Type == ImportRowType.Ingredient)
            {
                var ingredient = await inventoryItemRepository.GetByIdAsync(update.ExistingId, ct);
                current = ingredient is { IsDeleted: false }
                    ? ingredient.QuantityOnHand
                    : throw new ValidationException("file", $"Line {row.Line}: the ingredient no longer exists.");
            }
            else
            {
                var item = await itemRepository.GetByIdAsync(update.ExistingId, ct);
                if (item is null || item.IsDeleted)
                {
                    throw new ValidationException("file", $"Line {row.Line}: the item no longer exists.");
                }

                var onHand = await itemStockService.GetOnHandAsync([item], CurrentTenantId, ct);
                current = onHand.TryGetValue(item.Id, out var quantity)
                    ? quantity
                    : throw new ValidationException("file", $"Line {row.Line}: {item.Name} holds no stock of its own.");
            }

            var delta = row.CountedQty!.Value - current;
            if (delta == 0m)
            {
                continue;
            }

            movements.Add(await PostAsync(
                batch, branchId, row, update.Type == ImportRowType.Item ? update.ExistingId : null, update.Type == ImportRowType.Ingredient ? update.ExistingId : null,
                MovementType.Adjustment, delta, ct));
        }

        return new ImportResultDto(batch.Id, [], [], 0, movements, approver.Id, DateTimeOffset.UtcNow);
    }

    private async Task<ImportMovementDto> PostAsync(ImportBatch batch, Guid? branch, ParsedRow row, Guid? itemId, Guid? ingredientId, MovementType type, decimal quantity, CancellationToken ct)
    {
        var branchId = branch ?? throw new ValidationException("branchId", "Choose the branch the stock movements are for.");

        var note = row.Note is { } extra ? $"import:{batch.Id} {extra}" : $"import:{batch.Id}";
        if (note.Length > MaxMovementNoteLength)
        {
            note = note[..MaxMovementNoteLength];
        }

        var posted = await StockPosting.PostAsync(
            itemId, ingredientId, type, quantity, branchId, note, CurrentTenantId, CurrentUserId,
            itemRepository, inventoryItemRepository, itemStockService, movementRepository, ct)
            ?? throw new ValidationException("file", $"Line {row.Line}: the record no longer exists.");
        return new ImportMovementDto(row.Line, itemId, posted.Movement.InventoryItemId, type, quantity, posted.Movement.Id);
    }

    /// <summary>Changes only the listed fields through ItemService (full-replace request built from the current values).</summary>
    private async Task UpdateItemAsync(Guid id, ParsedRow row, IReadOnlyList<FieldChange> changes, Guid? categoryId, CancellationToken ct)
    {
        var item = await itemRepository.GetByIdAsync(id, ct) ?? throw new NotFoundException("Item", id);
        var fields = changes.ToDictionary(c => c.Field, c => c);
        var cost = fields.ContainsKey("cost");
        var request = new UpdateItemRequest(
            fields.TryGetValue("name", out var name) ? name.After! : item.Name,
            fields.ContainsKey("sku") ? row.Sku : item.Sku,
            fields.ContainsKey("barcode") ? row.Barcode : item.Barcode,
            fields.ContainsKey("category") ? categoryId : item.CategoryId,
            fields.ContainsKey("price") ? row.Price!.Value : item.BasePrice,
            item.ImageUrl,
            fields.TryGetValue("active", out var active) ? active.After == "true" : item.IsActive,
            item.DepartmentId,
            item.SortOrder,
            cost && !row.ClearCost ? row.Cost : null,
            cost && row.ClearCost,
            fields.ContainsKey("unit") ? row.Unit : null);
        _ = await itemService.UpdateAsync(id, request, ct);
        if (fields.ContainsKey("reorder_threshold"))
        {
            _ = await itemService.UpdateLowStockThresholdAsync(id, new UpdateLowStockThresholdRequest(row.ReorderThreshold), ct);
        }
    }

    private async Task UpdateIngredientAsync(Guid id, ParsedRow row, IReadOnlyList<FieldChange> changes, Guid? categoryId, CancellationToken ct)
    {
        var ingredient = await inventoryItemRepository.GetByIdAsync(id, ct) ?? throw new NotFoundException("InventoryItem", id);
        var fields = changes.ToDictionary(c => c.Field, c => c);
        var cost = fields.ContainsKey("cost");
        var request = new UpdateInventoryItemRequest(
            fields.TryGetValue("name", out var name) ? name.After! : ingredient.Name,
            fields.ContainsKey("sku") ? row.Sku : ingredient.Sku,
            fields.ContainsKey("unit") ? row.Unit! : ingredient.BaseUnit,
            ingredient.PackagingUnit,
            ingredient.PackagingSize,
            fields.ContainsKey("reorder_threshold") ? row.ReorderThreshold : ingredient.LowStockThreshold,
            fields.TryGetValue("active", out var active) ? active.After == "true" : ingredient.IsActive,
            fields.ContainsKey("category") ? categoryId : ingredient.CategoryId,
            cost && !row.ClearCost ? row.Cost : null,
            cost && row.ClearCost);
        _ = await inventoryItemService.UpdateAsync(id, request, ct);
    }

    // ---------------- planning ----------------

    /// <summary><see cref="Display"/> is what the user reviews (real stock figures). <see cref="HashPlan"/> is the same
    /// plan with stock replaced by a sentinel, hashed into the planHash the commit must echo.</summary>
    private sealed record PlanView(ImportKind Kind, ImportPlan Display, ImportPlan HashPlan, string Hash);

    private async Task<PlanView> BuildAsync(ParsedImport parsed, IReadOnlyDictionary<string, ImportResolution> resolutions, CancellationToken ct)
    {
        var (catalog, stockHolding) = await LoadCatalogAsync(ct);
        var sentinel = new ExistingCatalog(catalog.Records.Select(r => r with { CurrentStock = StockSentinel }).ToList(), catalog.Categories);
        var hashPlan = Augment(ImportPlanner.Plan(parsed, sentinel, resolutions), parsed, stockHolding);
        var display = parsed.Kind == ImportKind.Stock ? Augment(ImportPlanner.Plan(parsed, catalog, resolutions), parsed, stockHolding) : hashPlan;
        return new PlanView(parsed.Kind, display, hashPlan, Hash(hashPlan));
    }

    /// <summary>Rules the planner does not know because they come from this app's records rather than the file format.</summary>
    private static ImportPlan Augment(ImportPlan plan, ParsedImport parsed, HashSet<Guid> stockHolding)
    {
        var rows = parsed.Rows.ToDictionary(r => r.Line);
        var extra = new List<RowError>();
        void Error(int line, string message) => extra.Add(new RowError(line, $"Line {line}: {message}"));

        if (parsed.Kind == ImportKind.Catalog)
        {
            foreach (var create in plan.WillCreate.Where(c => c.Kind != PlannedCreateKind.Category))
            {
                var row = rows[create.Lines[0]];
                if (create.Kind == PlannedCreateKind.Item && row.Price is null)
                {
                    Error(row.Line, "price is required for a new item.");
                }

                if (create.Kind == PlannedCreateKind.Ingredient)
                {
                    IngredientRules(row, row.Barcode is not null, row.Price is not null);
                }
            }

            foreach (var update in plan.WillUpdate.Where(u => u.Type == ImportRowType.Ingredient))
            {
                IngredientRules(rows[update.Line], update.Changes.Any(c => c.Field == "barcode"), update.Changes.Any(c => c.Field == "price"));
            }
        }
        else
        {
            foreach (var update in plan.WillUpdate.Where(u => u.Type == ImportRowType.Item && !stockHolding.Contains(u.ExistingId)))
            {
                Error(update.Line, "this item holds no stock of its own (a service, combo or recipe item).");
            }
        }

        void IngredientRules(ParsedRow row, bool barcode, bool price)
        {
            if (barcode)
            {
                Error(row.Line, "ingredients have no barcode; leave barcode blank.");
            }

            if (price)
            {
                Error(row.Line, "ingredients have no selling price; leave price blank.");
            }
        }

        return extra.Count == 0 ? plan : plan with { RowErrors = plan.RowErrors.Concat(extra).ToList() };
    }

    private async Task<(ExistingCatalog Catalog, HashSet<Guid> StockHolding)> LoadCatalogAsync(CancellationToken ct)
    {
        var tenantId = CurrentTenantId;
        var items = (await itemRepository.ListByTenantAsync(tenantId, ct)).Where(i => !i.IsDeleted).ToList();
        var stock = await itemStockService.GetOnHandAsync(items, tenantId, ct);
        var itemCategories = (await categoryRepository.ListByTenantAsync(tenantId, ct)).Where(c => !c.IsDeleted).ToList();
        var ingredientCategories = await inventoryCategoryRepository.ListByTenantAsync(tenantId, ct);
        var ingredients = (await inventoryItemRepository.ListByTenantAsync(tenantId, ct))
            .Where(i => !i.IsDeleted && i.LinkedItemId is null && !i.IsAutoCreatedForItem)
            .ToList();
        var itemCategoryNames = itemCategories.ToDictionary(c => c.Id, c => c.Name);
        var ingredientCategoryNames = ingredientCategories.ToDictionary(c => c.Id, c => c.Name);

        var records = items
            .Select(i => new ExistingRecord(
                i.Id, ImportRowType.Item, i.Sku, i.Barcode, i.Name, Name(itemCategoryNames, i.CategoryId),
                i.BasePrice, i.CostPrice, i.LowStockThreshold, i.Unit, i.IsActive, stock.GetValueOrDefault(i.Id)))
            .Concat(ingredients.Select(i => new ExistingRecord(
                i.Id, ImportRowType.Ingredient, i.Sku, null, i.Name, Name(ingredientCategoryNames, i.CategoryId),
                null, i.UnitCost, i.LowStockThreshold, i.BaseUnit, i.IsActive, i.QuantityOnHand)))
            .OrderBy(r => r.Id)
            .ToList();
        var categories = itemCategories.Select(c => new ExistingCategory(c.Id, ImportRowType.Item, c.Name))
            .Concat(ingredientCategories.Select(c => new ExistingCategory(c.Id, ImportRowType.Ingredient, c.Name)))
            .OrderBy(c => c.Id)
            .ToList();
        return (new ExistingCatalog(records, categories), stock.Keys.ToHashSet());
    }

    private static string? Name(Dictionary<Guid, string> names, Guid? id) => id is { } key && names.TryGetValue(key, out var name) ? name : null;

    private static string Hash(ImportPlan plan) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(plan, Json))).ToLowerInvariant();

    private static void EnsureCommittable(ImportPlan plan, CommitImportRequest request)
    {
        if (plan.Blocked)
        {
            throw new ValidationException("plan", "Fix every row error and answer every decision before committing.");
        }

        if (plan.WillCreate.Count > 0 && !request.ConfirmCreations)
        {
            throw new ValidationException(nameof(request.ConfirmCreations), "Confirm the new categories, items and ingredients before committing.");
        }
    }

    /// <summary>The preview the user confirmed no longer matches: nothing is applied, the batch stays Previewed with its
    /// resolutions, and the fresh plan is returned with the rows whose outcome changed.</summary>
    private ImportCommitResultDto Stale(ImportBatch batch, PlanView view)
    {
        var changed = ChangedLines(ReadPlan(batch.PlanJson), view.HashPlan);
        return new ImportCommitResultDto(
            false, null, ToDto(batch, view), changed,
            view.HashPlan.Blocked
                ? "The import changed since it was reviewed and now needs attention. Nothing was applied."
                : "The import changed since it was reviewed. Nothing was applied; review the highlighted rows and commit again.");
    }

    private static bool PostsStock(ImportPlan plan) =>
        plan.WillCreate.Any(c => c.OpeningStock is > 0m)
        || plan.WillUpdate.Any(u => u.Changes.Any(c => c.Field == "counted_qty" && c.Before != c.After));

    private static List<int> ChangedLines(ImportPlan? before, ImportPlan after)
    {
        var old = Signatures(before);
        var fresh = Signatures(after);
        return old.Keys.Union(fresh.Keys)
            .Where(line => !string.Equals(old.GetValueOrDefault(line), fresh.GetValueOrDefault(line), StringComparison.Ordinal))
            .Order()
            .ToList();
    }

    /// <summary>One string per line describing its outcome, so two plans can be compared row by row.</summary>
    private static Dictionary<int, string> Signatures(ImportPlan? plan)
    {
        var parts = new Dictionary<int, List<string>>();
        void Add(int line, string part)
        {
            if (!parts.TryGetValue(line, out var list))
            {
                parts[line] = list = [];
            }

            list.Add(part);
        }

        if (plan is null)
        {
            return [];
        }

        foreach (var error in plan.RowErrors ?? [])
        {
            Add(error.Line, $"E|{error.Message}");
        }

        foreach (var decision in plan.NeedsDecision ?? [])
        {
            foreach (var line in decision.Lines)
            {
                Add(line, $"D|{decision.Key}");
            }
        }

        foreach (var update in plan.WillUpdate ?? [])
        {
            Add(update.Line, $"U|{update.ExistingId}|{update.ResolvedCategoryId}|{update.NewCategoryKey}|{JsonSerializer.Serialize(update.Changes, Json)}");
        }

        foreach (var create in plan.WillCreate ?? [])
        {
            foreach (var line in create.Lines)
            {
                Add(line, $"C|{create.Kind}|{create.Name}|{create.ResolvedCategoryId}|{create.NewCategoryKey}");
            }
        }

        foreach (var link in plan.AutoLinks ?? [])
        {
            Add(link.Line, $"L|{link.ExistingId}");
        }

        return parts.ToDictionary(p => p.Key, p => string.Join("\n", p.Value.Order(StringComparer.Ordinal)));
    }

    // ---------------- batches and DTOs ----------------

    private async Task<ImportBatch> RequireAsync(Guid id, bool tracked, CancellationToken ct)
    {
        var batch = tracked ? await batchRepository.GetAsync(id, ct) : await batchRepository.GetUntrackedAsync(id, ct);
        return batch is null || batch.TenantId != CurrentTenantId ? throw new NotFoundException("Import", id) : batch;
    }

    private async Task EnsureNotExpiredAsync(ImportBatch batch, CancellationToken ct)
    {
        if (batch.Status == ImportBatchStatus.Previewed && batch.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return;
        }

        if (batch.Status == ImportBatchStatus.Previewed)
        {
            var tracked = await RequireAsync(batch.Id, tracked: true, ct);
            tracked.Status = ImportBatchStatus.Expired;
            tracked.UpdatedAt = DateTimeOffset.UtcNow;
            _ = await unitOfWork.SaveChangesAsync(ct);
        }

        throw new ValidationException("id", "This import has expired (uploads are kept for 24 hours). Upload the file again.");
    }

    private static ImportCommitResultDto Replay(ImportBatch batch)
    {
        var result = batch.ResultJson is { } json ? JsonSerializer.Deserialize<ImportResultDto>(json, Json) : null;
        return new ImportCommitResultDto(true, result, null, [], null);
    }

    private static ParsedImport Parsed(ImportBatch batch) =>
        JsonSerializer.Deserialize<ParsedImport>(batch.ParsedJson, Json) ?? throw new InvalidOperationException("Import batch has no parsed rows.");

    private static Dictionary<string, ImportResolution> Resolutions(ImportBatch batch) =>
        JsonSerializer.Deserialize<Dictionary<string, ImportResolution>>(batch.ResolutionsJson, Json) ?? [];

    private static ImportPlan? ReadPlan(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ImportPlan>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Dictionary<string, ImportResolution> ToResolutions(IReadOnlyDictionary<string, ImportResolutionDto>? resolutions)
    {
        resolutions ??= new Dictionary<string, ImportResolutionDto>();
        if (resolutions.Count > MaxResolutions)
        {
            throw new ValidationException("resolutions", $"At most {MaxResolutions} decisions can be saved.");
        }

        var result = new Dictionary<string, ImportResolution>(StringComparer.Ordinal);
        foreach (var (key, dto) in resolutions)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > MaxResolutionKeyLength || dto is null)
            {
                throw new ValidationException("resolutions", "Every decision needs its key and an answer.");
            }

            result[key] = (dto.Choice ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "link" => dto.TargetId is { } target && target != Guid.Empty
                    ? new ImportResolution(ResolutionChoice.Link, target)
                    : throw new ValidationException(key, "Choose the existing record to link to."),
                "create" => new ImportResolution(ResolutionChoice.Create),
                "rename" => dto.NewName?.Trim() is { Length: > 0 and <= MaxNewNameLength } newName
                    ? new ImportResolution(ResolutionChoice.Rename, null, newName)
                    : throw new ValidationException(key, $"A new name of 1 to {MaxNewNameLength} characters is required."),
                _ => throw new ValidationException(key, "Choice must be link, create or rename."),
            };
        }

        return result;
    }

    private async Task<ImportBatchDto> ToDtoAsync(ImportBatch batch, CancellationToken ct)
    {
        if (batch.Status == ImportBatchStatus.Previewed)
        {
            return ToDto(batch, await BuildAsync(Parsed(batch), Resolutions(batch), ct));
        }

        // Committed or expired: show the plan as it was last reviewed/applied, not one rebuilt against today's catalog.
        var stored = ReadPlan(batch.PlanJson) ?? new ImportPlan([], [], [], [], [], 0, []);
        return ToDto(batch, new PlanView(batch.Kind, stored, stored, Hash(stored)));
    }

    private ImportBatchDto ToDto(ImportBatch batch, PlanView view)
    {
        var resolutions = Resolutions(batch).ToDictionary(
            pair => pair.Key,
            pair => new ImportResolutionDto(pair.Value.Choice.ToString().ToLowerInvariant(), pair.Value.TargetId, pair.Value.NewName));
        var result = batch.ResultJson is { } json ? JsonSerializer.Deserialize<ImportResultDto>(json, Json) : null;
        return new ImportBatchDto(
            batch.Id, batch.Kind, batch.Status, batch.ContentHash, batch.CreatedAt, batch.ExpiresAt,
            RedactCosts(view.Display, currentActorProvider.CanSeeCosts), view.Hash, view.Display.Blocked,
            currentActorProvider.BranchId is null && batch.Status == ImportBatchStatus.Previewed && PostsStock(view.Display), resolutions, result);
    }

    /// <summary>Blanks cost before/after values for an actor without CanSeeCosts (the hash is always taken unredacted).</summary>
    internal static ImportPlan RedactCosts(ImportPlan plan, bool canSeeCosts) =>
        canSeeCosts
            ? plan
            : plan with
            {
                WillUpdate = plan.WillUpdate
                    .Select(u => u with { Changes = u.Changes.Select(c => c.Field == "cost" ? c with { Before = null, After = null } : c).ToList() })
                    .ToList(),
            };

    private static async Task<byte[]> ReadCappedAsync(Stream content, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > CsvImportParser.MaxBytes)
            {
                throw new ValidationException("file", "The file is larger than 5 MB.");
            }
        }

        return buffer.ToArray();
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Imports require an authenticated tenant context.");

    private Guid CurrentUserId => currentActorProvider.UserId
        ?? throw new InvalidOperationException("Imports require an authenticated staff user.");
}
