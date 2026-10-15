import type { PurchaseOrder } from './types';

const esc = (value: string | number | null | undefined) =>
  String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c] as string);
const money = (n: number | undefined) => (n ?? 0).toLocaleString('en-PH', { style: 'currency', currency: 'PHP' });

/** Opens a plain printable purchase order in a new window and prints it. */
export function printPurchaseOrder(order: PurchaseOrder) {
  const rows = order.lines
    .map((l) => `<tr><td>${esc(l.itemName)}</td><td class=n>${esc(l.quantityOrdered)}</td><td>${esc(l.unit ?? 'pc')}</td><td class=n>${money(l.expectedUnitCost)}</td><td class=n>${money(l.amount)}</td></tr>`)
    .join('');
  const html = `<!doctype html><html><head><meta charset="utf-8"><title>${esc(order.controlNumber ?? 'Purchase order')}</title>
<style>body{font:14px system-ui,sans-serif;margin:32px;color:#111}h1{font-size:20px;margin:0 0 4px}table{border-collapse:collapse;width:100%;margin:16px 0}
th,td{border-bottom:1px solid #ccc;padding:6px 8px;text-align:left}.n{text-align:right}dl{margin-left:auto;width:260px;display:grid;grid-template-columns:1fr auto;gap:4px 16px}dd{margin:0;text-align:right}p{margin:2px 0}</style></head><body>
<h1>Purchase order ${esc(order.controlNumber)}</h1>
<p>Supplier: ${esc(order.supplierName)}</p>
<p>Pay to: ${esc(order.paymentAccountName)} ${esc(order.paymentAccountNumber)}</p>
<p>Requested ${esc(order.requestDate)} by ${esc(order.requestedByName)}${order.dateNeeded ? `, needed by ${esc(order.dateNeeded)}` : ''}</p>
<p>Deliver to: ${esc(order.branchName)} ${esc(order.deliveryAddress)}</p>
<table><thead><tr><th>Item</th><th class=n>Qty</th><th>Unit</th><th class=n>Unit price</th><th class=n>Amount</th></tr></thead><tbody>${rows}</tbody></table>
<dl><dt>Subtotal</dt><dd>${money(order.subtotal)}</dd><dt>Less discount</dt><dd>${money(order.discountAmount)}</dd>
<dt>VAT ${order.vatInclusive ? '(included)' : '(added)'}</dt><dd>${money(order.vatAmount)}</dd><dt><b>Order total</b></dt><dd><b>${money(order.orderTotal)}</b></dd></dl>
${order.remarks ? `<p>Remarks: ${esc(order.remarks)}</p>` : ''}
</body></html>`;
  const win = window.open('', '_blank');
  if (!win) return;
  win.document.write(html);
  win.document.close();
  win.focus();
  win.print();
}
