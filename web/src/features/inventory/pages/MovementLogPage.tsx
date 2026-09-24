import { useSearchParams } from 'react-router-dom';
import { ErrorState } from '../../../components/ErrorState';
import { SkeletonList } from '../../../components/Skeleton';
import { FormField, SecondaryButton, controlClass } from '../../../components/forms/FormField';
import { ListCard, Pill } from '../../../components/lists/QueryList';
import { LinkButton, PageHeader } from '../../../components/PageHeader';
import { formatDateTime } from '../../../lib/dates';
import { userMessage } from '../../../lib/apiError';
import { useBranches } from '../../branches/queries';
import { useItems } from '../../catalog/queries';
import { FILTERABLE_TYPES, movementLabel } from '../movement';
import { useMovementLog } from '../queries';
import { MovementType, type MovementFilter } from '../types';

function readFilter(params: URLSearchParams): MovementFilter {
  const type = params.get('type');
  const parsedType = type === null ? NaN : Number(type);
  return {
    itemId: params.get('itemId') || undefined,
    branchId: params.get('branchId') || undefined,
    type: (FILTERABLE_TYPES as readonly number[]).includes(parsedType) ? (parsedType as MovementType) : undefined,
  };
}

export function MovementLogPage() {
  const [params, setParams] = useSearchParams();
  const filter = readFilter(params);
  const log = useMovementLog(filter);
  const items = useItems();
  const branches = useBranches();

  function setFilter(key: 'itemId' | 'branchId' | 'type', value: string) {
    const next = new URLSearchParams(params);
    if (value === '') next.delete(key);
    else next.set(key, value);
    setParams(next, { replace: true });
  }

  const rows = log.data?.pages.flat() ?? [];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Stock movements"
        backTo={{ to: '/inventory', label: 'Inventory' }}
        action={<LinkButton to="/inventory/movements/new" primary>Record movement</LinkButton>}
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
        <FormField label="Item">
          <select value={filter.itemId ?? ''} onChange={(e) => setFilter('itemId', e.target.value)} className={controlClass}>
            <option value="">All items</option>
            {(items.data ?? []).map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            ))}
          </select>
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
        <ul className="flex flex-col gap-3">
          {rows.map((row) => (
            <ListCard key={row.id}>
              <div className="min-w-0">
                <p className="text-base font-semibold">{row.itemName}</p>
                <p className="text-base tabular-nums">
                  {movementLabel(row.type)}: {row.quantity}
                </p>
                <p className="text-sm text-ink-soft">
                  {row.branchName}, {row.staffUserName}, {formatDateTime(row.createdAt)}
                </p>
                {(row.reasonCategory || row.supplierReference || row.note) && (
                  <p className="mt-1 text-sm text-ink-soft">
                    {[row.reasonCategory && `Reason: ${row.reasonCategory}`, row.supplierReference && `Supplier ref: ${row.supplierReference}`, row.note]
                      .filter(Boolean)
                      .join(' | ')}
                  </p>
                )}
              </div>
              {row.type === MovementType.Sale && <Pill>Automatic</Pill>}
            </ListCard>
          ))}
        </ul>
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
