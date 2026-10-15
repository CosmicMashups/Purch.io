import { describe, expect, it } from 'vitest';
import { csvField, toCsv } from './csv';

describe('csvField', () => {
  it('leaves plain text alone and quotes commas, quotes and newlines', () => {
    expect(csvField('Bottled water')).toBe('Bottled water');
    expect(csvField('Chips, large')).toBe('"Chips, large"');
    expect(csvField('say "hi"')).toBe('"say ""hi"""');
  });

  it('defuses a cell a spreadsheet would run as a formula', () => {
    expect(csvField('=SUM(A1)')).toBe("'=SUM(A1)");
    expect(csvField('@cmd')).toBe("'@cmd");
  });

  it('writes null and undefined as empty cells and keeps numbers exact', () => {
    expect(toCsv(['a', 'b', 'c'], [[null, undefined, 12.5]])).toBe('a,b,c\n,,12.5');
  });
});
