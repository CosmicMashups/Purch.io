using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Purch.Application.Catalog;
using Purch.Application.EquipmentInventory;
using Purch.Application.Inventory;
using Purch.Application.Lifecycle;
using Purch.Application.Pos;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;
using Purch.Infrastructure.Persistence;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class EquipmentEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Equipment_can_be_created_listed_updated_and_reordered()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);

        var machine = await CreateEquipmentAsync(admin, "Ice cream machine");
        var fryer = await CreateEquipmentAsync(admin, "Deep fryer");
        Assert.Equal(EquipmentStatus.Operational, machine.Status);

        var updated = await admin.PutAsJsonAsync(
            $"/equipment/{machine.Id}",
            new UpdateEquipmentRequest("Ice cream machine 2", EquipmentKind.Equipment, null, "Kitchen", "Serviced", true));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var reorder = await admin.PutAsJsonAsync("/equipment/order", new ReorderEquipmentRequest([fryer.Id, machine.Id]));
        Assert.Equal(HttpStatusCode.NoContent, reorder.StatusCode);

        var list = (await admin.GetFromJsonAsync<List<EquipmentDto>>("/equipment", JsonOptions))!;
        Assert.Equal(["Deep fryer", "Ice cream machine 2"], list.Select(row => row.Name));
        Assert.Equal("Kitchen", list[1].Location);
    }

    [Fact]
    public async Task A_name_is_required_and_a_quantity_cannot_be_negative()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);

        var noName = await admin.PostAsJsonAsync("/equipment", new CreateEquipmentRequest(" ", EquipmentKind.Equipment, null, null, null));
        var negative = await admin.PostAsJsonAsync("/equipment", new CreateEquipmentRequest("Spoons", EquipmentKind.Utensil, -1, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
    }

    [Fact]
    public async Task Warehouse_manages_equipment_but_a_cashier_cannot()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var cashier = await TestSessions.CashierClientAsync(admin);
        using var warehouse = await TestSessions.StaffClientAsync(admin, "Walt Warehouse", MembershipRole.Staff, StaffDuty.Warehouse, "345612");
        var machine = await CreateEquipmentAsync(admin, "Griller");

        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync("/equipment")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PutAsJsonAsync($"/equipment/{machine.Id}/status", new SetEquipmentStatusRequest(EquipmentStatus.OutOfService))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await warehouse.GetAsync("/equipment")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await warehouse.PutAsJsonAsync($"/equipment/{machine.Id}/status", new SetEquipmentStatusRequest(EquipmentStatus.NeedsRepair))).StatusCode);
    }

    [Fact]
    public async Task An_item_needing_out_of_service_equipment_shows_out_of_stock_and_recovers()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var machine = await CreateEquipmentAsync(admin, "Ice cream machine");
        var sundae = await CreateItemAsync(admin, "Hot Fudge Sundae");
        var fries = await CreateItemAsync(admin, "Fries");
        _ = await LinkAsync(admin, sundae.Id, machine.Id);

        Assert.False((await ListItemAsync(admin, sundae.Id)).IsOutOfStock);

        _ = await SetStatusAsync(admin, machine.Id, EquipmentStatus.OutOfService);
        Assert.True((await ListItemAsync(admin, sundae.Id)).IsOutOfStock);
        Assert.False((await ListItemAsync(admin, fries.Id)).IsOutOfStock);

        _ = await SetStatusAsync(admin, machine.Id, EquipmentStatus.Operational);
        Assert.False((await ListItemAsync(admin, sundae.Id)).IsOutOfStock);
    }

    [Fact]
    public async Task Needs_repair_only_warns_and_inactive_equipment_never_blocks()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var machine = await CreateEquipmentAsync(admin, "Griller");
        var chicken = await CreateItemAsync(admin, "Chicken meal");
        _ = await LinkAsync(admin, chicken.Id, machine.Id);

        _ = await SetStatusAsync(admin, machine.Id, EquipmentStatus.NeedsRepair);
        Assert.False((await ListItemAsync(admin, chicken.Id)).IsOutOfStock);

        _ = await SetStatusAsync(admin, machine.Id, EquipmentStatus.OutOfService);
        Assert.True((await ListItemAsync(admin, chicken.Id)).IsOutOfStock);

        _ = await admin.PostAsync($"/lifecycle/Equipment/{machine.Id}/deactivate", null);
        Assert.False((await ListItemAsync(admin, chicken.Id)).IsOutOfStock);
    }

    [Fact]
    public async Task The_catalog_etag_changes_when_equipment_goes_out_of_service()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var machine = await CreateEquipmentAsync(admin, "Ice cream machine");
        var sundae = await CreateItemAsync(admin, "McFloat");
        _ = await LinkAsync(admin, sundae.Id, machine.Id);

        var before = (await admin.GetAsync("/items")).Headers.ETag!.ToString();
        _ = await SetStatusAsync(admin, machine.Id, EquipmentStatus.OutOfService);
        var after = (await admin.GetAsync("/items")).Headers.ETag!.ToString();

        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task A_combo_with_a_fixed_part_that_needs_broken_equipment_is_out_of_stock_too()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var machine = await CreateEquipmentAsync(admin, "Ice cream machine");
        var desserts = (await (await admin.PostAsJsonAsync("/categories", new CreateCategoryRequest("Desserts", 1))).Content.ReadFromJsonAsync<CategoryDto>(JsonOptions))!;
        var sundae = await CreateItemAsync(admin, "Sundae", desserts.Id);
        var combo = await CreateItemAsync(admin, "Sundae set", desserts.Id, PricingType.Combo);
        var slot = await admin.PostAsJsonAsync($"/items/{combo.Id}/combo-components", new CreateItemComboComponentRequest(Guid.Empty, "Dessert", 1, null, sundae.Id));
        Assert.Equal(HttpStatusCode.OK, slot.StatusCode);
        _ = await LinkAsync(admin, sundae.Id, machine.Id);

        _ = await SetStatusAsync(admin, machine.Id, EquipmentStatus.OutOfService);

        Assert.True((await ListItemAsync(admin, combo.Id)).IsOutOfStock);
    }

    [Fact]
    public async Task The_server_refuses_to_add_or_check_out_an_item_whose_equipment_is_down_but_accepts_an_offline_sale()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var machine = await CreateEquipmentAsync(admin, "Ice cream machine");
        var sundae = await CreateItemAsync(admin, "Sundae");
        _ = await LinkAsync(admin, sundae.Id, machine.Id);
        _ = await SetStatusAsync(admin, machine.Id, EquipmentStatus.OutOfService);

        var add = await admin.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(sundae.Id, null, 1m));
        Assert.Equal(HttpStatusCode.BadRequest, add.StatusCode);
        Assert.Contains("out of stock", await add.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        CheckoutRequest Sale(bool offline) => new(
            Guid.NewGuid(),
            [new AddTransactionLineRequest(sundae.Id, null, 1m)],
            false,
            null,
            null,
            new RecordPaymentRequest(PaymentMethod.Cash, 500m),
            OfflineSale: offline);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/transactions/checkout", Sale(offline: false))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/transactions/checkout", Sale(offline: true))).StatusCode);

        _ = await SetStatusAsync(admin, machine.Id, EquipmentStatus.Operational);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/transactions/checkout", Sale(offline: false))).StatusCode);
    }

    [Fact]
    public async Task Linking_replaces_the_set_and_rejects_equipment_that_is_not_the_tenants()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var machine = await CreateEquipmentAsync(admin, "Ice cream machine");
        var fryer = await CreateEquipmentAsync(admin, "Deep fryer");
        var item = await CreateItemAsync(admin, "Sundae");

        var first = await LinkAsync(admin, item.Id, machine.Id, fryer.Id);
        Assert.Equal(2, first.Count);

        var second = await LinkAsync(admin, item.Id, fryer.Id);
        Assert.Equal(fryer.Id, Assert.Single(second).EquipmentId);

        var bogus = await admin.PutAsJsonAsync($"/items/{item.Id}/equipment", new ReplaceItemEquipmentRequest([Guid.NewGuid()]));
        Assert.Equal(HttpStatusCode.NotFound, bogus.StatusCode);

        var list = (await admin.GetFromJsonAsync<List<EquipmentDto>>("/equipment", JsonOptions))!;
        Assert.Equal(1, list.Single(row => row.Id == fryer.Id).UsedByItemCount);
        Assert.Equal(0, list.Single(row => row.Id == machine.Id).UsedByItemCount);
    }

    [Fact]
    public async Task A_status_change_is_audited_once_and_a_no_op_is_not()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var machine = await CreateEquipmentAsync(admin, "Ice cream machine");

        _ = await SetStatusAsync(admin, machine.Id, EquipmentStatus.OutOfService);
        _ = await SetStatusAsync(admin, machine.Id, EquipmentStatus.OutOfService);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PurchDbContext>();
        var entries = db.AuditLogs
            .IgnoreQueryFilters()
            .Where(log => log.TargetEntityId == machine.Id && log.ActionType == AuditActionType.EquipmentStatusChanged)
            .ToList();
        _ = Assert.Single(entries);
    }

    [Fact]
    public async Task Equipment_follows_the_lifecycle_and_reports_how_many_items_use_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var machine = await CreateEquipmentAsync(admin, "Ice cream machine");
        var item = await CreateItemAsync(admin, "Sundae");
        _ = await LinkAsync(admin, item.Id, machine.Id);

        var impact = await admin.GetFromJsonAsync<LifecycleImpactDto>($"/lifecycle/Equipment/{machine.Id}/impact", JsonOptions);
        Assert.Contains("Used by 1 item.", impact!.Notes);

        var deleted = await admin.PostAsync($"/lifecycle/Equipment/{machine.Id}/delete", null);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.DoesNotContain(await admin.GetFromJsonAsync<List<EquipmentDto>>("/equipment", JsonOptions) ?? [], row => row.Id == machine.Id);
        var bin = await admin.GetFromJsonAsync<List<DeletedRecordDto>>("/lifecycle/Equipment/deleted", JsonOptions);
        Assert.Contains(bin!, record => record.Id == machine.Id);

        var restored = await admin.PostAsync($"/lifecycle/Equipment/{machine.Id}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.False((await admin.GetFromJsonAsync<List<EquipmentDto>>("/equipment", JsonOptions))!.Single(row => row.Id == machine.Id).IsActive);
    }

    private static async Task<EquipmentDto> CreateEquipmentAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/equipment", new CreateEquipmentRequest(name, EquipmentKind.Equipment, null, null, null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EquipmentDto>(JsonOptions))!;
    }

    private static async Task<EquipmentDto> SetStatusAsync(HttpClient client, Guid id, EquipmentStatus status)
    {
        var response = await client.PutAsJsonAsync($"/equipment/{id}/status", new SetEquipmentStatusRequest(status));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EquipmentDto>(JsonOptions))!;
    }

    private static async Task<List<ItemEquipmentDto>> LinkAsync(HttpClient client, Guid itemId, params Guid[] equipmentIds)
    {
        var response = await client.PutAsJsonAsync($"/items/{itemId}/equipment", new ReplaceItemEquipmentRequest(equipmentIds));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<ItemEquipmentDto>>(JsonOptions))!;
    }

    /// <summary>An item with stock on hand, so that only equipment can make it show as out of stock.</summary>
    private static async Task<ItemDto> CreateItemAsync(HttpClient client, string name, Guid? categoryId = null, PricingType type = PricingType.Unit)
    {
        var response = await client.PostAsJsonAsync("/items", new CreateItemRequest(name, null, null, categoryId, 50m, null, type));
        var item = (await response.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
        if (type != PricingType.Combo)
        {
            var stocked = await client.PostAsJsonAsync(
                "/inventory/movements",
                new RecordMovementRequest(item.Id, TestSessions.ShopOf(client).Tenant.BranchId, MovementType.StockIn, 100m, null, null, null, null));
            Assert.Equal(HttpStatusCode.OK, stocked.StatusCode);
        }

        return item;
    }

    private static async Task<ItemDto> ListItemAsync(HttpClient client, Guid id)
    {
        return (await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions))!.Single(item => item.Id == id);
    }
}
