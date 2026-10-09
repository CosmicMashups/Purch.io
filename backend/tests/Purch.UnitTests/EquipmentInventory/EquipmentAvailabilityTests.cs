using Purch.Application.EquipmentInventory;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.UnitTests.EquipmentInventory;

public sealed class EquipmentAvailabilityTests
{
    private static readonly Guid SundaeId = Guid.NewGuid();
    private static readonly Guid FriesId = Guid.NewGuid();

    private static Equipment Machine(
        EquipmentStatus status,
        bool isActive = true,
        bool isDeleted = false) => new() { Name = "Ice cream machine", Status = status, IsActive = isActive, IsDeleted = isDeleted };

    private static HashSet<Guid> Blocked(Equipment machine, params Guid[] itemIds)
    {
        var links = itemIds.Select(id => new ItemEquipment { ItemId = id, EquipmentId = machine.Id });
        return EquipmentAvailability.BlockedItemIds(links, new Dictionary<Guid, Equipment> { [machine.Id] = machine });
    }

    [Fact]
    public void An_item_is_blocked_while_its_equipment_is_out_of_service()
    {
        var blocked = Blocked(Machine(EquipmentStatus.OutOfService), SundaeId);

        Assert.Equal([SundaeId], blocked);
    }

    [Theory]
    [InlineData(EquipmentStatus.Operational)]
    [InlineData(EquipmentStatus.NeedsRepair)]
    public void Equipment_that_still_works_blocks_nothing(EquipmentStatus status)
    {
        Assert.Empty(Blocked(Machine(status), SundaeId));
    }

    [Fact]
    public void Retired_equipment_no_longer_blocks_the_items_that_used_it()
    {
        Assert.Empty(Blocked(Machine(EquipmentStatus.OutOfService, isActive: false), SundaeId));
        Assert.Empty(Blocked(Machine(EquipmentStatus.OutOfService, isDeleted: true), SundaeId));
    }

    [Fact]
    public void One_broken_machine_blocks_every_item_that_needs_it_and_no_others()
    {
        var broken = Machine(EquipmentStatus.OutOfService);
        var fryer = Machine(EquipmentStatus.Operational);
        var links = new[]
        {
            new ItemEquipment { ItemId = SundaeId, EquipmentId = broken.Id },
            new ItemEquipment { ItemId = FriesId, EquipmentId = fryer.Id },
        };

        var blocked = EquipmentAvailability.BlockedItemIds(
            links,
            new Dictionary<Guid, Equipment> { [broken.Id] = broken, [fryer.Id] = fryer });

        Assert.Equal([SundaeId], blocked);
    }

    [Fact]
    public void An_item_needing_two_machines_is_blocked_when_either_is_down()
    {
        var fryer = Machine(EquipmentStatus.Operational);
        var griller = Machine(EquipmentStatus.OutOfService);
        var links = new[]
        {
            new ItemEquipment { ItemId = SundaeId, EquipmentId = fryer.Id },
            new ItemEquipment { ItemId = SundaeId, EquipmentId = griller.Id },
        };

        var blocked = EquipmentAvailability.BlockedItemIds(
            links,
            new Dictionary<Guid, Equipment> { [fryer.Id] = fryer, [griller.Id] = griller });

        Assert.Equal([SundaeId], blocked);
    }

    [Fact]
    public void A_link_to_equipment_that_is_missing_is_ignored()
    {
        var link = new ItemEquipment { ItemId = SundaeId, EquipmentId = Guid.NewGuid() };

        Assert.Empty(EquipmentAvailability.BlockedItemIds([link], new Dictionary<Guid, Equipment>()));
    }

    [Fact]
    public void No_links_means_nothing_is_blocked()
    {
        Assert.Empty(EquipmentAvailability.BlockedItemIds([], new Dictionary<Guid, Equipment>()));
    }
}
