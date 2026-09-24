import { useMutation } from '@tanstack/react-query';
import { toast } from '../../../components/feedback/toastStore';
import { PrimaryButton, SecondaryButton } from '../../../components/forms/FormField';
import { csvRowCount, datedFilename, downloadTextFile } from '../../../lib/download';
import { reportsApi } from '../api';
import { businessToday } from '../range';
import type { RangeParams } from '../types';

interface ExportsPanelProps {
  params: RangeParams | null;
  /** The raw sales export is Admin-only on the server. */
  isAdmin: boolean;
}

export function ExportsPanel({ params, isAdmin }: ExportsPanelProps) {
  const lowStock = useMutation({ mutationFn: () => reportsApi.lowStockCsv() });
  const sales = useMutation({ mutationFn: (p: RangeParams) => reportsApi.transactionsCsv(p) });
  const today = businessToday();

  function exportLowStock() {
    lowStock.mutate(undefined, {
      onSuccess: (csv) => {
        const rows = csvRowCount(csv);
        if (rows === 0) return toast.info('Nothing is below its reorder level, so there is nothing to export.');
        downloadTextFile(datedFilename('low-stock-reorder', today), csv);
        toast.success(`Exported ${rows} item${rows === 1 ? '' : 's'} to reorder`);
      },
    });
  }

  function exportSales() {
    if (!params) return;
    sales.mutate(params, {
      onSuccess: (csv) => {
        const rows = csvRowCount(csv);
        if (rows === 0) return toast.info('There are no sales in this period to export.');
        downloadTextFile(datedFilename('sales-transactions', today), csv);
        toast.success(`Exported ${rows} row${rows === 1 ? '' : 's'}`);
      },
    });
  }

  return (
    <div className="grid max-w-3xl gap-6">
      <section className="rounded-panel border border-line bg-surface p-6">
        <h2 className="text-lg font-semibold">Low-stock reorder list</h2>
        <p className="mt-1 text-base text-ink-soft">Every item at or below its alert level, with a suggested reorder quantity. Send it to your supplier.</p>
        <div className="mt-4">
          <PrimaryButton type="button" busy={lowStock.isPending} onClick={exportLowStock}>
            {lowStock.isPending ? 'Preparing...' : 'Download CSV'}
          </PrimaryButton>
        </div>
      </section>

      <section className="rounded-panel border border-line bg-surface p-6">
        <h2 className="text-lg font-semibold">Sales transactions</h2>
        {isAdmin ? (
          <>
            <p className="mt-1 text-base text-ink-soft">Every sale in the chosen period, for your accountant or your own records. Uses the date range and branch above.</p>
            {!params && <p className="mt-2 text-sm font-medium text-danger">Choose a valid date range first.</p>}
            <div className="mt-4">
              <SecondaryButton type="button" disabled={!params || sales.isPending} onClick={exportSales}>
                {sales.isPending ? 'Preparing...' : 'Download CSV'}
              </SecondaryButton>
            </div>
          </>
        ) : (
          <p className="mt-1 text-base text-ink-soft">Only an admin can export raw sales.</p>
        )}
      </section>
    </div>
  );
}
