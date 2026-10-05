import { CheckCircle, Circle, Minus, Plus } from '@phosphor-icons/react';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Navigate, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { ErrorState } from '../../components/ErrorState';
import { Skeleton } from '../../components/Skeleton';
import { PurchImage } from '../../components/brand/PurchImage';
import { userMessage } from '../../lib/apiError';
import { useComboComponents, useItemModifierGroups, useItems, useVariants } from '../catalog/queries';
import { PricingType, type Item, type ItemComboComponent, type ItemVariant, type ModifierGroup } from '../catalog/types';
import { formatPeso } from '../dashboard/format';
import { addFlowFor } from '../pos/catalogView';
import {
  buildAddLine,
  isFixedSlot,
  missingOption,
  noneLabel,
  slotChoices,
  groupOptions,
  toggleModifier,
  unorderableReason,
  type OptionPicks,
  type OptionShape,
} from '../pos/options';
import { useTenantSettings } from '../tenant/queries';
import { AddedToOrderOverlay } from './AddedToOrderOverlay';
import { barClass, primaryButton, secondaryButton } from './KioskActionBar';
import { OptionCard } from './OptionCard';
import { resolveAdd, useLocalKioskCartStore, type LocalCartLine } from './localCart';
import { quantityCap } from './quantityCap';

type TabId = 'quantity' | 'variant' | `slot:${string}` | `group:${string}`;

interface Tab {
  id: TabId;
  label: string;
  /** "Required", "Optional", or what has been chosen so far. */
  hint: string;
  resolved: boolean;
}

const variantLabel = (variant: ItemVariant) => Object.values(variant.attributes).join(' / ') || variant.sku || 'Variant';

/** What an existing cart line had chosen, in the shape the page edits. Every group the line carries was decided, even to "none". */
function picksFromLine(line: LocalCartLine, slots: ItemComboComponent[], groups: ModifierGroup[]): Pick<OptionPicks, 'slots' | 'groups'> {
  const chosenIds = new Set(line.modifierSelections.map((selection) => selection.itemModifierId ?? selection.itemId));
  return {
    slots: Object.fromEntries(
      slots.filter((slot) => !isFixedSlot(slot)).map((slot) => [slot.id, line.comboSelections.filter((selection) => selection.slotId === slot.id).map((selection) => selection.selectedItemId)]),
    ),
    groups: Object.fromEntries(groups.map((group) => [group.id, groupOptions(group).filter((option) => chosenIds.has(option.id)).map((option) => option.id)])),
  };
}

export function KioskItemPage() {
  const { itemId = '' } = useParams();
  const [search] = useSearchParams();
  const lineId = search.get('line');
  const items = useItems();
  const line = useLocalKioskCartStore((s) => s.lines.find((candidate) => candidate.localId === lineId));
  const navigate = useNavigate();

  if (lineId && !line) return <Navigate to="/kiosk/cart" replace />;
  if (items.isPending) {
    return (
      <div className="flex h-full flex-col gap-4 p-6" aria-busy="true">
        <Skeleton className="h-16 w-1/2" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }
  if (items.isError) return <ErrorState title="This item could not be loaded" message={userMessage(items.error)} onRetry={() => void items.refetch()} />;

  const item = items.data.find((candidate) => candidate.id === itemId);
  if (!item || !item.isActive) {
    return (
      <div className="grid h-full place-items-center p-8 text-center">
        <div className="flex flex-col items-center gap-6">
          <p className="text-3xl font-extrabold">This item is no longer on the menu</p>
          <button type="button" className={primaryButton} onClick={() => navigate(line ? '/kiosk/cart' : '/kiosk/menu', { replace: true })}>
            {line ? 'Back to your order' : 'Back to menu'}
          </button>
        </div>
      </div>
    );
  }
  // Sold by weight is ordered at the counter; a stale link must not strand the customer here.
  if (addFlowFor(item) === 'weight') return <Navigate to="/kiosk/menu" replace />;

  return <ItemEditor key={`${item.id}:${lineId ?? ''}`} item={item} allItems={items.data} line={line} />;
}

function ItemEditor({ item, allItems, line }: { item: Item; allItems: Item[]; line: LocalCartLine | undefined }) {
  const navigate = useNavigate();
  const editing = line !== undefined;
  const addToCart = useLocalKioskCartStore((s) => s.add);
  const replaceLine = useLocalKioskCartStore((s) => s.replaceLine);
  const orderedElsewhere = useLocalKioskCartStore((s) =>
    s.lines.filter((other) => other.itemId === item.id && other.localId !== line?.localId).reduce((sum, other) => sum + other.quantity, 0),
  );
  const separateTracking = useTenantSettings().data?.useSeparateInventoryTracking;

  const needsVariant = item.pricingType === PricingType.VariantMatrix;
  const isCombo = item.pricingType === PricingType.Combo;
  const variants = useVariants(item.id, needsVariant);
  const comboSlots = useComboComponents(item.id, isCombo);
  const groups = useItemModifierGroups(item.id);

  const [picks, setPicks] = useState<OptionPicks>({ variantId: line?.itemVariantId ?? null, slots: {}, groups: {}, quantity: line?.quantity ?? 1 });
  const [active, setActive] = useState<TabId>('quantity');
  const [added, setAdded] = useState(false);

  const failed = (needsVariant && variants.isError ? variants : null) ?? (isCombo && comboSlots.isError ? comboSlots : null) ?? (groups.isError ? groups : null);
  const ready = (!needsVariant || variants.data) && (!isCombo || comboSlots.data) && groups.data;
  const shape = useMemo<OptionShape | null>(
    () => (ready ? { needsVariant, variants: variants.data ?? [], slots: isCombo ? (comboSlots.data ?? []) : [], groups: groups.data ?? [] } : null),
    [ready, needsVariant, isCombo, variants.data, comboSlots.data, groups.data],
  );

  // Editing an order line starts from what it already had, once the item's options are known.
  const seeded = useRef(false);
  useEffect(() => {
    if (!shape || !line || seeded.current) return;
    seeded.current = true;
    setPicks((current) => ({ ...current, ...picksFromLine(line, shape.slots, shape.groups) }));
  }, [shape, line]);

  const cap = quantityCap(item, separateTracking, orderedElsewhere);
  const blocked = shape ? unorderableReason(shape, allItems) : null;
  const problem = shape ? (blocked ?? missingOption(shape, picks, { explicitGroups: true })) : null;

  const tabs = useMemo<Tab[]>(() => (shape ? buildTabs(shape, picks, allItems) : []), [shape, picks, allItems]);
  const activeTab = tabs.find((tab) => tab.id === active) ?? tabs[0];
  const unresolved = tabs.filter((tab) => !tab.resolved);
  const nextTab = unresolved.find((tab) => tabs.indexOf(tab) > tabs.indexOf(activeTab)) ?? unresolved[0];

  const preview = shape
    ? resolveAdd(item, buildAddLine(item.id, shape, picks), { items: allItems, variants: variants.data, comboComponents: comboSlots.data, modifierGroups: groups.data })
    : null;
  const lineTotal = preview ? preview.unitPrice * picks.quantity : item.basePrice * picks.quantity;
  const backTo = editing ? '/kiosk/cart' : '/kiosk/menu';

  function commit() {
    if (!shape || problem || !preview) return;
    if (line) replaceLine(line.localId, preview);
    else addToCart(preview);
    setAdded(true);
  }

  if (failed) return <ErrorState title="The options could not be loaded" message={userMessage(failed.error)} onRetry={() => void failed.refetch()} />;

  return (
    <div className="flex h-full flex-col">
      <div className="flex min-h-0 flex-1">
        {tabs.length > 1 && (
          <div role="tablist" aria-orientation="vertical" aria-label="Customise your item" className="kiosk-scroll flex w-60 shrink-0 snap-y flex-col gap-2 overflow-y-auto border-r border-line bg-surface p-3 landscape:w-72">
            {tabs.map((tab) => (
              <TabButton key={tab.id} tab={tab} selected={tab.id === activeTab?.id} onSelect={() => setActive(tab.id)} />
            ))}
          </div>
        )}

        <div role="tabpanel" aria-label={activeTab?.label} className="kiosk-scroll min-w-0 flex-1 overflow-y-auto p-6">
          {!shape ? (
            <div className="flex flex-col gap-4" aria-busy="true">
              <Skeleton className="h-16 w-2/3" />
              <Skeleton className="h-28 w-full" />
              <Skeleton className="h-28 w-full" />
            </div>
          ) : (
            <div className={`mx-auto flex max-w-4xl flex-col gap-6 ${tabs.length <= 1 ? 'justify-center' : ''}`}>
              {blocked && (
                <p role="alert" className="rounded-panel border border-danger/40 bg-red-50 px-5 py-4 text-xl font-semibold text-danger">
                  {blocked}. Please choose something else or ask a member of staff.
                </p>
              )}
              {activeTab?.id === 'quantity' && (
                <QuantityPanel
                  item={item}
                  shape={shape}
                  quantity={picks.quantity}
                  cap={cap}
                  onChange={(quantity) => setPicks((current) => ({ ...current, quantity }))}
                />
              )}
              {activeTab?.id === 'variant' && (
                <ChoicePanel title="Choose a size or variant" hint="Pick one">
                  {shape.variants.length === 0 && <p className="text-xl text-ink-soft">This item has no variants set up yet. Please ask a member of staff.</p>}
                  {shape.variants.map((variant) => (
                    <OptionCard
                      key={variant.id}
                      kind="radio"
                      title={variantLabel(variant)}
                      price={formatPeso(variant.priceOverride ?? item.basePrice)}
                      selected={picks.variantId === variant.id}
                      onSelect={() => setPicks((current) => ({ ...current, variantId: variant.id }))}
                    />
                  ))}
                </ChoicePanel>
              )}
              {activeTab?.id.startsWith('slot:') && <SlotPanel slot={shape.slots.find((slot) => `slot:${slot.id}` === activeTab.id)} items={allItems} picks={picks} setPicks={setPicks} />}
              {activeTab?.id.startsWith('group:') && <GroupPanel group={shape.groups.find((group) => `group:${group.id}` === activeTab.id)} picks={picks} setPicks={setPicks} />}
            </div>
          )}
        </div>
      </div>

      <div className={barClass}>
        <div className="flex min-w-40 flex-1 flex-col justify-center">
          <span className={`text-base font-semibold ${problem ? 'text-warn' : 'text-ink-soft'}`} role="status">
            {problem ?? `Total for ${picks.quantity === 1 ? 'this item' : `${picks.quantity} items`}`}
          </span>
          <span className="text-3xl font-extrabold tabular-nums">{formatPeso(lineTotal)}</span>
        </div>
        <button type="button" className={secondaryButton} onClick={() => navigate(backTo)} disabled={added}>
          Cancel
        </button>
        {nextTab && (
          <button type="button" className={secondaryButton} onClick={() => setActive(nextTab.id)} disabled={added}>
            Next: {nextTab.label}
          </button>
        )}
        <button type="button" className={primaryButton} onClick={commit} disabled={!shape || problem !== null || added || cap.max < 1}>
          {editing ? 'Update order' : 'Add to order'}
        </button>
      </div>

      {added && (
        <AddedToOrderOverlay
          title={editing ? 'Order updated' : 'Added to your order'}
          subtitle={`${picks.quantity} x ${item.name}`}
          onDone={() => navigate(backTo, { replace: true })}
        />
      )}
    </div>
  );
}

function buildTabs(shape: OptionShape, picks: OptionPicks, items: Item[]): Tab[] {
  const tabs: Tab[] = [{ id: 'quantity', label: 'Quantity', hint: `${picks.quantity} selected`, resolved: true }];

  if (shape.needsVariant) {
    const chosen = shape.variants.find((variant) => variant.id === picks.variantId);
    tabs.push({ id: 'variant', label: 'Size / Variant', hint: chosen ? variantLabel(chosen) : 'Required', resolved: Boolean(chosen) });
  }

  for (const slot of shape.slots.filter((candidate) => !isFixedSlot(candidate))) {
    const chosen = (picks.slots[slot.id] ?? []).filter(Boolean);
    const names = chosen.map((id) => items.find((candidate) => candidate.id === id)?.name ?? '').filter(Boolean);
    tabs.push({ id: `slot:${slot.id}`, label: slot.slotLabel, hint: chosen.length >= slot.quantity ? names.join(', ') : 'Required', resolved: chosen.length >= slot.quantity });
  }

  for (const group of shape.groups) {
    const chosen = picks.groups[group.id];
    const names = (chosen ?? []).map((id) => groupOptions(group).find((option) => option.id === id)?.name ?? '').filter(Boolean);
    const resolved = group.isRequired ? (chosen ?? []).length > 0 : chosen !== undefined;
    tabs.push({
      id: `group:${group.id}`,
      label: group.name,
      hint: resolved ? (names.length > 0 ? names.join(', ') : noneLabel(group.name)) : group.isRequired ? 'Required' : 'Optional',
      resolved,
    });
  }
  return tabs;
}

function TabButton({ tab, selected, onSelect }: { tab: Tab; selected: boolean; onSelect: () => void }) {
  const ref = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    if (selected) ref.current?.scrollIntoView?.({ block: 'nearest' });
  }, [selected]);

  return (
    <button
      ref={ref}
      type="button"
      role="tab"
      aria-selected={selected}
      onClick={onSelect}
      className={`flex min-h-24 w-full shrink-0 snap-start items-center gap-3 rounded-panel px-4 py-3 text-left transition-colors duration-150 ${
        selected ? 'bg-brand text-on-brand' : 'bg-canvas text-ink hover:bg-brand-tint'
      }`}
    >
      <span className="flex min-w-0 flex-1 flex-col">
        <span className="text-xl font-bold leading-snug">{tab.label}</span>
        <span className={`line-clamp-2 text-base ${selected ? 'text-on-brand/85' : 'text-ink-soft'}`}>{tab.hint}</span>
      </span>
      {tab.id === 'quantity' ? null : tab.resolved ? (
        <CheckCircle size={30} weight="fill" aria-label="Chosen" className={selected ? 'text-on-brand' : 'text-ok'} />
      ) : (
        <Circle size={30} weight="bold" aria-label="Needs a choice" className={selected ? 'text-on-brand' : 'text-warn'} />
      )}
    </button>
  );
}

function ChoicePanel({ title, hint, children }: { title: string; hint: string; children: React.ReactNode }) {
  return (
    <section className="flex flex-col gap-4">
      <div>
        <h1 className="text-4xl font-extrabold tracking-tight">{title}</h1>
        <p className="text-xl text-ink-soft">{hint}</p>
      </div>
      <div className="grid grid-cols-[repeat(auto-fill,minmax(19rem,1fr))] gap-3">{children}</div>
    </section>
  );
}

function QuantityPanel({ item, shape, quantity, cap, onChange }: { item: Item; shape: OptionShape; quantity: number; cap: ReturnType<typeof quantityCap>; onChange: (quantity: number) => void }) {
  const included = shape.slots.filter(isFixedSlot);
  const stepper = 'grid size-20 place-items-center rounded-full border-2 border-line bg-surface transition-transform duration-100 hover:border-brand active:scale-95 disabled:opacity-35';

  return (
    <section className="flex flex-col items-center gap-6 text-center landscape:flex-row landscape:items-center landscape:text-left">
      <div className="aspect-[4/3] w-full max-w-md shrink-0 overflow-hidden rounded-panel bg-canvas">
        <PurchImage src={item.imageUrl} alt="" className="size-full object-cover" errorNode={<span className="block size-full bg-brand-tint" />} />
      </div>
      <div className="flex flex-1 flex-col items-center gap-5 landscape:items-start">
        <div>
          <h1 className="text-4xl font-extrabold tracking-tight">{item.name}</h1>
          <p className="text-2xl font-bold tabular-nums text-brand-strong">{formatPeso(item.basePrice)}</p>
        </div>
        {included.length > 0 && (
          <ul className="flex flex-col gap-1 text-xl text-ink-soft" aria-label="Included">
            {included.map((slot) => (
              <li key={slot.id}>
                Includes {slot.quantity > 1 ? `${slot.quantity} x ` : ''}
                {slot.componentItemName ?? slot.slotLabel}
              </li>
            ))}
          </ul>
        )}
        <div className="flex items-center gap-6" role="group" aria-label="Quantity">
          <button type="button" aria-label="Decrease quantity" className={stepper} disabled={quantity <= 1} onClick={() => onChange(quantity - 1)}>
            <Minus size={36} weight="bold" aria-hidden="true" />
          </button>
          <span aria-live="polite" className="min-w-16 text-center text-6xl font-extrabold tabular-nums">
            {quantity}
          </span>
          <button type="button" aria-label="Increase quantity" className={stepper} disabled={quantity >= cap.max} onClick={() => onChange(quantity + 1)}>
            <Plus size={36} weight="bold" aria-hidden="true" />
          </button>
        </div>
        {cap.limitedByStock && <p className="text-xl font-semibold text-warn">{cap.max === 0 ? 'None left' : `Only ${cap.max} left`}</p>}
      </div>
    </section>
  );
}

function SlotPanel({ slot, items, picks, setPicks }: { slot: ItemComboComponent | undefined; items: Item[]; picks: OptionPicks; setPicks: React.Dispatch<React.SetStateAction<OptionPicks>> }) {
  if (!slot) return null;
  const choices = slotChoices(slot, items);
  const units = Array.from({ length: slot.quantity }, (_, index) => index);

  return (
    <div className="flex flex-col gap-8">
      {units.map((unit) => (
        <ChoicePanel key={unit} title={slot.quantity > 1 ? `${slot.slotLabel} (${unit + 1} of ${slot.quantity})` : slot.slotLabel} hint="Pick one">
          {choices.length === 0 && <p className="text-xl text-ink-soft">Nothing is available for this choice right now.</p>}
          {choices.map((choice) => (
            <OptionCard
              key={choice.item.id}
              kind="radio"
              title={choice.item.name}
              price={choice.upcharge > 0 ? `+${formatPeso(choice.upcharge)}` : 'Included'}
              imageUrl={choice.item.imageUrl}
              showImage
              soldOut={choice.soldOut}
              selected={picks.slots[slot.id]?.[unit] === choice.item.id}
              onSelect={() =>
                setPicks((current) => {
                  const next = [...(current.slots[slot.id] ?? Array.from({ length: slot.quantity }, () => ''))];
                  next[unit] = choice.item.id;
                  return { ...current, slots: { ...current.slots, [slot.id]: next } };
                })
              }
            />
          ))}
        </ChoicePanel>
      ))}
    </div>
  );
}

function GroupPanel({ group, picks, setPicks }: { group: ModifierGroup | undefined; picks: OptionPicks; setPicks: React.Dispatch<React.SetStateAction<OptionPicks>> }) {
  if (!group) return null;
  const chosen = picks.groups[group.id];
  const choseNone = chosen !== undefined && chosen.length === 0;

  return (
    <ChoicePanel title={group.name} hint={group.isRequired ? (group.allowMultipleSelection ? 'Required. Pick at least one' : 'Required. Pick one') : group.allowMultipleSelection ? 'Optional. Pick any' : 'Optional. Pick one'}>
      {!group.isRequired && (
        <OptionCard kind="radio" title={noneLabel(group.name)} selected={choseNone} onSelect={() => setPicks((current) => ({ ...current, groups: { ...current.groups, [group.id]: [] } }))} />
      )}
      {groupOptions(group).map((modifier) => (
        <OptionCard
          key={modifier.id}
          kind={group.allowMultipleSelection ? 'checkbox' : 'radio'}
          title={modifier.name}
          price={modifier.priceDelta !== 0 ? `${modifier.priceDelta > 0 ? '+' : '-'}${formatPeso(Math.abs(modifier.priceDelta))}` : undefined}
          soldOut={modifier.soldOut}
          selected={(chosen ?? []).includes(modifier.id)}
          onSelect={() => setPicks((current) => ({ ...current, groups: { ...current.groups, [group.id]: toggleModifier(group, current.groups[group.id] ?? [], modifier.id) } }))}
        />
      ))}
    </ChoicePanel>
  );
}
