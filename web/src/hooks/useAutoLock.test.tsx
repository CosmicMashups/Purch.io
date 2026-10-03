import { act, fireEvent, render } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useAutoLock } from './useAutoLock';

function Probe({ enabled, onLock }: { enabled: boolean; onLock: () => void }) {
  useAutoLock(enabled, onLock, 1000);
  return null;
}

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

describe('useAutoLock', () => {
  it('locks once the screen has been left alone for the idle time', () => {
    const onLock = vi.fn();
    render(<Probe enabled onLock={onLock} />);
    act(() => vi.advanceTimersByTime(999));
    expect(onLock).not.toHaveBeenCalled();
    act(() => vi.advanceTimersByTime(2));
    expect(onLock).toHaveBeenCalledTimes(1);
  });

  it('taps and key presses start the wait again', () => {
    const onLock = vi.fn();
    render(<Probe enabled onLock={onLock} />);
    act(() => vi.advanceTimersByTime(800));
    fireEvent.pointerDown(window);
    act(() => vi.advanceTimersByTime(800));
    fireEvent.keyDown(window, { key: '1' });
    act(() => vi.advanceTimersByTime(800));
    expect(onLock).not.toHaveBeenCalled();
    act(() => vi.advanceTimersByTime(300));
    expect(onLock).toHaveBeenCalledTimes(1);
  });

  it('does nothing when it is not switched on, and stops when it is switched off', () => {
    const onLock = vi.fn();
    const { rerender } = render(<Probe enabled={false} onLock={onLock} />);
    act(() => vi.advanceTimersByTime(5000));
    expect(onLock).not.toHaveBeenCalled();

    rerender(<Probe enabled onLock={onLock} />);
    rerender(<Probe enabled={false} onLock={onLock} />);
    act(() => vi.advanceTimersByTime(5000));
    expect(onLock).not.toHaveBeenCalled();
  });
});
