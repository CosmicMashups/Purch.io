/** Saves text as a file in the browser. A byte-order mark is added to CSV so Excel reads pesos and accents correctly. */
export function downloadTextFile(filename: string, text: string, mime = 'text/csv;charset=utf-8'): void {
  const body = mime.startsWith('text/csv') ? `﻿${text}` : text;
  const url = URL.createObjectURL(new Blob([body], { type: mime }));
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

/** Data rows in a CSV that has a header line. Blank lines are ignored. */
export function csvRowCount(text: string): number {
  const lines = text.split(/\r?\n/).filter((line) => line.trim() !== '');
  return Math.max(0, lines.length - 1);
}

/** `low-stock-2026-09-24.csv` */
export function datedFilename(prefix: string, day: string, extension = 'csv'): string {
  return `${prefix}-${day}.${extension}`;
}
