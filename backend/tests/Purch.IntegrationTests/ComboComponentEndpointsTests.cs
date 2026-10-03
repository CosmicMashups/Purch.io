using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Onboarding;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class ComboComponentEndpointsTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Admin_can_add_a_combo_slot_to_a_combo_priced_item()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var categoryResponse = await client.PostAsJsonAsync(
            "/categories",
            new CreateCategoryRequest("Drinks", 1));
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Value Meal", null, null, null, 150m, null, PricingType.Combo));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var slotResponse = await client.PostAsJsonAsync(
            $"/items/{item!.Id}/combo-components",
            new CreateItemComboComponentRequest(category!.Id, "Choose a Drink", 1, null));

        Assert.Equal(HttpStatusCode.OK, slotResponse.StatusCode);
        var slot = await slotResponse.Content.ReadFromJsonAsync<ItemComboComponentDto>(JsonOptions);
        Assert.Equal("Choose a Drink", slot!.SlotLabel);
        Assert.Equal("Drinks", slot.ComponentCategoryName);

        var listResponse = await client.GetAsync($"/items/{item.Id}/combo-components");
        var slots = await listResponse.Content.ReadFromJsonAsync<List<ItemComboComponentDto>>(JsonOptions);
        _ = Assert.Single(slots!);
    }

    [Fact]
    public async Task Adding_a_combo_slot_to_a_unit_priced_item_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var categoryResponse = await client.PostAsJsonAsync(
            "/categories",
            new CreateCategoryRequest("Sides", 1));
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Plain Burger", null, null, null, 89m, null, PricingType.Unit));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var slotResponse = await client.PostAsJsonAsync(
            $"/items/{item!.Id}/combo-components",
            new CreateItemComboComponentRequest(category!.Id, "Choose a Side", 1, null));

        Assert.Equal(HttpStatusCode.BadRequest, slotResponse.StatusCode);
    }

    [Fact]
    public async Task A_combo_slot_referencing_a_nonexistent_category_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Combo Meal", null, null, null, 175m, null, PricingType.Combo));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var slotResponse = await client.PostAsJsonAsync(
            $"/items/{item!.Id}/combo-components",
            new CreateItemComboComponentRequest(Guid.NewGuid(), "Choose a Dessert", 1, null));

        Assert.Equal(HttpStatusCode.NotFound, slotResponse.StatusCode);
    }

    [Fact]
    public async Task A_combo_slot_with_zero_quantity_is_rejected()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await AuthenticatedAdminClientAsync(factory);

        var categoryResponse = await client.PostAsJsonAsync(
            "/categories",
            new CreateCategoryRequest("Sauces", 1));
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);

        var itemResponse = await client.PostAsJsonAsync(
            "/items",
            new CreateItemRequest("Wing Bucket", null, null, null, 320m, null, PricingType.Combo));
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>(JsonOptions);

        var slotResponse = await client.PostAsJsonAsync(
            $"/items/{item!.Id}/combo-components",
            new CreateItemComboComponentRequest(category!.Id, "Choose a Sauce", 0, null));

        Assert.Equal(HttpStatusCode.BadRequest, slotResponse.StatusCode);
    }

    private static Task<HttpClient> AuthenticatedAdminClientAsync(PurchApiFactory factory) => TestSessions.AdminClientAsync(factory);

}
