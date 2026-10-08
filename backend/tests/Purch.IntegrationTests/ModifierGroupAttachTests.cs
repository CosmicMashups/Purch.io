using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Catalog;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class ModifierGroupAttachTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task<ItemDto> ItemAsync(HttpClient client, string name, Guid? categoryId) =>
        (await (await client.PostAsJsonAsync("/items", new CreateItemRequest(name, null, null, categoryId, 50m, null, PricingType.Unit))).Content.ReadFromJsonAsync<ItemDto>(JsonOptions))!;

    private static async Task<int> GroupCountAsync(HttpClient client, Guid itemId) =>
        (await client.GetFromJsonAsync<List<ModifierGroupDto>>($"/items/{itemId}/modifier-groups", JsonOptions))!.Count;

    [Fact]
    public async Task A_group_can_be_attached_to_every_item_of_a_category_and_only_those()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var drinks = (await (await client.PostAsJsonAsync("/categories", new CreateCategoryRequest("Drinks", 0))).Content.ReadFromJsonAsync<CategoryDto>(JsonOptions))!;
        var tea = await ItemAsync(client, "Tea", drinks.Id);
        var coffee = await ItemAsync(client, "Coffee", drinks.Id);
        var cake = await ItemAsync(client, "Cake", null);
        var group = (await (await client.PostAsJsonAsync("/modifier-groups", new CreateModifierGroupRequest("Sweetness", false, true))).Content.ReadFromJsonAsync<ModifierGroupDto>(JsonOptions))!;

        var response = await client.PostAsJsonAsync($"/modifier-groups/{group.Id}/attach-items", new AttachModifierGroupToItemsRequest(CategoryId: drinks.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<AttachModifierGroupToItemsResult>(JsonOptions))!;
        Assert.Equal(2, result.Attached);
        Assert.Equal(1, await GroupCountAsync(client, tea.Id));
        Assert.Equal(1, await GroupCountAsync(client, coffee.Id));
        Assert.Equal(0, await GroupCountAsync(client, cake.Id));
    }

    [Fact]
    public async Task A_group_can_be_attached_to_chosen_items_and_attaching_again_skips_those_that_have_it()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var tea = await ItemAsync(client, "Tea", null);
        var coffee = await ItemAsync(client, "Coffee", null);
        var cake = await ItemAsync(client, "Cake", null);
        var group = (await (await client.PostAsJsonAsync("/modifier-groups", new CreateModifierGroupRequest("Sweetness", false, true))).Content.ReadFromJsonAsync<ModifierGroupDto>(JsonOptions))!;
        _ = await client.PostAsJsonAsync($"/modifier-groups/{group.Id}/attach-items", new AttachModifierGroupToItemsRequest(ItemIds: [tea.Id]));

        var again = await client.PostAsJsonAsync($"/modifier-groups/{group.Id}/attach-items", new AttachModifierGroupToItemsRequest(ItemIds: [tea.Id, coffee.Id]));

        var result = (await again.Content.ReadFromJsonAsync<AttachModifierGroupToItemsResult>(JsonOptions))!;
        Assert.Equal(1, result.Attached);
        Assert.Equal(1, result.AlreadyAttached);
        Assert.Equal(1, await GroupCountAsync(client, coffee.Id));
        Assert.Equal(0, await GroupCountAsync(client, cake.Id));
    }

    [Fact]
    public async Task Choosing_both_a_category_and_items_or_neither_is_refused()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var item = await ItemAsync(client, "Tea", null);
        var group = (await (await client.PostAsJsonAsync("/modifier-groups", new CreateModifierGroupRequest("Sweetness", false, true))).Content.ReadFromJsonAsync<ModifierGroupDto>(JsonOptions))!;

        var neither = await client.PostAsJsonAsync($"/modifier-groups/{group.Id}/attach-items", new AttachModifierGroupToItemsRequest());
        var both = await client.PostAsJsonAsync($"/modifier-groups/{group.Id}/attach-items", new AttachModifierGroupToItemsRequest(Guid.NewGuid(), [item.Id]));

        Assert.Equal(HttpStatusCode.BadRequest, neither.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, both.StatusCode);
    }
}
