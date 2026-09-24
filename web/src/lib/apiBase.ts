/** Where the API lives. In dev, /api is proxied by Vite so the browser never makes a cross-origin call. */
export const API_BASE_URL: string = import.meta.env.VITE_API_BASE_URL ?? (import.meta.env.DEV ? '/api' : 'https://purch-io-backend.vercel.app');
