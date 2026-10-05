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

/// <summary>A deal such as Buy 1 Take 1 or a meal with a choice of drink is a Combo item with its own category.
/// Its slots can name a fixed item, can have any number of choices, and can price individual choices.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class ComboDealTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task<CategoryDto> CategoryAsync(HttpClient client, string name, int order)
    {
        return (await (await client.PostAsJsonAsync("/categories", new CreateCategoryRequest(name, order))).Content.ReadFromJsonAsync<CategoryDto>(JsonOptions))!;
    }

    private static async Task<ItemDto> ItemAsync(HttpClient client, string name, decimal price, Guid? categoryId, PricingType type = PricingType.Unit)
    {
        return (await (await client.PostAsJsonAsync("/items", new CreateItemRequest(name, null, null, categoryId, price, null, type))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;
    }

    private static async Task<ItemComboComponentDto> SlotAsync(HttpClient client, Guid comboId, CreateItemComboComponentRequest request)
    {
        var response = await client.PostAsJsonAsync($"/items/{comboId}/combo-components", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ItemComboComponentDto>(JsonOptions))!;
    }

    [Fact]
    public async Task A_buy_one_take_one_deal_fills_in_its_fixed_item_without_the_customer_picking_anything()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var meals = await CategoryAsync(client, "Meals", 1);
        var promos = await CategoryAsync(client, "Promos", 2);
        var chicken = await ItemAsync(client, "Fried Chicken", 99m, meals.Id);
        var deal = await ItemAsync(client, "Chicken Buy 1 Take 1", 99m, promos.Id, PricingType.Combo);

        var slot = await SlotAsync(client, deal.Id, new CreateItemComboComponentRequest(Guid.Empty, "2 pcs Fried Chicken", 2, null, chicken.Id));
        Assert.Equal(chicken.Id, slot.ComponentItemId);
        Assert.Equal("Fried Chicken", slot.ComponentItemName);
        Assert.Equal(meals.Id, slot.ComponentCategoryId);

        var cart = await (await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(deal.Id, null, 1m))).Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);

        var line = Assert.Single(cart!.Lines);
        Assert.Equal(99m, line.UnitPrice);
        Assert.Equal(2, line.ComboSelections.Count);
        Assert.All(line.ComboSelections, picked => Assert.Equal("Fried Chicken", picked.SelectedItemName));
    }

    [Fact]
    public async Task A_fixed_slot_refuses_a_different_item_and_a_deal_can_have_more_than_two_parts()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var meals = await CategoryAsync(client, "Meals", 1);
        var sides = await CategoryAsync(client, "Sides", 2);
        var drinks = await CategoryAsync(client, "Drinks", 3);
        var promos = await CategoryAsync(client, "Promos", 4);
        var chicken = await ItemAsync(client, "Fried Chicken", 99m, meals.Id);
        var burger = await ItemAsync(client, "Burger", 80m, meals.Id);
        var fries = await ItemAsync(client, "Fries", 40m, sides.Id);
        var coke = await ItemAsync(client, "Coke", 30m, drinks.Id);
        var deal = await ItemAsync(client, "Family Bundle", 250m, promos.Id, PricingType.Combo);

        var fixedSlot = await SlotAsync(client, deal.Id, new CreateItemComboComponentRequest(Guid.Empty, "Chicken", 1, null, chicken.Id));
        var burgerSlot = await SlotAsync(client, deal.Id, new CreateItemComboComponentRequest(Guid.Empty, "Burger", 1, null, burger.Id));
        var sideSlot = await SlotAsync(client, deal.Id, new CreateItemComboComponentRequest(sides.Id, "Choose a side", 1, null));
        var drinkSlot = await SlotAsync(client, deal.Id, new CreateItemComboComponentRequest(drinks.Id, "Choose a drink", 2, null));

        var wrong = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(deal.Id, null, 1m, [new ComboSelectionRequest(fixedSlot.Id, burger.Id), new ComboSelectionRequest(sideSlot.Id, fries.Id), new ComboSelectionRequest(drinkSlot.Id, coke.Id), new ComboSelectionRequest(drinkSlot.Id, coke.Id)]));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var right = await client.PostAsJsonAsync(
            "/transactions/cart/lines",
            new AddTransactionLineRequest(deal.Id, null, 1m, [new ComboSelectionRequest(sideSlot.Id, fries.Id), new ComboSelectionRequest(drinkSlot.Id, coke.Id), new ComboSelectionRequest(drinkSlot.Id, coke.Id)]));
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        var line = Assert.Single((await right.Content.ReadFromJsonAsync<TransactionDto>(JsonOptions))!.Lines);
        Assert.Equal(5, line.ComboSelections.Count); // chicken, burger, fries, coke, coke
        Assert.Equal(250m, line.UnitPrice);
        Assert.NotEqual(burgerSlot.Id, fixedSlot.Id);
    }

    [Fact]
    public async Task A_choice_can_carry_its_own_surcharge_while_the_others_are_included()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var drinks = await CategoryAsync(client, "Drinks", 1);
        var promos = await CategoryAsync(client, "Promos", 2);
        var coke = await ItemAsync(client, "Coke", 30m, drinks.Id);
        var milkTea = await ItemAsync(client, "Milk Tea", 60m, drinks.Id);
        var meal = await ItemAsync(client, "Meal Deal", 150m, promos.Id, PricingType.Combo);
        var slot = await SlotAsync(client, meal.Id, new CreateItemComboComponentRequest(drinks.Id, "Choose a drink", 1, null, null, [new ComboChoiceUpchargeDto(milkTea.Id, 20m)]));

        Assert.Equal(20m, Assert.Single(slot.ChoiceUpcharges!).Amount);

        var withCoke = await (await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(meal.Id, null, 1m, [new ComboSelectionRequest(slot.Id, coke.Id)]))).Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Equal(150m, Assert.Single(withCoke!.Lines).UnitPrice);

        var withTea = await (await client.PostAsJsonAsync("/transactions/cart/lines", new AddTransactionLineRequest(meal.Id, null, 1m, [new ComboSelectionRequest(slot.Id, milkTea.Id)]))).Content.ReadFromJsonAsync<TransactionDto>(JsonOptions);
        Assert.Contains(withTea!.Lines, line => line.UnitPrice == 170m);
    }

    [Fact]
    public async Task A_slot_can_be_edited_and_removed_and_a_surcharge_must_name_an_item_from_its_category()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var drinks = await CategoryAsync(client, "Drinks", 1);
        var meals = await CategoryAsync(client, "Meals", 2);
        var coke = await ItemAsync(client, "Coke", 30m, drinks.Id);
        var burger = await ItemAsync(client, "Burger", 80m, meals.Id);
        var combo = await ItemAsync(client, "Meal Deal", 150m, null, PricingType.Combo);
        var slot = await SlotAsync(client, combo.Id, new CreateItemComboComponentRequest(drinks.Id, "Choose a drink", 1, null));

        var offCategory = await client.PutAsJsonAsync($"/items/{combo.Id}/combo-components/{slot.Id}", new UpdateItemComboComponentRequest(drinks.Id, "Choose a drink", 1, null, null, [new ComboChoiceUpchargeDto(burger.Id, 10m)]));
        Assert.Equal(HttpStatusCode.BadRequest, offCategory.StatusCode);

        var renamed = await client.PutAsJsonAsync($"/items/{combo.Id}/combo-components/{slot.Id}", new UpdateItemComboComponentRequest(drinks.Id, "Pick two drinks", 2, 5m, null, [new ComboChoiceUpchargeDto(coke.Id, 10m)]));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var listed = Assert.Single((await client.GetFromJsonAsync<List<ItemComboComponentDto>>($"/items/{combo.Id}/combo-components", JsonOptions))!);
        Assert.Equal("Pick two drinks", listed.SlotLabel);
        Assert.Equal(2, listed.Quantity);

        var deleted = await client.DeleteAsync($"/items/{combo.Id}/combo-components/{slot.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<ItemComboComponentDto>>($"/items/{combo.Id}/combo-components", JsonOptions))!);

        // A slot of another combo can't be reached through this one.
        var other = await ItemAsync(client, "Other Deal", 100m, null, PricingType.Combo);
        var stray = await client.DeleteAsync($"/items/{other.Id}/combo-components/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, stray.StatusCode);
    }

    [Fact]
    public async Task A_combo_cannot_contain_itself_or_another_combo()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var promos = await CategoryAsync(client, "Promos", 1);
        var deal = await ItemAsync(client, "Deal A", 100m, promos.Id, PricingType.Combo);
        var otherDeal = await ItemAsync(client, "Deal B", 100m, promos.Id, PricingType.Combo);

        var self = await client.PostAsJsonAsync($"/items/{deal.Id}/combo-components", new CreateItemComboComponentRequest(promos.Id, "Itself", 1, null, deal.Id));
        var nested = await client.PostAsJsonAsync($"/items/{deal.Id}/combo-components", new CreateItemComboComponentRequest(promos.Id, "Nested", 1, null, otherDeal.Id));

        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, nested.StatusCode);
    }

    [Fact]
    public async Task A_combo_reads_as_out_of_stock_when_an_item_it_always_includes_is_sold_out()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var branchId = (await client.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions))!.Single().Id;
        var meals = await CategoryAsync(client, "Meals", 1);
        var promos = await CategoryAsync(client, "Promos", 2);
        var chicken = await ItemAsync(client, "Fried Chicken", 99m, meals.Id);
        var deal = await ItemAsync(client, "Chicken Buy 1 Take 1", 99m, promos.Id, PricingType.Combo);
        _ = await SlotAsync(client, deal.Id, new CreateItemComboComponentRequest(Guid.Empty, "2 pcs Fried Chicken", 2, null, chicken.Id));

        // A new item starts with no stock, so the deal is unavailable until the chicken is stocked.
        Assert.True((await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions))!.Single(i => i.Id == deal.Id).IsOutOfStock);

        _ = await client.PostAsJsonAsync("/inventory/movements", new RecordMovementRequest(chicken.Id, branchId, MovementType.StockIn, 10m, null, null, null, null));

        Assert.False((await client.GetFromJsonAsync<List<ItemDto>>("/items", JsonOptions))!.Single(i => i.Id == deal.Id).IsOutOfStock);
    }
}
