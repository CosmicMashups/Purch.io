import { StockTakeStatus } from './types';

export const stockTakeStatusLabel: Record<StockTakeStatus, { label: string; tone: 'brand' | 'neutral' | 'warn' | 'danger' }> = {
  [StockTakeStatus.Draft]: { label: 'Draft', tone: 'neutral' },
  [StockTakeStatus.Counting]: { label: 'Counting', tone: 'brand' },
  [StockTakeStatus.Review]: { label: 'In review', tone: 'warn' },
  [StockTakeStatus.Applied]: { label: 'Applied', tone: 'brand' },
  [StockTakeStatus.Cancelled]: { label: 'Cancelled', tone: 'neutral' },
};
