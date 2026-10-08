export const PIN_MIN = 6;
export const PIN_MAX = 8;

/** Same rule as the API and the Flutter client: 6 to 8 digits for a PIN being set now. Existing 4-digit PINs still sign in. */
export function pinProblem(pin: string): string | null {
  const trimmed = pin.trim();
  if (trimmed === '') return 'Enter a PIN';
  if (trimmed.length < PIN_MIN || trimmed.length > PIN_MAX || !/^\d+$/.test(trimmed)) return `Use ${PIN_MIN} to ${PIN_MAX} digits`;
  return null;
}
