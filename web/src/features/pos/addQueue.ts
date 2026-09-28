import { create } from 'zustand';
import { ApiError } from '../../lib/apiError';
import type { AddLineRequest, Transaction } from './types';

/**
 * What the device works out for a tap before the server has answered, from the cached catalog. It is only a
 * preview so the cart can show a price at once: the server prices every line itself and its answer replaces
 * this. Absent when the device cannot price the add (a combo, or catalog data not loaded yet).
 */
export interface AddPreview {
  itemId: string;
  unitPrice: number;
  /** Variant and modifier names, shown under the row like the server's lines. */
  details: string[];
}

/** One tap waiting to reach the server. The label is only for the "adding" row and for error messages. */
export interface QueuedAdd {
  id: number;
  label: string;
  request: AddLineRequest;
  preview?: AddPreview;
}

/** The server takes at most this many lines in one batch. */
export const MAX_BATCH_LINES = 50;

/** A plain add (no variant, combo or modifiers) of the same item is the same as one add of the summed quantity. */
export function mergeKey(request: AddLineRequest): string | null {
  const plain = request.itemVariantId === null && !request.comboSelections?.length && !request.selectedModifierIds?.length;
  return plain ? request.itemId : null;
}

/**
 * What to send next: every waiting add, in the order tapped, as one batch, so a cashier tapping ten products costs one
 * round trip instead of ten. Waiting adds of the same plain item are joined into one line of the summed quantity;
 * anything with choices attached stays its own line, because each combination is its own line. A batch holds at most
 * MAX_BATCH_LINES lines; whatever does not fit waits for the next one.
 */
export function nextBatch(waiting: QueuedAdd[]): { batch: QueuedAdd[]; requests: AddLineRequest[]; rest: QueuedAdd[] } | null {
  if (waiting.length === 0) return null;

  const requests: AddLineRequest[] = [];
  const position = new Map<string, number>();
  let taken = 0;
  for (const add of waiting) {
    const key = mergeKey(add.request);
    const at = key === null ? undefined : position.get(key);
    if (at !== undefined) {
      requests[at] = { ...requests[at], quantity: requests[at].quantity + add.request.quantity };
    } else {
      if (requests.length >= MAX_BATCH_LINES) break;
      if (key !== null) position.set(key, requests.length);
      requests.push(add.request);
    }
    taken += 1;
  }
  return { batch: waiting.slice(0, taken), requests, rest: waiting.slice(taken) };
}

export interface PendingRow {
  key: string;
  label: string;
  quantity: number;
  /** The device's preview price for one unit, when it could work one out. */
  unitPrice?: number;
  details?: string[];
}

/** What the cashier sees while adds are on their way: item and quantity, with the device's preview price when it has one. */
export function pendingRows(inFlight: QueuedAdd[], waiting: QueuedAdd[]): PendingRow[] {
  const rows: PendingRow[] = [];
  for (const add of [...inFlight, ...waiting]) {
    const key = mergeKey(add.request);
    const existing = key === null ? undefined : rows.find((r) => r.key === key);
    if (existing) existing.quantity += add.request.quantity;
    else
      rows.push({
        key: key ?? `add-${add.id}`,
        label: add.label,
        quantity: add.request.quantity,
        ...(add.preview ? { unitPrice: add.preview.unitPrice, details: add.preview.details } : {}),
      });
  }
  return rows;
}

interface QueueState {
  waiting: QueuedAdd[];
  inFlight: QueuedAdd[];
}

export interface DrainContext {
  /** Sends one batch. The id is the same on every retry of that batch, so the server never adds its lines twice. */
  send: (batchId: string, requests: AddLineRequest[]) => Promise<Transaction>;
  /** Called with the server's freshly priced cart after every batch. */
  onCart: (cart: Transaction) => void;
  /** Runs before each batch, so an older cart read still in flight cannot land on top of the newer answer. */
  beforeSend?: () => Promise<void>;
  onError: (labels: string[], error: unknown) => void;
  /** Send a failed batch again under the same id. Only for a server that applies each id once; defaults to on. */
  retry?: boolean;
  /** Pause between retries. Replaceable so tests need not wait. */
  wait?: (ms: number) => Promise<void>;
}

const SEND_ATTEMPTS = 3;
const defaultWait = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

/** A failure that says nothing about the lines themselves: no connection, a cold or busy server, or a duplicate of this very batch. */
function isRetriable(error: unknown): boolean {
  return error instanceof ApiError && ['network', 'serviceUnavailable', 'unknown', 'conflict'].includes(error.kind);
}

function newBatchId(): string {
  if (typeof globalThis.crypto?.randomUUID === 'function') return globalThis.crypto.randomUUID();
  const bytes = globalThis.crypto.getRandomValues(new Uint8Array(16));
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

/**
 * A queue of cart adds. Tapping never waits: an add is recorded at once, and whatever has piled up is sent as one
 * batch, one batch at a time (the server keeps one cart per device, so two at once could race). A batch that
 * fails for a reason unrelated to its lines is sent again under the same id, which is safe because the server
 * applies a given batch id only once. The cart shown is always whatever the server last answered.
 */
export function createAddQueue() {
  const useQueue = create<QueueState>(() => ({ waiting: [], inFlight: [] }));
  let nextId = 1;
  let running = false;
  /** Bumped by reset(), so a request that never finishes cannot keep the queue from starting again. */
  let generation = 0;

  async function sendWithRetries(ctx: DrainContext, requests: AddLineRequest[], mine: number): Promise<Transaction | null> {
    const batchId = newBatchId();
    const wait = ctx.wait ?? defaultWait;
    const attempts = ctx.retry === false ? 1 : SEND_ATTEMPTS;
    for (let attempt = 1; ; attempt++) {
      try {
        await ctx.beforeSend?.();
        return await ctx.send(batchId, requests);
      } catch (error) {
        if (mine !== generation) return null;
        if (!isRetriable(error) || attempt >= attempts) throw error;
        await wait(400 * 2 ** (attempt - 1));
        if (mine !== generation) return null;
      }
    }
  }

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
          const cart = await sendWithRetries(ctx, next.requests, mine);
          if (mine !== generation || cart === null) return;
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
    add(ctx: DrainContext, label: string, request: AddLineRequest, preview?: AddPreview): void {
      useQueue.setState((s) => ({ waiting: [...s.waiting, { id: nextId++, label, request, ...(preview ? { preview } : {}) }] }));
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
