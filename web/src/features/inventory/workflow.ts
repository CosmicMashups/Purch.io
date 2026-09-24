import { BranchTransferStatus, PurchaseOrderStatus, type PurchaseOrderLine } from './types';

type Tone = 'brand' | 'neutral' | 'warn' | 'danger';

export const purchaseOrderStatusLabel: Record<PurchaseOrderStatus, { label: string; tone: Tone }> = {
  [PurchaseOrderStatus.Draft]: { label: 'Draft', tone: 'neutral' },
  [PurchaseOrderStatus.Sent]: { label: 'Sent', tone: 'brand' },
  [PurchaseOrderStatus.PartiallyReceived]: { label: 'Partly received', tone: 'warn' },
  [PurchaseOrderStatus.Received]: { label: 'Received', tone: 'brand' },
  [PurchaseOrderStatus.Cancelled]: { label: 'Cancelled', tone: 'danger' },
};

export const transferStatusLabel: Record<BranchTransferStatus, { label: string; tone: Tone }> = {
  [BranchTransferStatus.Pending]: { label: 'Pending', tone: 'neutral' },
  [BranchTransferStatus.InTransit]: { label: 'In transit', tone: 'warn' },
  [BranchTransferStatus.Received]: { label: 'Received', tone: 'brand' },
  [BranchTransferStatus.Cancelled]: { label: 'Cancelled', tone: 'danger' },
};

export type PurchaseOrderAction = 'send' | 'receive' | 'cancel';
export type TransferAction = 'ship' | 'receive' | 'cancel';

/** What to offer on screen. These mirror the API's transition rules; the API still enforces them. */
export function purchaseOrderActions(status: PurchaseOrderStatus): PurchaseOrderAction[] {
  switch (status) {
    case PurchaseOrderStatus.Draft:
      return ['send', 'cancel'];
    case PurchaseOrderStatus.Sent:
      return ['receive', 'cancel'];
    case PurchaseOrderStatus.PartiallyReceived:
      return ['receive'];
    default:
      return [];
  }
}

export function transferActions(status: BranchTransferStatus): TransferAction[] {
  switch (status) {
    case BranchTransferStatus.Pending:
      return ['ship', 'cancel'];
    case BranchTransferStatus.InTransit:
      return ['receive', 'cancel'];
    default:
      return [];
  }
}

export const isPurchaseOrderOpen = (status: PurchaseOrderStatus): boolean =>
  status === PurchaseOrderStatus.Sent || status === PurchaseOrderStatus.PartiallyReceived;

export const remainingToReceive = (line: PurchaseOrderLine): number => Math.max(0, line.quantityOrdered - line.quantityReceived);

export type ReceiveEntries = Record<string, string>;
export type ReceiveResult =
  | { ok: true; lines: { lineId: string; receivedQuantity: number }[] }
  | { ok: false; lineId?: string; message: string };

/**
 * Blank lines are skipped, because a partial delivery only lists what arrived. Anything typed must be
 * a number above zero, and at least one line is needed, as the API requires.
 */
export function buildReceiveLines(entries: ReceiveEntries): ReceiveResult {
  const lines: { lineId: string; receivedQuantity: number }[] = [];
  for (const [lineId, text] of Object.entries(entries)) {
    const trimmed = text.trim();
    if (trimmed === '') continue;
    const quantity = Number(trimmed);
    if (!Number.isFinite(quantity) || quantity <= 0) return { ok: false, lineId, message: 'Enter a quantity above zero' };
    lines.push({ lineId, receivedQuantity: quantity });
  }
  if (lines.length === 0) return { ok: false, message: 'Enter how many arrived for at least one item' };
  return { ok: true, lines };
}
