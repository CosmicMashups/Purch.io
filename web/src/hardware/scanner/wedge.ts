/**
 * USB and Bluetooth barcode scanners in keyboard mode type the code and press Enter within a few
 * milliseconds per key. A person cannot type that fast, so a fast run of characters ending in Enter is a scan.
 */
export class WedgeDetector {
  private buffer = '';
  private last = 0;
  private readonly maxGapMs: number;
  private readonly minLength: number;

  constructor(maxGapMs = 50, minLength = 4) {
    this.maxGapMs = maxGapMs;
    this.minLength = minLength;
  }

  /** Feeds one key press. Returns the scanned code when Enter completes a run, otherwise null. */
  push(key: string, at: number): string | null {
    if (key === 'Enter') {
      const code = this.buffer.length >= this.minLength ? this.buffer : null;
      this.reset();
      return code;
    }
    if (key.length !== 1) return null;
    if (this.buffer !== '' && at - this.last > this.maxGapMs) this.buffer = '';
    this.buffer += key;
    this.last = at;
    return null;
  }

  reset(): void {
    this.buffer = '';
  }
}

/** A scan meant for the search box must not also be captured here. */
export function isEditableTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  return target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName);
}
