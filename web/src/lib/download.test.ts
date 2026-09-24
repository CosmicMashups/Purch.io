import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { csvRowCount, datedFilename, downloadTextFile } from './download';

describe('csvRowCount', () => {
  it('counts data rows, not the header, and ignores blank lines', () => {
    expect(csvRowCount('Item,Stock\r\nMilk,2\r\nBeans,0\r\n')).toBe(2);
    expect(csvRowCount('Item,Stock\n\n')).toBe(0);
    expect(csvRowCount('')).toBe(0);
  });
});

describe('datedFilename', () => {
  it('builds a dated name', () => {
    expect(datedFilename('low-stock', '2026-09-24')).toBe('low-stock-2026-09-24.csv');
  });
});

describe('downloadTextFile', () => {
  const created: Blob[] = [];
  let click: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    created.length = 0;
    URL.createObjectURL = vi.fn((blob: Blob | MediaSource) => {
      created.push(blob as Blob);
      return 'blob:test';
    });
    URL.revokeObjectURL = vi.fn();
    click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
  });
  afterEach(() => vi.restoreAllMocks());

  it('downloads the text under the given file name and releases the URL', async () => {
    downloadTextFile('sales.csv', 'a,b\n1,2');
    expect(click).toHaveBeenCalledTimes(1);
    expect((click.mock.contexts[0] as HTMLAnchorElement).download).toBe('sales.csv');
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:test');
    expect(document.querySelector('a[download]')).toBeNull();
    const bytes = new Uint8Array(await created[0].arrayBuffer());
    expect([...bytes.slice(0, 3)]).toEqual([0xef, 0xbb, 0xbf]);
    expect(new TextDecoder('utf-8', { ignoreBOM: true }).decode(bytes)).toBe('﻿a,b\n1,2');
  });

  it('does not add a byte-order mark to other file types', async () => {
    downloadTextFile('note.txt', 'hello', 'text/plain');
    expect(await created[0].text()).toBe('hello');
  });
});
