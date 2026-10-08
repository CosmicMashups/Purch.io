using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Catalog;
using Purch.Application.Lifecycle;
using Purch.Application.Orders;
using Purch.Application.Pos;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class LifecycleEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Deactivating_an_item_keeps_it_listed_but_inactive_and_it_can_be_reactivated()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(admin, "Coffee");

        var result = await PostAsync<LifecycleResultDto>(admin, $"/lifecycle/Item/{item.Id}/deactivate");
        Assert.Equal(LifecycleStatus.Inactive, result.Status);
        var listed = (await admin.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions))!.Single(i => i.Id == item.Id);
        Assert.False(listed.IsActive);

        var back = await PostAsync<LifecycleResultDto>(admin, $"/lifecycle/Item/{item.Id}/reactivate");
        Assert.Equal(LifecycleStatus.Active, back.Status);
    }

    [Fact]
    public async Task A_deleted_item_leaves_the_list_stays_in_the_database_and_comes_back_inactive_on_restore()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(admin, "Tea");

        var deleted = await PostAsync<LifecycleResultDto>(admin, $"/lifecycle/Item/{item.Id}/delete");
        Assert.Equal(LifecycleStatus.Deleted, deleted.Status);
        Assert.DoesNotContain(await admin.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions) ?? [], i => i.Id == item.Id);
        var bin = await admin.GetFromJsonAsync<List<DeletedRecordDto>>("/lifecycle/Item/deleted", JsonOptions);
        Assert.Contains(bin!, r => r.Id == item.Id && r.Name == "Tea");

        var restored = await PostAsync<LifecycleResultDto>(admin, $"/lifecycle/Item/{item.Id}/restore");
        Assert.Equal(LifecycleStatus.Inactive, restored.Status);
        var listed = (await admin.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions))!.Single(i => i.Id == item.Id);
        Assert.False(listed.IsActive);
    }

    [Fact]
    public async Task A_cashier_cannot_deactivate_or_delete_anything()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var cashier = await TestSessions.CashierClientAsync(admin);
        var item = await CreateItemAsync(admin, "Juice");

        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PostAsync($"/lifecycle/Item/{item.Id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PostAsync($"/lifecycle/Item/{item.Id}/delete", null)).StatusCode);
    }

    [Fact]
    public async Task Warehouse_can_handle_items_but_not_promotions_or_staff()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var warehouse = await TestSessions.StaffClientAsync(admin, "Walt Warehouse", MembershipRole.Staff, StaffDuty.Warehouse, "345612");
        var item = await CreateItemAsync(admin, "Rice");

        Assert.Equal(HttpStatusCode.OK, (await warehouse.PostAsync($"/lifecycle/Item/{item.Id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await warehouse.PostAsync($"/lifecycle/BogoPromo/{Guid.NewGuid()}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await warehouse.PostAsync($"/lifecycle/Staff/{Guid.NewGuid()}/delete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await warehouse.PostAsync($"/lifecycle/Branch/{Guid.NewGuid()}/delete", null)).StatusCode);
    }

    [Fact]
    public async Task Only_an_admin_touches_a_branch_and_a_manager_cannot_delete_a_device()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var manager = await TestSessions.ManagerClientAsync(admin);

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"/lifecycle/Branch/{Guid.NewGuid()}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"/lifecycle/Device/{Guid.NewGuid()}/delete", null)).StatusCode);
    }

    [Fact]
    public async Task The_last_admin_cannot_be_deactivated_or_deleted()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var members = await admin.GetFromJsonAsync<List<Purch.Application.Onboarding.MemberDto>>("/staff/members", JsonOptions);
        var onlyAdmin = members!.Single(m => m.Role == MembershipRole.Admin);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/lifecycle/Staff/{onlyAdmin.Id}/delete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/lifecycle/Staff/{onlyAdmin.Id}/deactivate", null)).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_category_reports_how_many_items_use_it_in_the_impact_preview()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var category = (await (await admin.PostAsJsonAsync("/categories", new CreateCategoryRequest("Drinks", 0, null))).Content.ReadFromJsonAsync<CategoryDto>(JsonOptions))!;
        _ = await admin.PostAsJsonAsync("/items", new CreateItemRequest("Cola", null, null, category.Id, 30m, null, PricingType.Unit));

        var impact = await admin.GetFromJsonAsync<LifecycleImpactDto>($"/lifecycle/Category/{category.Id}/impact", JsonOptions);

        Assert.Contains("Used by 1 item.", impact!.Notes);
    }

    [Fact]
    public async Task Orders_lists_finished_sales_with_filters_and_exports_them_as_csv()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        var item = await CreateItemAsync(admin, "Bread");
        var sale = (await (await admin.PostAsJsonAsync(
            "/transactions/checkout",
            new CheckoutRequest(Guid.NewGuid(), [new AddTransactionLineRequest(item.Id, null, 1m)], false, null, null, new RecordPaymentRequest(PaymentMethod.Cash, 50m))))
            .Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!;

        var page = await admin.GetFromJsonAsync<OrdersPageDto>($"/orders?search={sale.ReceiptNumber}", JsonOptions);
        var row = Assert.Single(page!.Items);
        Assert.Equal(sale.Id, row.Id);
        Assert.Contains("Cash", row.PaymentMethods);

        var none = await admin.GetFromJsonAsync<OrdersPageDto>("/orders?status=Voided", JsonOptions);
        Assert.Empty(none!.Items);

        var detail = await admin.GetFromJsonAsync<TransactionDto>($"/orders/{sale.Id}", JsonOptions);
        Assert.Equal(sale.ReceiptNumber, detail!.ReceiptNumber);

        var csv = await admin.GetStringAsync("/orders/export.csv");
        Assert.StartsWith("Receipt,Date,Status", csv, StringComparison.Ordinal);
        Assert.Contains("\"Completed\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Orders_is_closed_to_cashiers()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var admin = await TestSessions.AdminClientAsync(factory);
        using var cashier = await TestSessions.CashierClientAsync(admin);

        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync("/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync("/orders/export.csv")).StatusCode);
    }

    private static async Task<ItemDto> CreateItemAsync(HttpClient admin, string name)
    {
        var response = await admin.PostAsJsonAsync("/items", new CreateItemRequest(name, null, null, null, 50m, null, PricingType.Unit));
        return (await response.Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url)
    {
        var response = await client.PostAsync(url, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }
}
