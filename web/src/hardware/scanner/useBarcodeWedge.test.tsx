import { fireEvent, render } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { useBarcodeWedge } from './useBarcodeWedge';

function Harness({ onScan, enabled = true }: { onScan: (code: string) => void; enabled?: boolean }) {
  useBarcodeWedge(onScan, enabled);
  return (
    <div>
      <input aria-label="search" />
      <button type="button">ok</button>
    </div>
  );
}

function scan(target: Element | Document, code: string) {
  for (const key of code) fireEvent.keyDown(target, { key });
  fireEvent.keyDown(target, { key: 'Enter' });
}

describe('useBarcodeWedge', () => {
  it('reports a scan typed at the page', () => {
    const onScan = vi.fn();
    render(<Harness onScan={onScan} />);
    scan(document.body, '4800016001234');
    expect(onScan).toHaveBeenCalledWith('4800016001234');
  });

  it('leaves keys typed into a field to that field', () => {
    const onScan = vi.fn();
    const { getByLabelText } = render(<Harness onScan={onScan} />);
    scan(getByLabelText('search'), '4800016001234');
    expect(onScan).not.toHaveBeenCalled();
  });

  it('ignores shortcuts', () => {
    const onScan = vi.fn();
    render(<Harness onScan={onScan} />);
    for (const key of '48000160') fireEvent.keyDown(document.body, { key, ctrlKey: true });
    fireEvent.keyDown(document.body, { key: 'Enter', ctrlKey: true });
    expect(onScan).not.toHaveBeenCalled();
  });

  it('does nothing while switched off', () => {
    const onScan = vi.fn();
    render(<Harness onScan={onScan} enabled={false} />);
    scan(document.body, '4800016001234');
    expect(onScan).not.toHaveBeenCalled();
  });

  it('uses the latest callback without listening twice', () => {
    const first = vi.fn();
    const second = vi.fn();
    const { rerender } = render(<Harness onScan={first} />);
    rerender(<Harness onScan={second} />);
    scan(document.body, 'ABCD1234');
    expect(first).not.toHaveBeenCalled();
    expect(second).toHaveBeenCalledTimes(1);
  });
});
