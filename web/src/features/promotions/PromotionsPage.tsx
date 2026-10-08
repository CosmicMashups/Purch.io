import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { PageHeader } from '../../components/PageHeader';
import { StatusFilter, type StatusView } from '../lifecycle/StatusFilter';
import { ErrorState } from '../../components/ErrorState';
import { Skeleton } from '../../components/Skeleton';
import { userMessage } from '../../lib/apiError';
import { useItems } from '../catalog/queries';
import { BogoPanel } from './components/BogoPanel';
import { ComboPanel } from './components/ComboPanel';
import { ItemDiscountPanel } from './components/ItemDiscountPanel';
import { PromoCodePanel } from './components/PromoCodePanel';

const TABS = [
  { id: 'bogo', label: 'Buy 1 Take 1' },
  { id: 'combo', label: 'Combo deals' },
  { id: 'discount', label: 'Item discounts' },
  { id: 'codes', label: 'Promo codes' },
] as const;

type TabId = (typeof TABS)[number]['id'];

function isTabId(value: string | null): value is TabId {
  return TABS.some((t) => t.id === value);
}

export function PromotionsPage() {
  const [params, setParams] = useSearchParams();
  const requested = params.get('type');
  const active: TabId = isTabId(requested) ? requested : 'bogo';
  const items = useItems();
  const [view, setView] = useState<StatusView>('active');

  return (
    <section className="flex flex-col gap-6">
      <PageHeader
        title="Promotions"
        subtitle="These apply automatically at the till. The server works out the final price."
        backTo={{ to: '/business', label: 'Business' }}
      />

      <div role="tablist" aria-label="Promotion type" className="flex flex-wrap gap-2">
        {TABS.map((tab) => {
          const selected = tab.id === active;
          return (
            <button
              key={tab.id}
              type="button"
              role="tab"
              id={`tab-${tab.id}`}
              aria-selected={selected}
              aria-controls={`panel-${tab.id}`}
              onClick={() => setParams({ type: tab.id }, { replace: true })}
              className={`h-12 rounded-control px-5 text-base font-semibold ${selected ? 'bg-brand text-on-brand' : 'border border-line bg-surface hover:border-brand'}`}
            >
              {tab.label}
            </button>
          );
        })}
      </div>

      <StatusFilter value={view} onChange={setView} />

      <div role="tabpanel" id={`panel-${active}`} aria-labelledby={`tab-${active}`}>
        {items.isPending && <Skeleton className="h-40 w-full" />}
        {items.isError && (
          <ErrorState title="Items could not be loaded" message={userMessage(items.error)} onRetry={() => void items.refetch()} />
        )}
        {items.isSuccess && (
          <>
            {active === 'bogo' && <BogoPanel items={items.data} view={view} />}
            {active === 'combo' && <ComboPanel items={items.data} view={view} />}
            {active === 'discount' && <ItemDiscountPanel items={items.data} view={view} />}
            {active === 'codes' && <PromoCodePanel view={view} />}
          </>
        )}
      </div>
    </section>
  );
}
