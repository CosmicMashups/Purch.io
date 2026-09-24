import { create } from 'zustand';
import type { AddLineRequest, Transaction } from './types';

/** One tap waiting to reach the server. The label is only for the "adding" row and for error messages. */
export interface QueuedAdd {
  id: number;
  label: string;
  request: AddLineRequest;
}

/** A plain add (no variant, combo or modifiers) of the same item is the same as one add of the summed quantity. */
export function mergeKey(request: AddLineRequest): string | null {
  const plain = request.itemVariantId === null && !request.comboSelections?.length && !request.selectedModifierIds?.length;
  return plain ? request.itemId : null;
}

/**
 * What to send next: the oldest waiting add, joined by every other waiting add of the same plain item, so a
 * cashier tapping one product five times costs one round trip instead of five. Anything with choices
 * attached is sent as it is, because each combination is its own line.
 */
export function nextBatch(waiting: QueuedAdd[]): { batch: QueuedAdd[]; request: AddLineRequest; rest: QueuedAdd[] } | null {
  const head = waiting[0];
  if (!head) return null;
  const key = mergeKey(head.request);
  if (key === null) return { batch: [head], request: head.request, rest: waiting.slice(1) };

  const batch = waiting.filter((a) => mergeKey(a.request) === key);
  const rest = waiting.filter((a) => mergeKey(a.request) !== key);
  const quantity = batch.reduce((sum, a) => sum + a.request.quantity, 0);
  return { batch, request: { ...head.request, quantity }, rest };
}

export interface PendingRow {
  key: string;
  label: string;
  quantity: number;
}

/** What the cashier sees while adds are on their way: item and quantity only. The server has not priced them yet. */
export function pendingRows(inFlight: QueuedAdd[], waiting: QueuedAdd[]): PendingRow[] {
  const rows: PendingRow[] = [];
  for (const add of [...inFlight, ...waiting]) {
    const key = mergeKey(add.request);
    const existing = key === null ? undefined : rows.find((r) => r.key === key);
    if (existing) existing.quantity += add.request.quantity;
    else rows.push({ key: key ?? `add-${add.id}`, label: add.label, quantity: add.request.quantity });
  }
  return rows;
}

interface QueueState {
  waiting: QueuedAdd[];
  inFlight: QueuedAdd[];
}

export interface DrainContext {
  send: (request: AddLineRequest) => Promise<Transaction>;
  /** Called with the server's freshly priced cart after every request. */
  onCart: (cart: Transaction) => void;
  /** Runs before each request, so an older cart read still in flight cannot land on top of the newer answer. */
  beforeSend?: () => Promise<void>;
  onError: (labels: string[], error: unknown) => void;
}

/**
 * A queue of cart adds. Tapping never waits: an add is recorded at once and sent in order, one request at a
 * time (the server keeps one cart per device and merges lines, so two at once could race). The cart
 * shown is always whatever the server last answered.
 */
export function createAddQueue() {
  const useQueue = create<QueueState>(() => ({ waiting: [], inFlight: [] }));
  let nextId = 1;
  let running = false;
  /** Bumped by reset(), so a request that never finishes cannot keep the queue from starting again. */
  let generation = 0;

  async function drain(ctx: DrainContext): Promise<void> {
    if (running) return;
    running = true;
    const mine = generation;
    try {
      for (;;) {
        const next = nextBatch(useQueue.getState().waiting);
        if (!next) break;
        useQueue.setState({ waiting: next.rest, inFlight: next.batch });
        try {
          await ctx.beforeSend?.();
          const cart = await ctx.send(next.request);
          if (mine !== generation) return;
          ctx.onCart(cart);
        } catch (error) {
          if (mine !== generation) return;
          ctx.onError([...new Set(next.batch.map((a) => a.label))], error);
        } finally {
          if (mine === generation) useQueue.setState({ inFlight: [] });
        }
      }
    } finally {
      if (mine === generation) running = false;
    }
  }

  return {
    useQueue,
    add(ctx: DrainContext, label: string, request: AddLineRequest): void {
      useQueue.setState((s) => ({ waiting: [...s.waiting, { id: nextId++, label, request }] }));
      void drain(ctx);
    },
    /** For tests and sign-out: forget everything, including a request still on its way, whose answer is ignored. */
    reset(): void {
      generation += 1;
      running = false;
      useQueue.setState({ waiting: [], inFlight: [] });
    },
  };
}
