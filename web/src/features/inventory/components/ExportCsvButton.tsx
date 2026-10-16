import { useState } from 'react';
import { Link } from 'react-router-dom';
import { toast } from '../../../components/feedback/toastStore';
import { userMessage } from '../../../lib/apiError';
import { datedFilename, downloadTextFile } from '../../../lib/download';
import { useSession } from '../../auth/useSession';
import { inventoryApi } from '../api';

/** `2026-10-08` for the viewer's local today. */
function localDay(date = new Date()): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** Downloads one of the inventory CSV exports as `<kind>-<yyyy-mm-dd>.csv`. */
export function ExportCsvButton({ kind, label }: { kind: 'items' | 'ingredients' | 'stock'; label: string }) {
  const { role } = useSession();
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

  // Same roles as the export routes; the API enforces them regardless.
  if (role !== 'Admin' && role !== 'Manager' && role !== 'Warehouse') return null;

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

/** Entry to the import wizard, beside the exports. Warehouse may upload; only Admin/Manager can commit. */
export function ImportCsvLink() {
  const { role } = useSession();
  if (role !== 'Admin' && role !== 'Manager' && role !== 'Warehouse') return null;
  return (
    <Link to="/inventory/import" className="flex h-12 items-center whitespace-nowrap rounded-control border border-brand px-5 text-base font-semibold text-brand-strong hover:bg-brand-tint">
      Import CSV
    </Link>
  );
}
