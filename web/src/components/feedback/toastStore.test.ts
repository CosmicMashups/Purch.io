import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { toast, useToastStore } from './toastStore';

describe('toastStore', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    useToastStore.setState({ toasts: [] });
  });
  afterEach(() => vi.useRealTimers());

  it('shows a toast and removes it after its lifetime', () => {
    toast.success('Saved');
    expect(useToastStore.getState().toasts).toHaveLength(1);
    vi.advanceTimersByTime(4001);
    expect(useToastStore.getState().toasts).toHaveLength(0);
  });

  it('keeps errors on screen longer than successes', () => {
    toast.error('Failed');
    vi.advanceTimersByTime(4001);
    expect(useToastStore.getState().toasts).toHaveLength(1);
  });

  it('caps the stack at four toasts', () => {
    for (let i = 0; i < 6; i++) toast.info(`n${i}`);
    expect(useToastStore.getState().toasts).toHaveLength(4);
    expect(useToastStore.getState().toasts[3].message).toBe('n5');
  });
});
