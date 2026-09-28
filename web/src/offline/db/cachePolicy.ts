/**
 * What may be kept across a reload: the catalog and branch structure for offline browsing, plus the Home
 * dashboard's own aggregate figures (revenue totals, top sellers, stock counts) so it shows something at
 * once instead of a blank loading screen. Nothing keyed by a customer, a staff member or a single sale is
 * ever stored: no per-staff reports, credit, audit, settings, carts or sale records. A key is allowed by
 * its first part, so a new feature stays out of the cache until it is added here.
 */
const CACHEABLE_ROOTS: ReadonlySet<string> = new Set(['items', 'categories', 'modifierGroups', 'branches', 'departments', 'dashboard']);

export function isCacheableKey(queryKey: readonly unknown[]): boolean {
  return typeof queryKey[0] === 'string' && CACHEABLE_ROOTS.has(queryKey[0]);
}

/** Only successful results are worth keeping; a failed or empty-pending query has nothing to show later. */
export function shouldPersistQuery(query: { queryKey: readonly unknown[]; state: { status: string } }): boolean {
  return query.state.status === 'success' && isCacheableKey(query.queryKey);
}

/** How long saved data may be shown, in milliseconds. Older data is dropped rather than trusted. */
export const CACHE_MAX_AGE_MS = 24 * 60 * 60 * 1000;

/** Bump when the shape of a cached response changes, so old saved data is discarded instead of misread. */
export const CACHE_SCHEMA_VERSION = 'v1';
