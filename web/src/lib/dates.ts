/** `datetime-local` value ("2026-09-24T10:30", the browser's local time) to an ISO instant, or null when blank. */
export function localInputToIso(value: string | null | undefined): string | null {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

/** ISO instant to a `datetime-local` value in the browser's local time, or '' when absent. */
export function isoToLocalInput(iso: string | null | undefined): string {
  if (!iso) return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

const dateTime = new Intl.DateTimeFormat('en-PH', { dateStyle: 'medium', timeStyle: 'short' });

/** "Sep 24, 2026, 10:30 AM" for display. */
export function formatDateTime(iso: string): string {
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? iso : dateTime.format(date);
}

/** Human summary of a promo's window, e.g. "Always on", "From ...", "Until ...", "... to ...". */
export function describeWindow(startsAt: string | null, endsAt: string | null): string {
  if (!startsAt && !endsAt) return 'Always on';
  if (startsAt && endsAt) return `${formatDateTime(startsAt)} to ${formatDateTime(endsAt)}`;
  return startsAt ? `From ${formatDateTime(startsAt)}` : `Until ${formatDateTime(endsAt as string)}`;
}
