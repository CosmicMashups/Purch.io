using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;

namespace Purch.Application.Promotions;

public sealed class ComboPromoRuleService(
    IComboPromoRuleRepository comboPromoRuleRepository,
    IItemRepository itemRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IComboPromoRuleService
{
    public async Task<IReadOnlyList<ComboPromoRuleDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rules = await comboPromoRuleRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        return [.. rules.OrderBy(rule => rule.Name).Select(ToDto)];
    }

    public async Task<ComboPromoRuleDto> CreateAsync(CreateComboPromoRuleRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(request.Name, request.ItemAId, request.ItemBId, request.ComboPrice, request.StartsAt, request.EndsAt, cancellationToken);

        var rule = new ComboPromoRule
        {
            TenantId = CurrentTenantId,
            Name = request.Name.Trim(),
            ItemAId = request.ItemAId,
            ItemBId = request.ItemBId,
            ComboPrice = request.ComboPrice,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            IsActive = true,
        };

        comboPromoRuleRepository.Add(rule);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(rule);
    }

    public async Task<ComboPromoRuleDto> UpdateAsync(Guid id, UpdateComboPromoRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = await GetOwnedRuleAsync(id, cancellationToken);

        await ValidateAsync(request.Name, request.ItemAId, request.ItemBId, request.ComboPrice, request.StartsAt, request.EndsAt, cancellationToken);

        rule.Name = request.Name.Trim();
        rule.ItemAId = request.ItemAId;
        rule.ItemBId = request.ItemBId;
        rule.ComboPrice = request.ComboPrice;
        rule.StartsAt = request.StartsAt;
        rule.EndsAt = request.EndsAt;
        rule.IsActive = request.IsActive;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(rule);
    }

    private async Task ValidateAsync(
        string name,
        Guid itemAId,
        Guid itemBId,
        decimal comboPrice,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException(nameof(name), "A name is required.");
        }

        if (itemAId == itemBId)
        {
            throw new ValidationException(nameof(itemBId), "A combo needs two different items.");
        }

        if (comboPrice < 0)
        {
            throw new ValidationException(nameof(comboPrice), "Combo price cannot be negative.");
        }

        if (startsAt is { } starts && endsAt is { } ends && starts >= ends)
        {
            throw new ValidationException(nameof(endsAt), "End date must be after the start date.");
        }

        await GetOwnedItemAsync(itemAId, cancellationToken);
        await GetOwnedItemAsync(itemBId, cancellationToken);
    }

    private async Task<Item> GetOwnedItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var item = await itemRepository.GetByIdAsync(itemId, cancellationToken)
            ?? throw new NotFoundException("Item", itemId);

        if (item.TenantId != CurrentTenantId)
        {
            throw new NotFoundException("Item", itemId);
        }

        return item;
    }

    private async Task<ComboPromoRule> GetOwnedRuleAsync(Guid id, CancellationToken cancellationToken)
    {
        var rule = await comboPromoRuleRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("ComboPromoRule", id);

        if (rule.TenantId != CurrentTenantId)
        {
            throw new NotFoundException("ComboPromoRule", id);
        }

        return rule;
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Combo promo rule management requires an authenticated tenant context.");

    private static ComboPromoRuleDto ToDto(ComboPromoRule rule)
    {
        return new(rule.Id, rule.Name, rule.ItemAId, rule.ItemBId, rule.ComboPrice, rule.StartsAt, rule.EndsAt, rule.IsActive);
    }
}
