using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Inventory;
using Purch.Application.Onboarding;
using Purch.Domain.Enums;
using Purch.IntegrationTests.Fixtures;

namespace Purch.IntegrationTests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class IngredientMovementTests(PostgresContainerFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task A_standalone_ingredient_can_be_stocked_in_and_out_from_the_movement_form()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(client);
        var flour = await CreateIngredientAsync(client, "Flour");

        var stockIn = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(null, branchId, MovementType.StockIn, 50m, null, null, null, null, flour.Id));
        Assert.Equal(HttpStatusCode.OK, stockIn.StatusCode);
        var movement = await stockIn.Content.ReadFromJsonAsync<InventoryMovementDto>(JsonOptions);
        Assert.Null(movement!.ItemId);
        Assert.Equal(flour.Id, movement.InventoryItemId);
        Assert.Equal("Flour", movement.ItemName);

        var spoiled = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(null, branchId, MovementType.Consumption, 20m, null, null, null, null, flour.Id));
        Assert.Equal(HttpStatusCode.OK, spoiled.StatusCode);

        var ingredients = await client.GetFromJsonAsync<List<InventoryItemDto>>("/inventory-items", JsonOptions);
        Assert.Equal(30m, ingredients!.Single(i => i.Id == flour.Id).QuantityOnHand);

        var log = await client.GetFromJsonAsync<List<InventoryMovementDto>>($"/inventory/movements?inventoryItemId={flour.Id}", JsonOptions);
        Assert.Equal(2, log!.Count);
    }

    [Fact]
    public async Task An_ingredient_movement_cannot_take_more_than_is_on_hand()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(client);
        var sugar = await CreateIngredientAsync(client, "Sugar");

        var response = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(null, branchId, MovementType.StockOut, 5m, null, null, null, null, sugar.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_movement_needs_exactly_one_of_item_or_ingredient()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var branchId = await MainBranchIdAsync(client);

        var response = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(null, branchId, MovementType.StockIn, 5m, null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_ingredient_transfer_ships_and_receives_like_an_item_transfer()
    {
        await using var factory = new PurchApiFactory(postgres.ConnectionString);
        using var client = await TestSessions.AdminClientAsync(factory);
        var mainBranchId = await MainBranchIdAsync(client);
        var branchResponse = await client.PostAsJsonAsync("/branches", new CreateBranchRequest("Branch 2", null));
        var secondBranchId = (await branchResponse.Content.ReadFromJsonAsync<BranchDto>(JsonOptions))!.Id;
        var butter = await CreateIngredientAsync(client, "Butter");
        _ = await client.PostAsJsonAsync(
            "/inventory/movements",
            new RecordMovementRequest(null, mainBranchId, MovementType.StockIn, 10m, null, null, null, null, butter.Id));

        var created = await client.PostAsJsonAsync(
            "/branch-transfers",
            new CreateBranchTransferRequest(mainBranchId, secondBranchId, [new CreateBranchTransferLineRequest(null, 4m, butter.Id)]));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var transfer = (await created.Content.ReadFromJsonAsync<BranchTransferDto>(JsonOptions))!;
        var line = Assert.Single(transfer.Lines);
        Assert.Equal("Butter", line.ItemName);
        Assert.Equal(butter.Id, line.InventoryItemId);

        _ = await client.PostAsync($"/branch-transfers/{transfer.Id}/mark-in-transit", null);
        var inTransit = await client.GetFromJsonAsync<List<InventoryItemDto>>("/inventory-items", JsonOptions);
        Assert.Equal(6m, inTransit!.Single(i => i.Id == butter.Id).QuantityOnHand);

        _ = await client.PostAsync($"/branch-transfers/{transfer.Id}/mark-received", null);
        var received = await client.GetFromJsonAsync<List<InventoryItemDto>>("/inventory-items", JsonOptions);
        Assert.Equal(10m, received!.Single(i => i.Id == butter.Id).QuantityOnHand);

        var moves = await client.GetFromJsonAsync<List<InventoryMovementDto>>(
            $"/inventory/movements?inventoryItemId={butter.Id}&type={(int)MovementType.Transfer}", JsonOptions);
        Assert.Equal(2, moves!.Count);
    }

    private static async Task<InventoryItemDto> CreateIngredientAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            "/inventory-items",
            new CreateInventoryItemRequest(name, null, "g", "bag", 1000m, null));
        return (await response.Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions))!;
    }

    private static async Task<Guid> MainBranchIdAsync(HttpClient client)
    {
        var branches = await client.GetFromJsonAsync<List<BranchDto>>("/branches", JsonOptions);
        return branches!.Single().Id;
    }
}
