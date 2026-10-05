import { act, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AddedToOrderOverlay } from './AddedToOrderOverlay';

const matchMedia = (reduce: boolean) =>
  vi.fn().mockImplementation((query: string) => ({ matches: reduce && query.includes('reduce'), media: query, addEventListener: vi.fn(), removeEventListener: vi.fn() }));

beforeEach(() => vi.useFakeTimers());
afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe('AddedToOrderOverlay', () => {
  it('announces the message and what was added', () => {
    vi.stubGlobal('matchMedia', matchMedia(true));
    render(<AddedToOrderOverlay title="Added to your order" subtitle="2 x Iced Latte" onDone={() => undefined} />);
    expect(screen.getByRole('status')).toHaveTextContent('Added to your order');
    expect(screen.getByText('2 x Iced Latte')).toBeInTheDocument();
  });

  it('with reduced motion shows the finished mark and simply waits a moment before finishing, once', () => {
    vi.stubGlobal('matchMedia', matchMedia(true));
    const onDone = vi.fn();
    render(<AddedToOrderOverlay title="Added to your order" onDone={onDone} />);
    act(() => vi.advanceTimersByTime(1_000));
    expect(onDone).not.toHaveBeenCalled();
    act(() => vi.advanceTimersByTime(300));
    expect(onDone).toHaveBeenCalledTimes(1);
    act(() => vi.advanceTimersByTime(5_000));
    expect(onDone).toHaveBeenCalledTimes(1);
  });

  it('does not finish after it has been removed', () => {
    vi.stubGlobal('matchMedia', matchMedia(true));
    const onDone = vi.fn();
    const { unmount } = render(<AddedToOrderOverlay title="Added to your order" onDone={onDone} />);
    unmount();
    act(() => vi.advanceTimersByTime(5_000));
    expect(onDone).not.toHaveBeenCalled();
  });
});
