import { API_BASE_URL } from './apiBase';

/** Only the pictures the app uses are bundled, so unused artwork never ends up in the download. */
const bundled = import.meta.glob(['../assets/logo.jpg', '../assets/wordmark-trimmed.png', '../assets/images/{combo_rice_bowl,beverage_iced_latte,kiosk_poster_default,sample_qr_ph}.jpg'], { eager: true, query: '?url', import: 'default' }) as Record<string, string>;

/** The pictures shipped with the app, by the same `assets/...` path the Flutter client stores in the database. */
const byPath = new Map(Object.entries(bundled).map(([file, url]) => [file.replace('../assets/', 'assets/'), url]));

const need = (path: string): string => byPath.get(path) ?? '';

export const BUNDLED = {
  logo: need('assets/logo.jpg'),
  /** The wordmark trimmed to its lettering, so it can be sized by its height. */
  wordmark: need('assets/wordmark-trimmed.png'),
  kioskPoster: need('assets/images/kiosk_poster_default.jpg'),
} as const;

/** Values a form can store to point at a bundled picture. Same strings as the Flutter client, so both read them. */
export const SAMPLE_IMAGE = {
  riceBowl: 'assets/images/combo_rice_bowl.jpg',
  icedLatte: 'assets/images/beverage_iced_latte.jpg',
  kioskPoster: 'assets/images/kiosk_poster_default.jpg',
  gcashQr: 'assets/images/sample_qr_ph.jpg',
} as const;

/**
 * Turns a stored image value into something a browser can load:
 * a bundled path (`assets/...`), a hosted upload (`/uploads/...`, served by the API) or a full URL.
 * An unknown bundled path gives null, so the caller shows its fallback instead of a broken image.
 */
export function resolveImage(source: string | null | undefined): string | null {
  const src = source?.trim();
  if (!src) return null;
  if (src.startsWith('assets/')) return byPath.get(src) ?? null;
  if (/^(https?:|data:|blob:)/i.test(src)) return src;
  if (src.startsWith('/')) return `${API_BASE_URL.replace(/\/+$/, '')}${src}`;
  return src;
}

/** The sample pictures offered beside the upload button on item and category forms. */
export const ITEM_SAMPLES = [
  { label: 'Rice Bowl', value: SAMPLE_IMAGE.riceBowl },
  { label: 'Iced Latte', value: SAMPLE_IMAGE.icedLatte },
];
