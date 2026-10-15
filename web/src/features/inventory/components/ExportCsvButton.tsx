import { useState } from 'react';
import { toast } from '../../../components/feedback/toastStore';
import { userMessage } from '../../../lib/apiError';
import { datedFilename, downloadTextFile } from '../../../lib/download';
import { inventoryApi } from '../api';

/** `2026-10-08` for the viewer's local today. */
function localDay(date = new Date()): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** Downloads one of the inventory CSV exports as `<kind>-<yyyy-mm-dd>.csv`. */
export function ExportCsvButton({ kind, label }: { kind: 'items' | 'ingredients' | 'stock'; label: string }) {
  const [busy, setBusy] = useState(false);

  async function run() {
    setBusy(true);
    try {
      downloadTextFile(datedFilename(kind, localDay()), await inventoryApi.exportCsv(kind));
    } catch (error) {
      toast.error(userMessage(error));
    } finally {
      setBusy(false);
    }
  }

  return (
    <button
      type="button"
      onClick={() => void run()}
      disabled={busy}
      className="h-12 whitespace-nowrap rounded-control bg-brand px-5 text-base font-semibold text-on-brand hover:bg-brand-strong disabled:opacity-50"
    >
      {busy ? 'Exporting…' : label}
    </button>
  );
}
