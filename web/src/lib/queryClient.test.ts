import { beforeEach, describe, expect, it } from 'vitest';
import { useToastStore } from '../components/feedback/toastStore';
import { ApiError } from './apiError';
import { notifyMutationError, queryClient } from './queryClient';

describe('notifyMutationError', () => {
  beforeEach(() => useToastStore.setState({ toasts: [] }));

  it('toasts a friendly message for a failed save', () => {
    notifyMutationError(new ApiError('conflict', 'Barcode already in use'), undefined);
    expect(useToastStore.getState().toasts[0]).toMatchObject({ tone: 'error', message: 'Barcode already in use' });
  });

  it('does not leak raw server text', () => {
    notifyMutationError(new ApiError('unknown', 'System.Exception: boom'), undefined);
    expect(useToastStore.getState().toasts[0].message).toBe('Something went wrong. Please try again.');
  });

  it('stays quiet for silent mutations and expired sessions', () => {
    notifyMutationError(new ApiError('validation', 'x'), true);
    notifyMutationError(new ApiError('unauthorized', 'x'), undefined);
    expect(useToastStore.getState().toasts).toHaveLength(0);
  });
});

describe('queryClient defaults', () => {
  it('never queues a save while offline: it must fail now instead of replaying later', () => {
    expect(queryClient.getDefaultOptions().mutations?.networkMode).toBe('always');
  });

  it('keeps data in memory at least as long as it may be saved for offline use', () => {
    expect(queryClient.getDefaultOptions().queries?.gcTime).toBe(24 * 60 * 60 * 1000);
  });
});
