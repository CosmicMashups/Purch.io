import { useQueryClient } from '@tanstack/react-query';
import { useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { ApproverPinDialog } from '../../../components/ApproverPinDialog';
import { toast } from '../../../components/feedback/toastStore';
import { controlClass, FormField, PrimaryButton } from '../../../components/forms/FormField';
import { PageHeader } from '../../../components/PageHeader';
import { ApiError, userMessage } from '../../../lib/apiError';
import { useSession } from '../../auth/useSession';
import { useSelectableBranches } from '../../branches/queries';
import { useApproverPinAction } from '../../shared/useApproverPinAction';
import { inventoryApi } from '../api';
import { CsvFileField, MAX_CSV_BYTES } from '../components/CsvFileField';
import { ImportReview } from '../components/ImportReview';
import type { ImportBatch, ImportResolution, ImportResult } from '../types';

const STALE = 'The file or catalog changed. Review again.';

export function ImportPage() {
  const { role } = useSession();
  const canCommit = role === 'Admin' || role === 'Manager';
  const qc = useQueryClient();
  const { branches } = useSelectableBranches();
  const [kind, setKind] = useState<'catalog' | 'stock'>('catalog');
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const keyRef = useRef('');
  const [batch, setBatch] = useState<ImportBatch | null>(null);
  const [confirmed, setConfirmed] = useState<boolean[]>([]);
  const [branchId, setBranchId] = useState('');
  const [busy, setBusy] = useState(false);
  const [staleNote, setStaleNote] = useState(false);
  const [result, setResult] = useState<ImportResult | null>(null);
  const committing = useRef<Promise<{ result: ImportResult | null }> | null>(null);

  function pick(next: File | null) {
    setFileError(null);
    if (next && next.size > MAX_CSV_BYTES) {
      setFile(null);
      setFileError('That file is larger than 5 MB.');
      return;
    }
    setFile(next);
    // One key per selected file: re-uploading the same selection returns the same batch.
    keyRef.current = next ? crypto.randomUUID() : '';
  }

  function show(next: ImportBatch) {
    setBatch(next);
    setConfirmed(next.plan.willCreate.map(() => false));
  }

  async function guard(action: () => Promise<void>) {
    setBusy(true);
    try {
      await action();
    } catch (error) {
      toast.error(userMessage(error));
    } finally {
      setBusy(false);
    }
  }

  const upload = () =>
    guard(async () => {
      show(await inventoryApi.imports.upload(file!, kind, keyRef.current));
      setStaleNote(false);
    });

  const resolve = (key: string, r: ImportResolution) =>
    guard(async () => show(await inventoryApi.imports.saveResolutions(batch!.id, { ...batch!.resolutions, [key]: r })));

  const pin = useApproverPinAction({
    send: (p) => {
      if (committing.current) return committing.current;
      const request = inventoryApi.imports
        .commit(batch!.id, { approverPin: p ?? null, planHash: batch!.planHash, confirmCreations: true, branchId: batch!.requiresBranch ? branchId : null })
        .finally(() => (committing.current = null));
      committing.current = request;
      return request;
    },
    onDone: (r) => {
      if (r.result) setResult(r.result);
      void qc.invalidateQueries({ queryKey: ['inventory'] });
      void qc.invalidateQueries({ queryKey: ['items'] });
    },
    describeError: (error) => {
      if (error instanceof ApiError && error.kind === 'conflict') {
        setStaleNote(true);
        void inventoryApi.imports.get(batch!.id).then(show);
        return STALE;
      }
      return userMessage(error);
    },
  });

  if (result) {
    return (
      <div className="flex flex-col gap-4">
        <PageHeader title="Import complete" backTo={{ to: '/inventory', label: 'Inventory' }} />
        <ul className="text-base text-ink">
          <li>{result.createdCategories.length} categories created</li>
          <li>{result.createdRecords.length} records created</li>
          <li>{result.updatedCount} updated</li>
          <li>{result.movements.length} stock adjustments</li>
        </ul>
        <Link to="/inventory/movements" className="flex h-12 w-fit items-center rounded-control bg-brand px-5 text-base font-semibold text-on-brand">
          View stock movements
        </Link>
      </div>
    );
  }

  const ready =
    !!batch && batch.plan.rowErrors.length === 0 && batch.plan.needsDecision.length === 0 && confirmed.every(Boolean) && (!batch.requiresBranch || !!branchId);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Import CSV" subtitle="Upload a catalog or stock file, review the changes, then commit." backTo={{ to: '/inventory', label: 'Inventory' }} />

      <div className="flex flex-col gap-4 rounded-panel border border-line bg-surface p-4">
        <FormField label="What is in the file">
          <select value={kind} onChange={(e) => setKind(e.target.value as 'catalog' | 'stock')} className={controlClass} disabled={busy}>
            <option value="catalog">Catalog (items and ingredients)</option>
            <option value="stock">Stock counts</option>
          </select>
        </FormField>
        <CsvFileField file={file} onPick={pick} error={fileError} />
        <div>
          <PrimaryButton type="button" busy={busy && !batch} disabled={!file || busy} onClick={() => void upload()}>
            {batch ? 'Upload again' : 'Upload and review'}
          </PrimaryButton>
        </div>
      </div>

      {staleNote && (
        <p role="alert" className="rounded-panel border border-amber-300 bg-amber-50 p-3 text-base text-amber-900">
          {STALE}
        </p>
      )}

      {batch && (
        <>
          <ImportReview batch={batch} confirmed={confirmed} onConfirmedChange={setConfirmed} busy={busy} onResolve={(k, r) => void resolve(k, r)} />
          {batch.requiresBranch && canCommit && (
            <FormField label="Record stock at branch">
              <select value={branchId} onChange={(e) => setBranchId(e.target.value)} className={controlClass}>
                <option value="">Choose a branch</option>
                {(branches ?? []).map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.name}
                  </option>
                ))}
              </select>
            </FormField>
          )}
          {canCommit && (
            <div>
              <PrimaryButton type="button" disabled={!ready || busy} onClick={() => void pin.run('Approve import', 'Enter a manager or admin PIN to commit this import.')}>
                Commit import
              </PrimaryButton>
            </div>
          )}
        </>
      )}
      {pin.dialogProps && <ApproverPinDialog open {...pin.dialogProps} />}
    </div>
  );
}
