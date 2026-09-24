import { ChartTip, Legend } from './ChartParts';
import { useChartTip, useElementWidth } from './chartHooks';
import { shareLabel } from './scale';

export interface PartItem {
  key: string;
  label: string;
  value: number;
  color: string;
}

/** One bar split into parts: thin, rounded only at its two ends, with a 2px gap of surface between parts. */
export function StackedStrip({ items, formatValue, ariaLabel }: { items: PartItem[]; formatValue: (value: number) => string; ariaLabel: string }) {
  const [ref, width] = useElementWidth<HTMLDivElement>();
  const { tip, show, hide } = useChartTip();
  const total = items.reduce((sum, i) => sum + Math.max(0, i.value), 0);
  const parts = items.filter((i) => i.value > 0);

  return (
    <figure>
      <div ref={ref} className="relative">
        <div role="img" aria-label={ariaLabel} className="flex h-5 w-full gap-0.5 overflow-hidden rounded-[4px]">
          {parts.map((item) => (
            <div
              key={item.key}
              className="h-full min-w-1"
              style={{ width: `${(item.value / total) * 100}%`, background: item.color }}
              onPointerEnter={(e) => {
                const box = e.currentTarget.getBoundingClientRect();
                const host = ref.current?.getBoundingClientRect();
                show(box.left - (host?.left ?? 0) + box.width / 2, 0, { title: item.label, rows: [{ label: shareLabel(item.value, total), value: formatValue(item.value), color: item.color }] });
              }}
              onPointerLeave={hide}
            />
          ))}
        </div>
        <ChartTip tip={tip} width={width} />
      </div>
      <Legend className="mt-4 max-w-lg" items={items.map((i) => ({ key: i.key, label: i.label, color: i.color, detail: `${formatValue(i.value)}, ${shareLabel(i.value, total)}` }))} />
    </figure>
  );
}

export interface WaffleItem {
  key: string;
  label: string;
  count: number;
  color: string;
  /** Drawn as an outline, for people or things that exist but are not active. */
  hollow?: boolean;
}

const PER_ROW = 10;

/** One square per person or thing, grouped by kind. Good for small counts, where a bar would hide the individuals. */
export function Waffle({ items, unit, ariaLabel }: { items: WaffleItem[]; unit: string; ariaLabel: string }) {
  const squares = items.flatMap((item) => Array.from({ length: item.count }, (_, i) => ({ id: `${item.key}-${i}`, item })));
  const total = squares.length;

  return (
    <figure className="flex flex-wrap items-start gap-x-8 gap-y-4">
      <div role="img" aria-label={`${ariaLabel}: ${total} ${unit}`} className="grid shrink-0 gap-1" style={{ gridTemplateColumns: `repeat(${Math.min(PER_ROW, Math.max(total, 1))}, 1.25rem)` }}>
        {squares.map(({ id, item }) => (
          <span
            key={id}
            title={item.label}
            className="size-5 rounded-[5px]"
            style={item.hollow ? { border: `2px solid ${item.color}` } : { background: item.color }}
          />
        ))}
      </div>
      <Legend className="min-w-48 max-w-sm flex-1" items={items.map((i) => ({ key: i.key, label: i.label, color: i.color, hollow: i.hollow, detail: String(i.count) }))} />
    </figure>
  );
}
