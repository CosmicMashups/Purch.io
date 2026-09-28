import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../lib/apiError';
import { makeCart } from '../../test/pos';
import { createAddQueue, MAX_BATCH_LINES, mergeKey, nextBatch, pendingRows, type DrainContext, type QueuedAdd } from './addQueue';
import type { AddLineRequest } from './types';

const plain = (itemId: string, quantity = 1): AddLineRequest => ({ itemId, itemVariantId: null, quantity });
const queued = (id: number, label: string, request: AddLineRequest): QueuedAdd => ({ id, label, request });

describe('mergeKey', () => {
  it('lets a plain add merge with others of the same item', () => {
    expect(mergeKey(plain('latte'))).toBe('latte');
  });

  it('never merges an add with a variant, combo picks or modifiers', () => {
    expect(mergeKey({ ...plain('tee'), itemVariantId: 'large' })).toBeNull();
    expect(mergeKey({ ...plain('meal'), comboSelections: [{ slotId: 's', selectedItemId: 'x' }] })).toBeNull();
    expect(mergeKey({ ...plain('latte'), selectedModifierIds: ['no-ice'] })).toBeNull();
  });

  it('treats empty choice lists as plain', () => {
    expect(mergeKey({ ...plain('latte'), comboSelections: [], selectedModifierIds: [] })).toBe('latte');
  });
});

describe('nextBatch', () => {
  it('is nothing when nothing is waiting', () => {
    expect(nextBatch([])).toBeNull();
  });

  it('takes everything waiting, in tap order, joining repeats of the same plain item into one line', () => {
    const waiting = [queued(1, 'Latte', plain('latte')), queued(2, 'Mocha', plain('mocha')), queued(3, 'Latte', plain('latte', 2))];
    const next = nextBatch(waiting)!;
    expect(next.requests).toEqual([plain('latte', 3), plain('mocha')]);
    expect(next.batch.map((a) => a.id)).toEqual([1, 2, 3]);
    expect(next.rest).toEqual([]);
  });

  it('keeps different choices of one item as separate lines', () => {
    const a = { ...plain('latte'), selectedModifierIds: ['a'] };
    const b = { ...plain('latte'), selectedModifierIds: ['b'] };
    expect(nextBatch([queued(1, 'Latte', a), queued(2, 'Latte', b)])!.requests).toEqual([a, b]);
  });

  it('adds fractional weights together', () => {
    const waiting = [queued(1, 'Rice', plain('rice', 0.25)), queued(2, 'Rice', plain('rice', 0.5))];
    expect(nextBatch(waiting)!.requests[0].quantity).toBe(0.75);
  });

  it('leaves whatever exceeds the batch size for the next batch', () => {
    const waiting = Array.from({ length: MAX_BATCH_LINES + 3 }, (_, i) => queued(i + 1, `Item ${i}`, plain(`item-${i}`)));
    const next = nextBatch(waiting)!;
    expect(next.requests).toHaveLength(MAX_BATCH_LINES);
    expect(next.rest.map((a) => a.id)).toEqual([MAX_BATCH_LINES + 1, MAX_BATCH_LINES + 2, MAX_BATCH_LINES + 3]);
  });
});

describe('pendingRows', () => {
  it('shows one row per item with the total quantity', () => {
    const rows = pendingRows([queued(1, 'Latte', plain('latte'))], [queued(2, 'Latte', plain('latte', 2)), queued(3, 'Mocha', plain('mocha'))]);
    expect(rows).toEqual([
      { key: 'latte', label: 'Latte', quantity: 3 },
      { key: 'mocha', label: 'Mocha', quantity: 1 },
    ]);
  });

  it('carries the device preview price and details when the add has one', () => {
    const add: QueuedAdd = { ...queued(1, 'Latte', plain('latte')), preview: { itemId: 'latte', unitPrice: 150, details: ['Oat milk'] } };
    expect(pendingRows([add], [])).toEqual([{ key: 'latte', label: 'Latte', quantity: 1, unitPrice: 150, details: ['Oat milk'] }]);
  });

  it('keeps items with choices on their own rows', () => {
    const rows = pendingRows([], [queued(1, 'Latte', { ...plain('latte'), selectedModifierIds: ['a'] }), queued(2, 'Latte', { ...plain('latte'), selectedModifierIds: ['b'] })]);
    expect(rows).toHaveLength(2);
  });
});

describe('createAddQueue', () => {
  const queue = createAddQueue();
  const deferred = () => {
    let resolve!: (v: ReturnType<typeof makeCart>) => void;
    let reject!: (e: unknown) => void;
    const promise = new Promise<ReturnType<typeof makeCart>>((res, rej) => {
      resolve = res;
      reject = rej;
    });
    return { promise, resolve, reject };
  };

  let send: ReturnType<typeof vi.fn<DrainContext['send']>>;
  let onCart: ReturnType<typeof vi.fn<DrainContext['onCart']>>;
  let onError: ReturnType<typeof vi.fn<DrainContext['onError']>>;
  let ctx: DrainContext;

  beforeEach(() => {
    queue.reset();
    send = vi.fn<DrainContext['send']>();
    onCart = vi.fn<DrainContext['onCart']>();
    onError = vi.fn<DrainContext['onError']>();
    ctx = { send, onCart, onError, wait: async () => undefined };
  });

  const settle = () => new Promise((r) => setTimeout(r, 0));

  it('records every tap at once, even while a batch is on its way', async () => {
    const first = deferred();
    send.mockReturnValueOnce(first.promise);
    queue.add(ctx, 'Latte', plain('latte'));
    queue.add(ctx, 'Mocha', plain('mocha'));
    queue.add(ctx, 'Ube', plain('ube'));
    await settle();

    const { inFlight, waiting } = queue.useQueue.getState();
    expect(inFlight.map((a) => a.label)).toEqual(['Latte']);
    expect(waiting.map((a) => a.label)).toEqual(['Mocha', 'Ube']);
    first.resolve(makeCart());
  });

  it('sends one batch at a time, and taps made meanwhile go together in the next one', async () => {
    const first = deferred();
    send.mockReturnValueOnce(first.promise).mockResolvedValue(makeCart());
    queue.add(ctx, 'A', plain('a'));
    await settle();
    queue.add(ctx, 'B', plain('b'));
    queue.add(ctx, 'C', plain('c'));
    queue.add(ctx, 'B', plain('b'));
    first.resolve(makeCart());
    await new Promise((r) => setTimeout(r, 20));

    expect(send).toHaveBeenCalledTimes(2);
    expect(send).toHaveBeenNthCalledWith(1, expect.any(String), [plain('a')]);
    expect(send).toHaveBeenNthCalledWith(2, expect.any(String), [plain('b', 2), plain('c')]);
  });

  it('gives each batch its own id', async () => {
    send.mockResolvedValue(makeCart());
    queue.add(ctx, 'A', plain('a'));
    await settle();
    queue.add(ctx, 'B', plain('b'));
    await settle();
    expect(send.mock.calls[0][0]).not.toBe(send.mock.calls[1][0]);
  });

  it('hands each priced cart from the server to the screen', async () => {
    const cart = makeCart({ totalAmount: 150 });
    send.mockResolvedValue(cart);
    queue.add(ctx, 'Latte', plain('latte'));
    await settle();
    expect(onCart).toHaveBeenCalledWith(cart);
    expect(queue.useQueue.getState()).toEqual({ waiting: [], inFlight: [] });
  });

  it('reports a batch the server refused by name and carries on with what was tapped after it', async () => {
    const first = deferred();
    send.mockReturnValueOnce(first.promise).mockResolvedValue(makeCart());
    queue.add(ctx, 'Latte', plain('latte'));
    await settle();
    queue.add(ctx, 'Mocha', plain('mocha'));
    first.reject(new ApiError('validation', 'sold out'));
    await new Promise((r) => setTimeout(r, 20));

    expect(onError).toHaveBeenCalledWith(['Latte'], expect.any(ApiError));
    expect(send).toHaveBeenCalledTimes(2);
    expect(onCart).toHaveBeenCalledTimes(1);
    expect(queue.useQueue.getState().inFlight).toEqual([]);
  });

  it('sends a batch again under the same id after a failure that says nothing about its lines', async () => {
    send.mockRejectedValueOnce(new ApiError('network', 'offline')).mockRejectedValueOnce(new ApiError('serviceUnavailable', 'busy')).mockResolvedValue(makeCart({ totalAmount: 150 }));
    queue.add(ctx, 'Latte', plain('latte'));
    await new Promise((r) => setTimeout(r, 20));

    expect(send).toHaveBeenCalledTimes(3);
    expect(new Set(send.mock.calls.map((c) => c[0])).size).toBe(1);
    expect(onError).not.toHaveBeenCalled();
    expect(onCart).toHaveBeenCalledWith(expect.objectContaining({ totalAmount: 150 }));
  });

  it('gives up after three attempts and reports the failure', async () => {
    send.mockRejectedValue(new ApiError('network', 'offline'));
    queue.add(ctx, 'Latte', plain('latte'));
    await new Promise((r) => setTimeout(r, 20));

    expect(send).toHaveBeenCalledTimes(3);
    expect(onError).toHaveBeenCalledWith(['Latte'], expect.any(ApiError));
  });

  it('does not retry a batch the server refused', async () => {
    send.mockRejectedValue(new ApiError('validation', 'not active'));
    queue.add(ctx, 'Latte', plain('latte'));
    await settle();
    expect(send).toHaveBeenCalledTimes(1);
  });

  it('does not retry when retries are switched off', async () => {
    send.mockRejectedValue(new ApiError('network', 'offline'));
    queue.add({ ...ctx, retry: false }, 'Latte', plain('latte'));
    await settle();
    expect(send).toHaveBeenCalledTimes(1);
    expect(onError).toHaveBeenCalled();
  });

  it('runs the before-send hook ahead of every batch', async () => {
    const calls: string[] = [];
    const beforeSend = vi.fn(async () => void calls.push('before'));
    const first = deferred();
    send.mockImplementationOnce(() => {
      calls.push('send');
      return first.promise;
    });
    send.mockImplementation(async () => {
      calls.push('send');
      return makeCart();
    });
    queue.add({ ...ctx, beforeSend }, 'A', plain('a'));
    await settle();
    queue.add({ ...ctx, beforeSend }, 'B', plain('b'));
    first.resolve(makeCart());
    await new Promise((r) => setTimeout(r, 20));
    expect(calls).toEqual(['before', 'send', 'before', 'send']);
  });

  it('can start again after a reset even if a request never finished', async () => {
    send.mockReturnValueOnce(new Promise(() => undefined));
    queue.add(ctx, 'Stuck', plain('stuck'));
    await settle();
    queue.reset();

    send.mockResolvedValue(makeCart({ totalAmount: 60 }));
    queue.add(ctx, 'Fresh', plain('fresh'));
    await settle();
    expect(send).toHaveBeenLastCalledWith(expect.any(String), [plain('fresh')]);
    expect(onCart).toHaveBeenCalledWith(expect.objectContaining({ totalAmount: 60 }));
  });

  it('ignores the answer to a request that was abandoned by a reset', async () => {
    const stale = deferred();
    send.mockReturnValueOnce(stale.promise);
    queue.add(ctx, 'Old', plain('old'));
    await settle();
    queue.reset();
    stale.resolve(makeCart({ totalAmount: 999 }));
    await settle();
    expect(onCart).not.toHaveBeenCalled();
    expect(onError).not.toHaveBeenCalled();
  });

  it('starts over after everything has finished', async () => {
    send.mockResolvedValue(makeCart());
    queue.add(ctx, 'A', plain('a'));
    await settle();
    queue.add(ctx, 'B', plain('b'));
    await settle();
    expect(send).toHaveBeenCalledTimes(2);
  });
});
