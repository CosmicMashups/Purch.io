import { beforeEach, describe, expect, it, vi } from 'vitest';
import { makeCart } from '../../test/pos';
import { createAddQueue, mergeKey, nextBatch, pendingRows, type DrainContext, type QueuedAdd } from './addQueue';
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

  it('joins every waiting add of the same plain item into one request', () => {
    const waiting = [queued(1, 'Latte', plain('latte')), queued(2, 'Mocha', plain('mocha')), queued(3, 'Latte', plain('latte', 2))];
    const next = nextBatch(waiting)!;
    expect(next.request).toEqual({ itemId: 'latte', itemVariantId: null, quantity: 3 });
    expect(next.batch.map((a) => a.id)).toEqual([1, 3]);
    expect(next.rest.map((a) => a.id)).toEqual([2]);
  });

  it('keeps different choices of one item as separate requests', () => {
    const waiting = [queued(1, 'Latte', { ...plain('latte'), selectedModifierIds: ['a'] }), queued(2, 'Latte', { ...plain('latte'), selectedModifierIds: ['b'] })];
    const next = nextBatch(waiting)!;
    expect(next.batch.map((a) => a.id)).toEqual([1]);
    expect(next.rest.map((a) => a.id)).toEqual([2]);
  });

  it('adds fractional weights together', () => {
    const waiting = [queued(1, 'Rice', plain('rice', 0.25)), queued(2, 'Rice', plain('rice', 0.5))];
    expect(nextBatch(waiting)!.request.quantity).toBe(0.75);
  });
});

describe('pendingRows', () => {
  it('shows one row per item with the total quantity, and nothing priced', () => {
    const rows = pendingRows([queued(1, 'Latte', plain('latte'))], [queued(2, 'Latte', plain('latte', 2)), queued(3, 'Mocha', plain('mocha'))]);
    expect(rows).toEqual([
      { key: 'latte', label: 'Latte', quantity: 3 },
      { key: 'mocha', label: 'Mocha', quantity: 1 },
    ]);
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
    ctx = { send, onCart, onError };
  });

  const settle = () => new Promise((r) => setTimeout(r, 0));

  it('records every tap at once, even while a request is on its way', async () => {
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

  it('sends one request at a time, in order', async () => {
    const order: string[] = [];
    let running = 0;
    let peak = 0;
    send.mockImplementation(async (r: AddLineRequest) => {
      running += 1;
      peak = Math.max(peak, running);
      await new Promise((res) => setTimeout(res, 5));
      order.push(r.itemId);
      running -= 1;
      return makeCart();
    });
    queue.add(ctx, 'A', plain('a'));
    queue.add(ctx, 'B', plain('b'));
    queue.add(ctx, 'C', plain('c'));
    await new Promise((r) => setTimeout(r, 60));
    expect(order).toEqual(['a', 'b', 'c']);
    expect(peak).toBe(1);
  });

  it('turns taps made while one is in flight into a single merged request', async () => {
    const first = deferred();
    send.mockReturnValueOnce(first.promise).mockResolvedValue(makeCart());
    queue.add(ctx, 'Latte', plain('latte'));
    await settle();
    queue.add(ctx, 'Latte', plain('latte'));
    queue.add(ctx, 'Latte', plain('latte'));
    queue.add(ctx, 'Latte', plain('latte'));
    first.resolve(makeCart());
    await new Promise((r) => setTimeout(r, 20));

    expect(send).toHaveBeenCalledTimes(2);
    expect(send).toHaveBeenNthCalledWith(1, plain('latte', 1));
    expect(send).toHaveBeenNthCalledWith(2, plain('latte', 3));
  });

  it('hands each priced cart from the server to the screen', async () => {
    const cart = makeCart({ totalAmount: 150 });
    send.mockResolvedValue(cart);
    queue.add(ctx, 'Latte', plain('latte'));
    await settle();
    expect(onCart).toHaveBeenCalledWith(cart);
    expect(queue.useQueue.getState()).toEqual({ waiting: [], inFlight: [] });
  });

  it('reports a failed add by name and carries on with the rest', async () => {
    send.mockRejectedValueOnce(new Error('sold out')).mockResolvedValue(makeCart());
    queue.add(ctx, 'Latte', plain('latte'));
    queue.add(ctx, 'Mocha', plain('mocha'));
    await new Promise((r) => setTimeout(r, 20));

    expect(onError).toHaveBeenCalledWith(['Latte'], expect.any(Error));
    expect(send).toHaveBeenCalledTimes(2);
    expect(onCart).toHaveBeenCalledTimes(1);
    expect(queue.useQueue.getState().inFlight).toEqual([]);
  });

  it('runs the before-send hook ahead of every request', async () => {
    const calls: string[] = [];
    const beforeSend = vi.fn(async () => void calls.push('before'));
    send.mockImplementation(async () => {
      calls.push('send');
      return makeCart();
    });
    queue.add({ ...ctx, beforeSend }, 'A', plain('a'));
    queue.add({ ...ctx, beforeSend }, 'B', plain('b'));
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
    expect(send).toHaveBeenLastCalledWith(plain('fresh'));
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
