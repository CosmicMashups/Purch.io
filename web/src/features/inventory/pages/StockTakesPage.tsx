import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ErrorState } from '../../../components/ErrorState';
import { toast } from '../../../components/feedback/toastStore';
import { FormDialog } from '../../../components/forms/FormDialog';
import { FormField, PrimaryButton, controlClass } from '../../../components/forms/FormField';
import { Pill } from '../../../components/lists/QueryList';
import { PageHeader } from '../../../components/PageHeader';
import { SkeletonList } from '../../../components/Skeleton';
import { userMessage } from '../../../lib/apiError';
import { useCreateStockTake, useInventoryCategories, useInventoryItems, useStockTakes } from '../queries';
import { stockTakeStatusLabel } from '../stockTake';
import { StockTakeScopeKind } from '../types';

export function StockTakesPage() {
  const takes = useStockTakes();
  const [creating, setCreating] = useState(false);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Stock takes"
        backTo={{ to: '/inventory', label: 'Inventory' }}
        action={
          <PrimaryButton type="button" onClick={() => setCreating(true)}>
            New stock take
          </PrimaryButton>
        }
      />
      {takes.isPending && <SkeletonList />}
      {takes.isError && <ErrorState title="Stock takes could not be loaded" message={userMessage(takes.error)} onRetry={() => void takes.refetch()} />}
      {takes.isSuccess && takes.data.length === 0 && (
        <p className="rounded-panel border border-dashed border-ink-soft/40 p-6 text-base text-ink-soft">No stock takes yet.</p>
      )}
      {takes.isSuccess && takes.data.length > 0 && (
        <ul className="flex flex-col gap-3">
          {takes.data.map((t) => (
            <li key={t.id}>
              <Link
                to={`/inventory/stock-takes/${t.id}`}
                className="flex min-h-12 flex-wrap items-center justify-between gap-2 rounded-panel border border-line bg-surface p-4 hover:border-brand"
              >
                <span className="text-base font-semibold text-ink">{t.name}</span>
                <span className="flex items-center gap-2 text-sm text-ink-soft">
                  {t.countedCount} of {t.lineCount} counted
                  {t.blind && <Pill>Blind</Pill>}
                  <Pill tone={stockTakeStatusLabel[t.status].tone}>{stockTakeStatusLabel[t.status].label}</Pill>
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
      {creating && <CreateDialog onClose={() => setCreating(false)} />}
    </div>
  );
}

function CreateDialog({ onClose }: { onClose: () => void }) {
  const create = useCreateStockTake();
  const navigate = useNavigate();
  const categories = useInventoryCategories();
  const ingredients = useInventoryItems();
  const [name, setName] = useState('');
  const [scope, setScope] = useState<StockTakeScopeKind>(StockTakeScopeKind.All);
  const [blind, setBlind] = useState(false);
  const [picked, setPicked] = useState<string[]>([]);
  const toggle = (id: string) => setPicked((p) => (p.includes(id) ? p.filter((x) => x !== id) : [...p, id]));
  const options =
    scope === StockTakeScopeKind.Categories ? (categories.data ?? []) : scope === StockTakeScopeKind.Picked ? (ingredients.data ?? []) : [];

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (create.isPending) return;
    if (!name.trim()) return void toast.error('Give the stock take a name.');
    try {
      const take = await create.mutateAsync({
        name: name.trim(),
        scopeKind: scope,
        blind,
        categoryIds: scope === StockTakeScopeKind.Categories ? picked : undefined,
        inventoryItemIds: scope === StockTakeScopeKind.Picked ? picked : undefined,
      });
      onClose();
      navigate(`/inventory/stock-takes/${take.id}`);
    } catch (error) {
      toast.error(userMessage(error));
    }
  }

  return (
    <FormDialog title="New stock take" busy={create.isPending} submitLabel="Create stock take" onSubmit={(e) => void submit(e)} onClose={onClose}>
      <FormField label="Name">
        <input value={name} onChange={(e) => setName(e.target.value)} className={controlClass} />
      </FormField>
      <FormField label="What to count">
        <select
          value={scope}
          onChange={(e) => {
            setScope(Number(e.target.value) as StockTakeScopeKind);
            setPicked([]);
          }}
          className={controlClass}
        >
          <option value={StockTakeScopeKind.All}>Everything</option>
          <option value={StockTakeScopeKind.Categories}>Chosen categories</option>
          <option value={StockTakeScopeKind.Picked}>Chosen ingredients</option>
        </select>
      </FormField>
      {options.length > 0 && (
        <fieldset className="flex max-h-60 flex-col overflow-y-auto">
          <legend className="text-base font-semibold">Choose which</legend>
          {options.map((o) => (
            <label key={o.id} className="flex h-12 items-center gap-3 text-base">
              <input type="checkbox" checked={picked.includes(o.id)} onChange={() => toggle(o.id)} className="size-5" />
              {o.name}
            </label>
          ))}
        </fieldset>
      )}
      <label className="flex h-12 items-center gap-3 text-base">
        <input type="checkbox" checked={blind} onChange={(e) => setBlind(e.target.checked)} className="size-5" />
        Blind count (hide expected quantities while counting)
      </label>
    </FormDialog>
  );
}
