import { useSearchParams } from 'react-router-dom';
import { ErrorState } from '../../../components/ErrorState';
import { SkeletonList } from '../../../components/Skeleton';
import { FormField, PrimaryButton, SecondaryButton, controlClass } from '../../../components/forms/FormField';
import { DataTable, type DataColumn } from '../../../components/lists/DataTable';
import { Pill } from '../../../components/lists/QueryList';
import { PageHeader } from '../../../components/PageHeader';
import { GroupedCombobox } from '../../../components/forms/GroupedCombobox';
import { formatDateTime } from '../../../lib/dates';
import { userMessage } from '../../../lib/apiError';
import { useBranches } from '../../branches/queries';
import { FILTERABLE_TYPES, initialMovementType, movementLabel } from '../movement';
import { useMovementLog } from '../queries';
import { MovementType, type MovementFilter, type InventoryMovement } from '../types';
import { ingredientRef, itemRef, parseStockRef, useStockOptions } from '../stockOptions';
import { RecordMovementDialog } from './RecordMovementDialog';

function readFilter(params: URLSearchParams): MovementFilter {
  const type = params.get('type');
  const parsedType = type === null ? NaN : Number(type);
  return {
    itemId: params.get('itemId') || undefined,
    inventoryItemId: params.get('inventoryItemId') || undefined,
    branchId: params.get('branchId') || undefined,
    type: (FILTERABLE_TYPES as readonly number[]).includes(parsedType) ? (parsedType as MovementType) : undefined,
  };
}

export function MovementLogPage() {
  const [params, setParams] = useSearchParams();
  const filter = readFilter(params);
  const log = useMovementLog(filter);
  const stock = useStockOptions();
  const branches = useBranches();

  function setFilter(key: 'branchId' | 'type', value: string) {
    const next = new URLSearchParams(params);
    if (value === '') next.delete(key);
    else next.set(key, value);
    setParams(next, { replace: true });
  }

  function setStockFilter(value: string) {
    const next = new URLSearchParams(params);
    next.delete('itemId');
    next.delete('inventoryItemId');
    const ref = parseStockRef(value);
    if (ref) next.set(ref.kind === 'item' ? 'itemId' : 'inventoryItemId', ref.id);
    setParams(next, { replace: true });
  }

  // Links from elsewhere (the Inventory home, the restock list) open the dialog with `?record=1`.
  const recording = params.get('record') !== null;
  function closeRecording() {
    const next = new URLSearchParams(params);
    next.delete('record');
    next.delete('stockRef');
    next.delete('recordType');
    setParams(next, { replace: true });
  }

  const rows = log.data?.pages.flat() ?? [];
  const columns: DataColumn<InventoryMovement>[] = [
    {
      header: 'Date',
      className: 'whitespace-nowrap text-gray-600',
      sortValue: (r) => new Date(r.createdAt),
      cell: (r) => formatDateTime(r.createdAt),
    },
    {
      header: 'Item',
      sortValue: (r) => r.inventoryItemName ?? r.itemName,
      cell: (r) => (
        <div>
          <p className="font-medium text-gray-900">{r.inventoryItemName ?? r.itemName}</p>
          {r.inventoryItemName && r.inventoryItemName !== r.itemName && r.itemId && <p className="text-xs text-gray-500">For {r.itemName}</p>}
        </div>
      ),
    },
    { header: 'Branch', className: 'text-gray-600', sortValue: (r) => r.branchName, cell: (r) => r.branchName },
    { header: 'Staff', className: 'text-gray-600', sortValue: (r) => r.staffUserName, cell: (r) => r.staffUserName },
    {
      header: 'Type',
      sortValue: (r) => movementLabel(r.type),
      cell: (r) => (
        <div className="flex items-center gap-2">
          {movementLabel(r.type)}
          {r.type === MovementType.Sale && <Pill>Automatic</Pill>}
        </div>
      ),
    },
    { header: 'Quantity', className: 'text-right font-semibold tabular-nums', sortValue: (r) => r.quantity, cell: (r) => r.quantity },
    {
      header: 'Notes',
      className: 'max-w-xs text-xs text-gray-500',
      cell: (r) =>
        [r.reasonCategory && `Reason: ${r.reasonCategory}`, r.supplierReference && `Supplier ref: ${r.supplierReference}`, r.note].filter(Boolean).join(' | '),
    },
  ];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Stock movements"
        backTo={{ to: '/inventory', label: 'Inventory' }}
        action={
          <PrimaryButton
            type="button"
            onClick={() => {
              const next = new URLSearchParams(params);
              next.set('record', '1');
              setParams(next, { replace: true });
            }}
          >
            Record movement
          </PrimaryButton>
        }
      />

      <div className="grid gap-4 sm:grid-cols-3">
        <FormField label="Type">
          <select value={filter.type ?? ''} onChange={(e) => setFilter('type', e.target.value)} className={controlClass}>
            <option value="">All types</option>
            {FILTERABLE_TYPES.map((type) => (
              <option key={type} value={type}>
                {movementLabel(type)}
              </option>
            ))}
          </select>
        </FormField>
        <FormField label="Item or ingredient">
          <GroupedCombobox
            groups={stock.groups}
            value={filter.itemId ? itemRef(filter.itemId) : filter.inventoryItemId ? ingredientRef(filter.inventoryItemId) : ''}
            onChange={setStockFilter}
            emptyLabel="All items and ingredients"
            placeholder="All items and ingredients"
          />
        </FormField>
        <FormField label="Branch">
          <select value={filter.branchId ?? ''} onChange={(e) => setFilter('branchId', e.target.value)} className={controlClass}>
            <option value="">All branches</option>
            {(branches.data ?? []).map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </select>
        </FormField>
      </div>

      {log.isPending && <SkeletonList rows={6} />}
      {log.isError && <ErrorState title="The movement log could not be loaded" message={userMessage(log.error)} onRetry={() => void log.refetch()} />}
      {log.isSuccess && rows.length === 0 && (
        <p className="rounded-panel border border-dashed border-ink-soft/40 p-6 text-base text-ink-soft">No movements match these filters.</p>
      )}
      {log.isSuccess && rows.length > 0 && (
        <DataTable label="Stock movements" rows={rows} columns={columns} getId={(r) => r.id} />
      )}
      {recording && (
        <RecordMovementDialog
          prefill={{ stockRef: params.get('stockRef') ?? undefined, type: initialMovementType(params.get('recordType')) }}
          onClose={closeRecording}
        />
      )}
      {log.hasNextPage && (
        <div>
          <SecondaryButton type="button" disabled={log.isFetchingNextPage} onClick={() => void log.fetchNextPage()}>
            {log.isFetchingNextPage ? 'Loading...' : 'Load older movements'}
          </SecondaryButton>
        </div>
      )}
    </div>
  );
}
