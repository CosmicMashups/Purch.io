using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Catalog;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Application.Pos;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

/// <summary>A modifier group can link to a category so every active item in it is offered as an add-on
/// ("Add fries & sides" -> the "Fries & sides" category), next to the group's own modifiers.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class CategoryLinkedModifierGroupTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private sealed record Setup(HttpClient Client, ItemDto Burger, ItemDto Fries, ItemDto Rings, ModifierGroupDto Group);

    private static async Task<Setup> ArrangeAsync(PurchApiFactory factory, bool required = false, bool multiple = true)
    {
        var client = await TestSessions.AdminClientAsync(factory);
        var category = await ReadAsync<CategoryDto>(await client.PostAsJsonAsync("/categories", new CreateCategoryRequest("Fries & sides", 1)));
        var burger = await ReadAsync<ItemDto>(await client.PostAsJsonAsync("/items", new CreateItemRequest("Burger", null, null, null, 100m, null, PricingType.Unit)));
        var fries = await ReadAsync<ItemDto>(await client.PostAsJsonAsync("/items", new CreateItemRequest("Large Fries", null, null, category.Id, 80m, null, PricingType.Unit)));
        var rings = await ReadAsync<ItemDto>(await client.PostAsJsonAsync("/items", new CreateItemRequest("Onion Rings", null, null, category.Id, 70m, null, PricingType.Unit)));

        // Items with no stock read as sold out, so every one starts with ten on the shelf.
        var branchId = (await ReadAsync<List<BranchDto>>(await client.GetAsync("/branches"))).Single().Id;
        foreach (var item in new[] { burger, fries, rings })
        {
            _ = await ReadAsync<object>(await client.PostAsJsonAsync("/inventory/movements", new RecordMovementRequest(item.Id, branchId, MovementType.StockIn, 10m, null, null, null, null)));
        }

        var group = await ReadAsync<ModifierGroupDto>(await client.PostAsJsonAsync("/modifier-groups", new CreateModifierGroupRequest("Add fries & sides", multiple, required, category.Id)));
        group = await ReadAsync<ModifierGroupDto>(await client.PostAsJsonAsync($"/modifier-groups/{group.Id}/modifiers", new CreateItemModifierRequest("Extra ketchup", 5m)));
        _ = await ReadAsync<ModifierGroupDto>(await client.PostAsJsonAsync($"/items/{burger.Id}/modifier-groups", new AttachModifierGroupRequest(group.Id)));
        return new Setup(client, burger, fries, rings, group);
    }

    private static Task<HttpResponseMessage> AddAsync(Setup setup, params Guid[] categoryItemIds)
    {
        return setup.Client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(setup.Burger.Id, null, 1m, null, null, categoryItemIds));
    }

    [Fact]
    public async Task A_linked_group_lists_the_category_items_live_at_their_own_price()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var setup = await ArrangeAsync(factory);
        using var shopClient = setup.Client;

        Assert.NotNull(setup.Group.CategoryId);
        Assert.Equal(["Extra ketchup"], setup.Group.Modifiers.Select(m => m.Name));

        var groups = await ReadAsync<List<ModifierGroupDto>>(await setup.Client.GetAsync("/modifier-groups"));
        var offered = groups.Single(g => g.Id == setup.Group.Id).CategoryItems!;
        Assert.Equal(["Large Fries", "Onion Rings"], offered.Select(o => o.Name));
        Assert.Equal(80m, offered.Single(o => o.Name == "Large Fries").Price);

        // A new item in the category shows up with no change to the group.
        var category = setup.Group.CategoryId!.Value;
        _ = await ReadAsync<ItemDto>(await setup.Client.PostAsJsonAsync("/items", new CreateItemRequest("Gravy", null, null, category, 15m, null, PricingType.Unit)));
        groups = await ReadAsync<List<ModifierGroupDto>>(await setup.Client.GetAsync("/modifier-groups"));
        Assert.Contains(groups.Single(g => g.Id == setup.Group.Id).CategoryItems!, o => o.Name == "Gravy");
    }

    [Fact]
    public async Task A_picked_category_item_adds_its_price_and_a_manual_modifier_can_ride_along()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var setup = await ArrangeAsync(factory);
        using var shopClient = setup.Client;

        var response = await setup.Client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(setup.Burger.Id, null, 1m, null, [setup.Group.Modifiers.Single().Id], [setup.Fries.Id]));
        var cart = await ReadAsync<TransactionDto>(response);

        var line = cart.Lines.Single();
        Assert.Equal(100m + 80m + 5m, line.UnitPrice);
        Assert.Equal(2, line.ModifierSelections.Count);
        var picked = line.ModifierSelections.Single(s => s.ItemId == setup.Fries.Id);
        Assert.Equal("Large Fries", picked.ModifierName);
        Assert.Equal("Add fries & sides", picked.ModifierGroupName);
        Assert.Equal(80m, picked.PriceDelta);
    }

    [Fact]
    public async Task An_override_changes_the_price_and_an_exclusion_hides_the_item()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var setup = await ArrangeAsync(factory);
        using var shopClient = setup.Client;

        var withOverride = await ReadAsync<ModifierGroupDto>(await setup.Client.PutAsJsonAsync(
            $"/modifier-groups/{setup.Group.Id}/category-items/{setup.Fries.Id}", new UpdateModifierCategoryItemRequest(60m, false)));
        Assert.Equal(60m, withOverride.CategoryItems!.Single(o => o.ItemId == setup.Fries.Id).Price);

        _ = await ReadAsync<ModifierGroupDto>(await setup.Client.PutAsJsonAsync(
            $"/modifier-groups/{setup.Group.Id}/category-items/{setup.Rings.Id}", new UpdateModifierCategoryItemRequest(null, true)));

        var cart = await ReadAsync<TransactionDto>(await AddAsync(setup, setup.Fries.Id));
        Assert.Equal(100m + 60m, cart.Lines.Single().UnitPrice);

        var excluded = await AddAsync(setup, setup.Rings.Id);
        Assert.Equal(HttpStatusCode.BadRequest, excluded.StatusCode);

        // Clearing the override puts the item back to its own price.
        var cleared = await ReadAsync<ModifierGroupDto>(await setup.Client.PutAsJsonAsync(
            $"/modifier-groups/{setup.Group.Id}/category-items/{setup.Fries.Id}", new UpdateModifierCategoryItemRequest(null, false)));
        Assert.Equal(80m, cleared.CategoryItems!.Single(o => o.ItemId == setup.Fries.Id).Price);
    }

    [Fact]
    public async Task The_groups_required_and_single_choice_rules_count_category_items_too()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var required = await ArrangeAsync(factory, required: true, multiple: false);
        using var shopClient = required.Client;

        Assert.Equal(HttpStatusCode.BadRequest, (await required.Client.PostAsJsonAsync(
            "/transactions/cart/lines", new AddTransactionLineRequest(required.Burger.Id, null, 1m))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AddAsync(required, required.Fries.Id)).StatusCode);

        var twoChoices = await AddAsync(required, required.Fries.Id, required.Rings.Id);
        Assert.Equal(HttpStatusCode.BadRequest, twoChoices.StatusCode);

        var otherItem = await AddAsync(required, required.Burger.Id);
        Assert.Equal(HttpStatusCode.BadRequest, otherItem.StatusCode);
    }

    [Fact]
    public async Task Selling_a_picked_category_item_takes_it_off_the_shelf_once_per_unit()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var setup = await ArrangeAsync(factory);
        using var shopClient = setup.Client;

        _ = await ReadAsync<TransactionDto>(await setup.Client.PostAsJsonAsync(
            "/transactions/cart/lines", new AddTransactionLineRequest(setup.Burger.Id, null, 2m, null, null, [setup.Fries.Id])));
        var pay = await setup.Client.PostAsJsonAsync("/transactions/cart/payments", new RecordPaymentRequest(PaymentMethod.Cash, 100000m));
        Assert.Equal(HttpStatusCode.OK, pay.StatusCode);

        var items = await ReadAsync<List<ItemDto>>(await setup.Client.GetAsync("/items"));
        Assert.Equal(8m, items.Single(i => i.Id == setup.Fries.Id).StockOnHand);
        Assert.Equal(10m, items.Single(i => i.Id == setup.Rings.Id).StockOnHand);
    }

    [Fact]
    public async Task Changing_the_linked_category_drops_the_old_overrides()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        var setup = await ArrangeAsync(factory);
        using var shopClient = setup.Client;

        _ = await ReadAsync<ModifierGroupDto>(await setup.Client.PutAsJsonAsync(
            $"/modifier-groups/{setup.Group.Id}/category-items/{setup.Fries.Id}", new UpdateModifierCategoryItemRequest(60m, false)));

        var unlinked = await ReadAsync<ModifierGroupDto>(await setup.Client.PutAsJsonAsync(
            $"/modifier-groups/{setup.Group.Id}", new UpdateModifierGroupRequest("Add fries & sides", true, false, null)));
        Assert.Null(unlinked.CategoryId);
        Assert.Null(unlinked.CategoryItems);

        var relinked = await ReadAsync<ModifierGroupDto>(await setup.Client.PutAsJsonAsync(
            $"/modifier-groups/{setup.Group.Id}", new UpdateModifierGroupRequest("Add fries & sides", true, false, setup.Group.CategoryId)));
        Assert.Equal(80m, relinked.CategoryItems!.Single(o => o.ItemId == setup.Fries.Id).Price);
    }
}
