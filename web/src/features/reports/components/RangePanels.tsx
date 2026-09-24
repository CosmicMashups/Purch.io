import { OTHER_COLOR, assignColors } from '../../../components/charts/colors';
import { Donut, type DonutSlice } from '../../../components/charts/Donut';
import { Meter } from '../../../components/charts/Meter';
import { BarList, DotPlot, LollipopList } from '../../../components/charts/RankedCharts';
import { foldTail } from '../../../components/charts/scale';
import { AsyncPanel } from '../../dashboard/components/AsyncPanel';
import { formatPeso } from '../../dashboard/format';
import { movementLabel } from '../../inventory/movement';
import { useDepartmentSales, useMovementSummary, useStaffPerformance } from '../queries';
import type { DepartmentSales, RangeParams } from '../types';

const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`;

export function StaffPanels({ params }: { params: RangeParams }) {
  const report = useStaffPerformance(params);
  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <AsyncPanel
        title="Sales per cashier"
        subtitle="Revenue rung up in this period"
        query={report}
        isEmpty={(d) => d.sales.length === 0}
        emptyMessage="No staff sales in this period."
      >
        {(d) => (
          <LollipopList
            formatValue={formatPeso}
            rows={[...d.sales]
              .sort((a, b) => b.totalSales - a.totalSales)
              .map((s) => ({ key: s.staffUserId, label: s.staffName, value: s.totalSales, detail: plural(s.transactionCount, 'sale') }))}
          />
        )}
      </AsyncPanel>

      <AsyncPanel
        title="Shift attendance"
        subtitle="Shifts opened, and how many closed with a cash variance"
        query={report}
        isEmpty={(d) => d.shiftAttendance.length === 0}
        emptyMessage="No shifts were opened in this period."
      >
        {(d) => (
          <ul className="flex flex-col gap-5">
            {[...d.shiftAttendance]
              .sort((a, b) => b.shiftsOpened - a.shiftsOpened)
              .map((s) => (
                <li key={s.staffUserId}>
                  <Meter
                    label={s.staffName}
                    value={s.shiftsWithVariance}
                    total={s.shiftsOpened}
                    unit="shifts with a cash difference"
                    tone={s.shiftsWithVariance > 0 ? 'warn' : 'ok'}
                    status={s.shiftsWithVariance > 0 ? `${plural(s.shiftsWithVariance, 'shift')} closed with a cash difference` : 'Every cash count matched'}
                  />
                </li>
              ))}
          </ul>
        )}
      </AsyncPanel>
    </div>
  );
}

const MAX_SLICES = 6;

const departmentKey = (d: DepartmentSales) => d.departmentId ?? 'general';

/** Departments as slices, colours fixed per department. Past six, the smallest fold into one muted "Other". */
function departmentSlices(rows: DepartmentSales[]): DonutSlice[] {
  const colors = assignColors(rows.map(departmentKey));
  const positive = rows.filter((r) => r.revenue > 0);
  const folded = foldTail<DepartmentSales>(positive, MAX_SLICES, (r) => r.revenue, (rest, total) => ({
    departmentId: 'other',
    departmentName: `Other (${rest.length})`,
    revenue: total,
  }));
  return folded
    .sort((a, b) => b.revenue - a.revenue)
    .map((r) => ({ key: departmentKey(r), label: r.departmentName, value: r.revenue, color: r.departmentId === 'other' ? OTHER_COLOR : (colors.get(departmentKey(r)) as string) }));
}

export function DepartmentPanel({ params }: { params: RangeParams }) {
  const report = useDepartmentSales(params);
  return (
    <AsyncPanel
      title="Sales by department"
      subtitle="Revenue split by department. Items with no department are under General."
      query={report}
      isEmpty={(d) => d.filter((r) => r.revenue > 0).length === 0}
      emptyMessage="No sales in this period."
    >
      {(d) => {
        const slices = departmentSlices(d);
        const total = slices.reduce((sum, s) => sum + s.value, 0);
        // A ring needs at least three parts to say anything a bar does not.
        return slices.length < 3 ? (
          <BarList formatValue={formatPeso} rows={slices.map((s) => ({ key: s.key, label: s.label, value: s.value }))} />
        ) : (
          <Donut slices={slices} formatValue={formatPeso} totalLabel="Total" totalValue={formatPeso(total)} ariaLabel="Revenue by department" />
        );
      }}
    </AsyncPanel>
  );
}

export function MovementPanel({ params }: { params: RangeParams }) {
  const report = useMovementSummary(params);
  return (
    <AsyncPanel
      title="Stock movement"
      subtitle="Quantity moved by reason in this period"
      query={report}
      isEmpty={(d) => d.byType.length === 0}
      emptyMessage="No stock movements in this period."
    >
      {(d) => (
        <DotPlot
          formatValue={(n) => String(n)}
          rows={[...d.byType]
            .sort((a, b) => b.totalQuantity - a.totalQuantity)
            .map((row) => ({ key: String(row.type), label: movementLabel(row.type), value: row.totalQuantity, detail: plural(row.movementCount, 'record') }))}
        />
      )}
    </AsyncPanel>
  );
}
