import { useMemo } from 'react';
import type { ComboboxGroup } from '../../components/forms/GroupedCombobox';
import { useCategories, useItems } from '../catalog/queries';
import type { Category, Item } from '../catalog/types';
import { useInventoryCategories, useInventoryItems } from './queries';
import type { InventoryCategory, InventoryItem } from './types';

/** A stock movement or transfer line is against either a catalog item or an ingredient; the picker's value says which. */
export type StockRef = { kind: 'item'; id: string } | { kind: 'ingredient'; id: string };

export const itemRef = (id: string) => `item:${id}`;
export const ingredientRef = (id: string) => `ingredient:${id}`;

export function parseStockRef(value: string): StockRef | null {
  const [kind, ...rest] = value.split(':');
  const id = rest.join(':');
  if (id === '' || (kind !== 'item' && kind !== 'ingredient')) return null;
  return { kind, id };
}

/** The two request fields a stock reference becomes. */
export function stockRefFields(value: string): { itemId: string | null; inventoryItemId: string | null } {
  const ref = parseStockRef(value);
  return { itemId: ref?.kind === 'item' ? ref.id : null, inventoryItemId: ref?.kind === 'ingredient' ? ref.id : null };
}

const byName = (a: { label: string }, b: { label: string }) => a.label.localeCompare(b.label);

/**
 * Items and ingredients as picker groups, one per category. Item categories and ingredient categories are separate lists, so a group is
 * labelled with its kind ("Ingredients · Dairy") and a name that exists in both never merges. Ingredients that only mirror an item's own
 * stock are left out; the item is the thing to pick.
 */
export function buildStockGroups(
  items: Item[],
  categories: Category[],
  ingredients: InventoryItem[],
  ingredientCategories: InventoryCategory[],
): ComboboxGroup[] {
  const groups: ComboboxGroup[] = [];

  const itemGroups = new Map<string, ComboboxGroup>();
  for (const category of [...categories].sort((a, b) => a.sortOrder - b.sortOrder)) {
    itemGroups.set(category.id, { label: `Items · ${category.name}`, options: [] });
  }
  const uncategorisedItems: ComboboxGroup = { label: 'Items · Uncategorised', options: [] };
  for (const item of items.filter((i) => i.isActive)) {
    const group = (item.categoryId && itemGroups.get(item.categoryId)) || uncategorisedItems;
    group.options.push({ value: itemRef(item.id), label: item.name, keywords: item.sku ?? undefined });
  }

  const ingredientGroups = new Map<string, ComboboxGroup>();
  for (const category of [...ingredientCategories].sort((a, b) => a.sortOrder - b.sortOrder)) {
    ingredientGroups.set(category.id, { label: `Ingredients · ${category.name}`, options: [] });
  }
  const uncategorisedIngredients: ComboboxGroup = { label: 'Ingredients · Uncategorised', options: [] };
  for (const ingredient of ingredients.filter((i) => i.isActive && !i.isAutoCreatedForItem)) {
    const group = (ingredient.categoryId && ingredientGroups.get(ingredient.categoryId)) || uncategorisedIngredients;
    group.options.push({ value: ingredientRef(ingredient.id), label: ingredient.name, hint: ingredient.baseUnit, keywords: ingredient.sku ?? undefined });
  }

  for (const group of [...itemGroups.values(), uncategorisedItems, ...ingredientGroups.values(), uncategorisedIngredients]) {
    if (group.options.length > 0) groups.push({ ...group, options: group.options.sort(byName) });
  }
  return groups;
}

export function useStockOptions() {
  const items = useItems();
  const categories = useCategories();
  const ingredients = useInventoryItems();
  const ingredientCategories = useInventoryCategories();

  const groups = useMemo(
    () => buildStockGroups(items.data ?? [], categories.data ?? [], ingredients.data ?? [], ingredientCategories.data ?? []),
    [items.data, categories.data, ingredients.data, ingredientCategories.data],
  );
  const failed = [items, ingredients].find((q) => q.isError) ?? null;
  return { groups, ready: !!items.data && !!ingredients.data, failed };
}
