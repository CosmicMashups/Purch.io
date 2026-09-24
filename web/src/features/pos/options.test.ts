import { describe, expect, it } from 'vitest';
import type { ItemComboComponent, ItemVariant, ModifierGroup } from '../catalog/types';
import { buildAddLine, missingOption, toggleModifier, type OptionPicks, type OptionShape } from './options';

const variant = { id: 'v1', itemId: 'i', attributes: { Size: 'L' }, sku: null, priceOverride: null, imageUrl: null } as ItemVariant;
const slot = { id: 's1', itemId: 'i', componentCategoryId: 'c', slotLabel: 'Drink', quantity: 2, substitutionUpchargeAmount: 0 } as ItemComboComponent;
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
