import { describe, expect, it } from 'vitest';
import type { Item, ItemComboComponent, ItemVariant, ModifierGroup } from '../catalog/types';
import { buildAddLine, missingOption, slotChoices, toggleModifier, unorderableReason, type OptionPicks, type OptionShape } from './options';

const variant = { id: 'v1', itemId: 'i', attributes: { Size: 'L' }, sku: null, priceOverride: null, imageUrl: null } as ItemVariant;
const slot = { id: 's1', componentCategoryId: 'c', slotLabel: 'Drink', quantity: 2, substitutionUpchargeAmount: 0 } as ItemComboComponent;
const single: ModifierGroup = { id: 'g1', name: 'Sugar', allowMultipleSelection: false, isRequired: true, modifiers: [] };
const multi: ModifierGroup = { id: 'g2', name: 'Add-ons', allowMultipleSelection: true, isRequired: false, modifiers: [] };

const empty: OptionShape = { needsVariant: false, variants: [], slots: [], groups: [] };
const picks = (over: Partial<OptionPicks> = {}): OptionPicks => ({ variantId: null, slots: {}, groups: {}, quantity: 1, ...over });

describe('missingOption', () => {
  it('needs nothing for a plain item', () => {
    expect(missingOption(empty, picks())).toBeNull();
  });

  it('asks for a variant, each combo slot unit, and required modifier groups', () => {
    const shape: OptionShape = { needsVariant: true, variants: [variant], slots: [slot], groups: [single, multi] };
    expect(missingOption(shape, picks())).toBe('Choose a variant');
    expect(missingOption(shape, picks({ variantId: 'v1' }))).toBe('Choose Drink');
    expect(missingOption(shape, picks({ variantId: 'v1', slots: { s1: ['a'] } }))).toBe('Choose Drink');
    expect(missingOption(shape, picks({ variantId: 'v1', slots: { s1: ['a', 'b'] } }))).toBe('Choose Sugar');
    expect(missingOption(shape, picks({ variantId: 'v1', slots: { s1: ['a', 'b'] }, groups: { g1: ['m'] } }))).toBeNull();
  });

  it('never blocks on an optional group', () => {
    expect(missingOption({ ...empty, groups: [multi] }, picks())).toBeNull();
  });

  it('rejects a zero quantity', () => {
    expect(missingOption(empty, picks({ quantity: 0 }))).toBe('Quantity must be above zero');
  });
});

describe('missingOption with explicit groups (the kiosk)', () => {
  const drinks: ModifierGroup = { id: 'g3', name: 'Add drinks', allowMultipleSelection: false, isRequired: false, modifiers: [{ id: 'coke', name: 'Coke Zero', priceDelta: 25 }] };
  const shape: OptionShape = { ...empty, groups: [drinks] };

  it('does not treat an optional group as decided until the customer says so', () => {
    expect(missingOption(shape, picks(), { explicitGroups: true })).toBe('Choose Add drinks');
    expect(missingOption(shape, picks({ groups: { g3: [] } }), { explicitGroups: true })).toBeNull(); // "No drinks"
    expect(missingOption(shape, picks({ groups: { g3: ['coke'] } }), { explicitGroups: true })).toBeNull();
  });

  it('is unchanged for the till, where an optional group stays optional', () => {
    expect(missingOption(shape, picks())).toBeNull();
  });

  it('still needs a real pick from a required group even when it was touched', () => {
    expect(missingOption({ ...empty, groups: [single] }, picks({ groups: { g1: [] } }), { explicitGroups: true })).toBe('Choose Sugar');
  });
});

describe('deals with fixed items', () => {
  const fixed = { id: 'chicken', componentCategoryId: 'meals', slotLabel: '2 pcs Fried Chicken', quantity: 2, substitutionUpchargeAmount: null, componentItemId: 'fried-chicken' } as ItemComboComponent;
  const choose = { id: 'drink', componentCategoryId: 'drinks', slotLabel: 'Choose a drink', quantity: 1, substitutionUpchargeAmount: null, choiceUpcharges: [{ itemId: 'tea', amount: 20 }] } as ItemComboComponent;
  const shape: OptionShape = { ...empty, slots: [fixed, choose] };

  it('asks the customer for nothing on a fixed slot', () => {
    expect(missingOption(shape, picks({ slots: { drink: ['coke'] } }))).toBeNull();
    expect(missingOption(shape, picks())).toBe('Choose Choose a drink');
  });

  it('leaves a fixed slot out of the request, because the server fills it in', () => {
    expect(buildAddLine('deal', shape, picks({ slots: { chicken: ['fried-chicken', 'fried-chicken'], drink: ['coke'] } })).comboSelections).toEqual([{ slotId: 'drink', selectedItemId: 'coke' }]);
  });

  it('lists the choices of a slot with their surcharge and stock, hiding inactive items', () => {
    const catalog = [
      { id: 'coke', name: 'Coke', categoryId: 'drinks', isActive: true, isOutOfStock: false },
      { id: 'tea', name: 'Milk Tea', categoryId: 'drinks', isActive: true, isOutOfStock: true },
      { id: 'old', name: 'Retired', categoryId: 'drinks', isActive: false, isOutOfStock: false },
      { id: 'burger', name: 'Burger', categoryId: 'meals', isActive: true, isOutOfStock: false },
    ] as Item[];
    expect(slotChoices(choose, catalog).map((choice) => [choice.item.id, choice.upcharge, choice.soldOut])).toEqual([
      ['coke', 0, false],
      ['tea', 20, true],
    ]);
  });
});

describe('sold-out choices', () => {
  const soldOutDrinks: ModifierGroup = { id: 'g4', name: 'Pick a drink', allowMultipleSelection: false, isRequired: true, modifiers: [{ id: 'a', name: 'A', priceDelta: 0, isOutOfStock: true }, { id: 'b', name: 'B', priceDelta: 0, isOutOfStock: true }] };

  it('never selects a sold-out modifier', () => {
    const group: ModifierGroup = { ...single, modifiers: [{ id: 'a', name: 'A', priceDelta: 0, isOutOfStock: true }, { id: 'b', name: 'B', priceDelta: 0 }] };
    expect(toggleModifier(group, [], 'a')).toEqual([]);
    expect(toggleModifier(group, [], 'b')).toEqual(['b']);
  });

  it('marks the item unorderable when a required group has nothing left to choose', () => {
    expect(unorderableReason({ ...empty, groups: [soldOutDrinks] }, [])).toBe('Pick a drink is sold out');
    expect(unorderableReason({ ...empty, groups: [{ ...soldOutDrinks, isRequired: false }] }, [])).toBeNull();
  });

  it('marks a deal unorderable when every choice in a slot is sold out', () => {
    const slotOfSoldOut = { id: 's9', componentCategoryId: 'drinks', slotLabel: 'Choose a drink', quantity: 1, substitutionUpchargeAmount: null } as ItemComboComponent;
    const catalog = [{ id: 'tea', name: 'Milk Tea', categoryId: 'drinks', isActive: true, isOutOfStock: true }] as Item[];
    expect(unorderableReason({ ...empty, slots: [slotOfSoldOut] }, catalog)).toBe('Choose a drink is sold out');
  });
});

describe('toggleModifier', () => {
  it('replaces the pick in a single-select group', () => {
    expect(toggleModifier(single, ['a'], 'b')).toEqual(['b']);
  });

  it('adds to and removes from a multi-select group', () => {
    expect(toggleModifier(multi, ['a'], 'b')).toEqual(['a', 'b']);
    expect(toggleModifier(multi, ['a', 'b'], 'a')).toEqual(['b']);
  });

  it('lets a single-select pick be cleared by tapping it again', () => {
    expect(toggleModifier(single, ['a'], 'a')).toEqual([]);
  });
});

describe('buildAddLine', () => {
  it('sends only what was chosen', () => {
    expect(buildAddLine('i', empty, picks({ quantity: 3 }))).toEqual({ itemId: 'i', itemVariantId: null, quantity: 3 });
  });

  it('sends a variant, one entry per combo unit, and all modifier ids', () => {
    const shape: OptionShape = { needsVariant: true, variants: [variant], slots: [slot], groups: [single, multi] };
    expect(buildAddLine('i', shape, picks({ variantId: 'v1', slots: { s1: ['a', 'b'] }, groups: { g1: ['m1'], g2: ['m2', 'm3'] } }))).toEqual({
      itemId: 'i',
      itemVariantId: 'v1',
      quantity: 1,
      comboSelections: [
        { slotId: 's1', selectedItemId: 'a' },
        { slotId: 's1', selectedItemId: 'b' },
      ],
      selectedModifierIds: ['m1', 'm2', 'm3'],
    });
  });
});
