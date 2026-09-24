const peso = new Intl.NumberFormat('en-PH', { style: 'currency', currency: 'PHP' });
const pesoCompact = new Intl.NumberFormat('en-PH', {
  style: 'currency',
  currency: 'PHP',
  notation: 'compact',
  maximumFractionDigits: 1,
});

export const formatPeso = (amount: number): string => peso.format(amount);
export const formatPesoCompact = (amount: number): string => pesoCompact.format(amount);

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** "2026-09-24" -> "Sep 24". Parsed as text so the browser's timezone can never shift the day. */
export function formatDay(isoDate: string): string {
  const [, month, day] = isoDate.split('-');
  const name = MONTHS[Number(month) - 1];
  return name && day ? `${name} ${Number(day)}` : isoDate;
}

export function greetingFor(hour: number): string {
  if (hour < 12) return 'Good morning';
  if (hour < 18) return 'Good afternoon';
  return 'Good evening';
}

/** Stock on hand as a share of its own alert threshold, clamped to 0..1. A zero threshold reads as empty. */
export function stockRatio(stockOnHand: number, threshold: number): number {
  if (threshold <= 0) return 0;
  return Math.min(1, Math.max(0, stockOnHand / threshold));
}
