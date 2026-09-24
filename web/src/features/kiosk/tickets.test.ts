import { describe, expect, it } from 'vitest';
import { makeCart, makeLine } from '../../test/pos';
import { KitchenStatus } from '../pos/types';
import { actionLabel, byPrepNumber, lineDetails, nextKitchenStatus, splitBoard, statusLabel } from './tickets';

const order = (n: number | null, status: KitchenStatus) => makeCart({ id: `o${n}`, kioskPrepNumber: n, kitchenStatus: status });

describe('kitchen status flow', () => {
  it('moves queued to preparing to ready to picked up, then stops', () => {
    expect(nextKitchenStatus(KitchenStatus.Queued)).toBe(KitchenStatus.Preparing);
    expect(nextKitchenStatus(KitchenStatus.Preparing)).toBe(KitchenStatus.Ready);
    expect(nextKitchenStatus(KitchenStatus.Ready)).toBe(KitchenStatus.PickedUp);
    expect(nextKitchenStatus(KitchenStatus.PickedUp)).toBeNull();
  });

  it('labels each step in the words the kitchen uses', () => {
    expect(actionLabel(KitchenStatus.Queued)).toBe('Start preparing');
    expect(actionLabel(KitchenStatus.Preparing)).toBe('Mark ready');
    expect(actionLabel(KitchenStatus.Ready)).toBe('Picked up');
    expect(actionLabel(KitchenStatus.PickedUp)).toBeNull();
    expect(statusLabel(KitchenStatus.Preparing)).toBe('Preparing');
  });
});

describe('ordering and the board', () => {
  it('works the queue oldest first, with unnumbered orders last', () => {
    expect(byPrepNumber([order(3, 0), order(null, 0), order(1, 0)]).map((o) => o.kioskPrepNumber)).toEqual([1, 3, null]);
  });

  it('does not reorder the list it was given', () => {
    const input = [order(2, 0), order(1, 0)];
    byPrepNumber(input);
    expect(input.map((o) => o.kioskPrepNumber)).toEqual([2, 1]);
  });

  it('splits ready orders from those still being made, and drops picked up ones', () => {
    const { ready, preparing } = splitBoard([order(1, KitchenStatus.Ready), order(2, KitchenStatus.Queued), order(3, KitchenStatus.Preparing), order(4, KitchenStatus.PickedUp)]);
    expect(ready.map((o) => o.kioskPrepNumber)).toEqual([1]);
    expect(preparing.map((o) => o.kioskPrepNumber)).toEqual([2, 3]);
  });
});

describe('lineDetails', () => {
  it('lists variant, combo picks and modifiers, and nothing else', () => {
    const line = makeLine({
      id: 'l',
      itemName: 'Meal',
      itemVariantAttributes: { Size: 'Large' },
      comboSelections: [{ slotId: 's', slotLabel: 'Drink', selectedItemId: 'd', selectedItemName: 'Iced Tea' }],
      modifierSelections: [{ itemModifierId: 'm', modifierName: 'No ice', modifierGroupName: 'Ice', priceDelta: 0 }],
    });
    expect(lineDetails(line)).toEqual(['Large', 'Drink: Iced Tea', 'No ice']);
  });

  it('is empty for a plain line', () => {
    expect(lineDetails(makeLine({ id: 'l', itemName: 'Water' }))).toEqual([]);
  });
});
