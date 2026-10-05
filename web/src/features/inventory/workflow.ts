import { BranchTransferStatus, PurchaseOrderStatus, ReceivingCondition, ReceivingRemark } from './types';

type Tone = 'brand' | 'neutral' | 'warn' | 'danger';

export const purchaseOrderStatusLabel: Record<PurchaseOrderStatus, { label: string; tone: Tone }> = {
  [PurchaseOrderStatus.Draft]: { label: 'Draft', tone: 'neutral' },
  [PurchaseOrderStatus.Sent]: { label: 'Submitted', tone: 'brand' },
  [PurchaseOrderStatus.PartiallyReceived]: { label: 'Partially delivered', tone: 'warn' },
  [PurchaseOrderStatus.Received]: { label: 'Delivered', tone: 'brand' },
  [PurchaseOrderStatus.Cancelled]: { label: 'Cancelled', tone: 'danger' },
};

export const transferStatusLabel: Record<BranchTransferStatus, { label: string; tone: Tone }> = {
  [BranchTransferStatus.Pending]: { label: 'Pending', tone: 'neutral' },
  [BranchTransferStatus.InTransit]: { label: 'In transit', tone: 'warn' },
  [BranchTransferStatus.Received]: { label: 'Received', tone: 'brand' },
  [BranchTransferStatus.Cancelled]: { label: 'Cancelled', tone: 'danger' },
};

export type PurchaseOrderAction = 'send' | 'record-delivery' | 'cancel';
export type TransferAction = 'ship' | 'receive' | 'cancel';

/** What to offer on screen. These mirror the API's transition rules; the API still enforces them. */
export function purchaseOrderActions(status: PurchaseOrderStatus): PurchaseOrderAction[] {
  switch (status) {
    case PurchaseOrderStatus.Draft:
      return ['send', 'cancel'];
    case PurchaseOrderStatus.Sent:
      return ['record-delivery', 'cancel'];
    case PurchaseOrderStatus.PartiallyReceived:
      return ['record-delivery'];
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

export const conditionLabel: Record<ReceivingCondition, string> = {
  [ReceivingCondition.Good]: 'Good',
  [ReceivingCondition.NotGood]: 'Not good',
};

export const remarkLabel: Record<ReceivingRemark, { label: string; tone: Tone }> = {
  [ReceivingRemark.Accepted]: { label: 'Accepted', tone: 'brand' },
  [ReceivingRemark.Rejected]: { label: 'Rejected', tone: 'danger' },
};
