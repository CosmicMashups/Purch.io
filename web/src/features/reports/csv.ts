/** Quotes a CSV cell. A cell starting with one of these characters is run as a formula when the file is opened in a
 * spreadsheet, and names, reasons and category names are user-entered, so it is defused with a leading quote. */
export function csvField(value: string | number | null | undefined): string {
  if (value === null || value === undefined) return '';
  let text = String(value);
  if (/^[=+\-@\t\r]/.test(text)) text = `'${text}`;
  return /[",\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

/** Builds a CSV from the figures already on screen, so the file always matches the report and costs no request. */
export function toCsv(header: string[], rows: (string | number | null | undefined)[][]): string {
  return [header, ...rows].map((row) => row.map(csvField).join(',')).join('\n');
}
