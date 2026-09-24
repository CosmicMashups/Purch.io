import { describe, expect, it } from 'vitest';
import { BranchTransferStatus, PurchaseOrderStatus } from './types';
import { buildReceiveLines, purchaseOrderActions, remainingToReceive, transferActions } from './workflow';

describe('purchaseOrderActions', () => {
  it('follows the API transition rules', () => {
    expect(purchaseOrderActions(PurchaseOrderStatus.Draft)).toEqual(['send', 'cancel']);
    expect(purchaseOrderActions(PurchaseOrderStatus.Sent)).toEqual(['receive', 'cancel']);
    expect(purchaseOrderActions(PurchaseOrderStatus.PartiallyReceived)).toEqual(['receive']);
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

describe('remainingToReceive', () => {
  it('is what is still outstanding and never negative', () => {
    const line = { id: 'l', itemId: 'i', itemName: 'X', quantityOrdered: 10, quantityReceived: 4, expectedUnitCost: 1 };
    expect(remainingToReceive(line)).toBe(6);
    expect(remainingToReceive({ ...line, quantityReceived: 12 })).toBe(0);
  });
});

describe('buildReceiveLines', () => {
  it('skips blank lines for a partial delivery', () => {
    expect(buildReceiveLines({ a: '3', b: '' })).toEqual({ ok: true, lines: [{ lineId: 'a', receivedQuantity: 3 }] });
  });

  it('requires at least one line', () => {
    expect(buildReceiveLines({ a: '', b: ' ' })).toEqual({ ok: false, message: 'Enter how many arrived for at least one item' });
  });

  it('rejects zero, negative and non-numeric quantities', () => {
    expect(buildReceiveLines({ a: '0' })).toMatchObject({ ok: false, lineId: 'a' });
    expect(buildReceiveLines({ a: '-1' })).toMatchObject({ ok: false, lineId: 'a' });
    expect(buildReceiveLines({ a: 'x' })).toMatchObject({ ok: false, lineId: 'a' });
  });
});
