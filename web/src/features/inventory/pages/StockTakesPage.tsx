import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ErrorState } from '../../../components/ErrorState';
import { useCategories, useItems } from '../../catalog/queries';
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
  const invCategories = useInventoryCategories();
  const catCategories = useCategories();
  const ingredients = useInventoryItems();
  const catalogItems = useItems();
  const [name, setName] = useState('');
  const [scope, setScope] = useState<StockTakeScopeKind>(StockTakeScopeKind.All);
  const [blind, setBlind] = useState(false);
  const [picked, setPicked] = useState<string[]>([]);
  const [query, setQuery] = useState('');
  const [error, setError] = useState<string | null>(null);
  const toggle = (id: string) => setPicked((p) => (p.includes(id) ? p.filter((x) => x !== id) : [...p, id]));

  type Source = { isPending: boolean; isError: boolean; data?: { id: string; name: string }[] };
  const sections: { title: string; source: Source }[] =
    scope === StockTakeScopeKind.Categories
      ? [
          { title: 'Item categories', source: catCategories },
          { title: 'Ingredient categories', source: invCategories },
        ]
      : scope === StockTakeScopeKind.Picked
        ? [
            { title: 'Items', source: catalogItems },
            { title: 'Ingredients', source: ingredients },
          ]
        : [];
  const q = query.trim().toLowerCase();

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (create.isPending) return;
    if (!name.trim()) return void setError('Give the stock take a name.');
    if (scope !== StockTakeScopeKind.All && picked.length === 0) {
      return void setError(scope === StockTakeScopeKind.Categories ? 'Choose at least one category.' : 'Pick at least one item or ingredient.');
    }
    setError(null);
    const ids = new Set(picked);
    const only = (list?: { id: string }[]) => (list ?? []).filter((o) => ids.has(o.id)).map((o) => o.id);
    try {
      const take = await create.mutateAsync({
        name: name.trim(),
        scopeKind: scope,
        blind,
        // Categories scope: the backend filters items and ingredients by the same id set.
        categoryIds: scope === StockTakeScopeKind.Categories ? [...only(catCategories.data), ...only(invCategories.data)] : undefined,
        itemIds: scope === StockTakeScopeKind.Picked ? only(catalogItems.data) : undefined,
        inventoryItemIds: scope === StockTakeScopeKind.Picked ? only(ingredients.data) : undefined,
      });
      onClose();
      navigate(`/inventory/stock-takes/${take.id}`);
    } catch (e) {
      setError(userMessage(e));
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
            setQuery('');
            setError(null);
          }}
          className={controlClass}
        >
          <option value={StockTakeScopeKind.All}>Everything</option>
          <option value={StockTakeScopeKind.Categories}>Chosen categories</option>
          <option value={StockTakeScopeKind.Picked}>Chosen items and ingredients</option>
        </select>
      </FormField>
      {sections.length > 0 && (
        <div className="flex flex-col gap-3">
          <input aria-label="Search" placeholder="Search" value={query} onChange={(e) => setQuery(e.target.value)} className={controlClass} />
          <div className="flex max-h-60 flex-col overflow-y-auto">
            {sections.map(({ title, source }) => {
              const rows = (source.data ?? []).filter((o) => o.name.toLowerCase().includes(q));
              return (
                <fieldset key={title}>
                  <legend className="text-base font-semibold">{title}</legend>
                  {source.isPending && <p className="text-base text-ink-soft">Loading...</p>}
                  {source.isError && <p className="text-base text-danger">Could not be loaded.</p>}
                  {!source.isPending && !source.isError && rows.length === 0 && <p className="text-base text-ink-soft">None found.</p>}
                  {rows.map((o) => (
                    <label key={o.id} className="flex h-12 items-center gap-3 text-base">
                      <input type="checkbox" checked={picked.includes(o.id)} onChange={() => toggle(o.id)} className="size-5" />
                      {o.name}
                    </label>
                  ))}
                </fieldset>
              );
            })}
          </div>
        </div>
      )}
      <label className="flex h-12 items-center gap-3 text-base">
        <input type="checkbox" checked={blind} onChange={(e) => setBlind(e.target.checked)} className="size-5" />
        Blind count (hide expected quantities while counting)
      </label>
      {error && (
        <p role="alert" className="text-sm font-medium text-danger">
          {error}
        </p>
      )}
    </FormDialog>
  );
}
