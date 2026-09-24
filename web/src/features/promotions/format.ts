import { formatPeso } from '../dashboard/format';
import type { Item } from '../catalog/types';
import { PromoDiscountType } from './types';

export function describeDiscount(type: PromoDiscountType, value: number): string {
  switch (type) {
    case PromoDiscountType.Percentage:
      return `${value}% off`;
    case PromoDiscountType.FixedAmount:
      return `${formatPeso(value)} off`;
    case PromoDiscountType.FixedPrice:
      return `Priced at ${formatPeso(value)}`;
  }
}

export function itemNameOf(items: Item[], id: string): string {
  return items.find((i) => i.id === id)?.name ?? 'Removed item';
}
