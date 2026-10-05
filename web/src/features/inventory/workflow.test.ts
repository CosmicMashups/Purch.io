import { describe, expect, it } from 'vitest';
import { BranchTransferStatus, PurchaseOrderStatus } from './types';
import { purchaseOrderActions, purchaseOrderStatusLabel, transferActions } from './workflow';

describe('purchaseOrderActions', () => {
  it('follows the API transition rules', () => {
    expect(purchaseOrderActions(PurchaseOrderStatus.Draft)).toEqual(['send', 'cancel']);
    expect(purchaseOrderActions(PurchaseOrderStatus.Sent)).toEqual(['record-delivery', 'cancel']);
    expect(purchaseOrderActions(PurchaseOrderStatus.PartiallyReceived)).toEqual(['record-delivery']);
    expect(purchaseOrderActions(PurchaseOrderStatus.Received)).toEqual([]);
    expect(purchaseOrderActions(PurchaseOrderStatus.Cancelled)).toEqual([]);
  });
});

describe('transferActions', () => {
  it('follows the API transition rules', () => {
    expect(transferActions(BranchTransferStatus.Pending)).toEqual(['ship', 'cancel']);
    expect(transferActions(BranchTransferStatus.InTransit)).toEqual(['receive', 'cancel']);
    expect(transferActions(BranchTransferStatus.Received)).toEqual([]);
    expect(transferActions(BranchTransferStatus.Cancelled)).toEqual([]);
  });
});

describe('purchaseOrderStatusLabel', () => {
  it('uses the delivery wording', () => {
    expect(purchaseOrderStatusLabel[PurchaseOrderStatus.Sent].label).toBe('Submitted');
    expect(purchaseOrderStatusLabel[PurchaseOrderStatus.PartiallyReceived].label).toBe('Partially delivered');
    expect(purchaseOrderStatusLabel[PurchaseOrderStatus.Received].label).toBe('Delivered');
  });
});
