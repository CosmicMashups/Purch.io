export const PIN_MIN = 4;
export const PIN_MAX = 8;

/** Same rule as the API and the Flutter client: 4 to 8 digits. */
export function pinProblem(pin: string): string | null {
  const trimmed = pin.trim();
  if (trimmed === '') return 'Enter a PIN';
  if (trimmed.length < PIN_MIN || trimmed.length > PIN_MAX || !/^\d+$/.test(trimmed)) return `Use ${PIN_MIN} to ${PIN_MAX} digits`;
  return null;
}
